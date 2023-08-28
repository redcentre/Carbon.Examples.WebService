﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Carbon.Examples.WebService.Common;
using Microsoft.Extensions.Logging;
using RCS.Carbon.Shared;
using RCS.RubyCloud.WebService;

namespace Carbon.Examples.WebService.WebApi.Controllers;

partial class JobController
{
	#region OXT Helpers

	/// <summary>
	/// This method runs on a worker thread that is spun-up by a MultiOxtStart endpoint call.
	/// An engine instance is created for the Session Id to use for the duration of the OXT
	/// loop processing, so multiple threads may run for the same session (although the client
	/// UI doesn't permit this at the moment).
	/// </summary>
	void MultiOxtSequentialProc(object? o)
	{
		Log.Info("MultiOxtProc START");
		var state = (MoxtState)o!;
		var watch = new Stopwatch();
		using var wrap = new StateWrap(state.SessionId, LicProv, true);
		var list = new List<RubyMultiOxtItem>();
		string fullfilter = ComposeFilter(state.Request);
		int repcount = state.Request.ReportNames.Length;
		DateTime start = DateTime.Now;
		state.ProgressMessage = "Starting";
		foreach (var tup in state.Request.ReportNames.Select((n, i) => new { Name = n, Ix = i }))
		{
			if (state.CancelSource.IsCancellationRequested)
			{
				// There will be an unpredictable delay before the cancel request is detected,
				// because the current OXT generation may take some time to complete and let
				// the loop come around again. There is currenly no way to 'interrupt' Carbon
				// crosstab processing.
				Log.Warn($"Multi OXT loop {state.Id} cancelled");
				state.Items = list.ToArray();
				state.ProgressMessage = "Cancelled";
				watch.Stop();
				return;
			}
			try
			{
				state.ProgressMessage = $"Running report {tup.Ix + 1}/{state.Request.ReportNames.Length}";
				Log.Info(state.ProgressMessage);
				watch.Restart();
				string fixname = FixMultiName(tup.Name);
				string oxt = wrap.Engine.DrillDashboardTableAsOXT(tup.Name, fullfilter);
				string[] lines = CommonUtil.ReadStringLines(oxt).ToArray();

				double repsecs = watch.Elapsed.TotalSeconds;
				double totalsecs = DateTime.Now.Subtract(start).TotalSeconds;
				Logger.LogDebug(888, "Loop {Ix}/{Count} {RepSecs,5:F1}/{TotalSecs:F1} {FixName}", tup.Ix + 1, repcount, repsecs, totalsecs, fixname);
				int? titlesRowCount = GetMetaInt(lines, "Titles RowCount");
				bool? dispColLetters = GetMetaBool(lines, "Display ColumnLetters");
				bool? dispRowLetters = GetMetaBool(lines, "Display RowLetters");
				bool? sigShowLetters = GetMetaBool(lines, "Significance ShowLetters");
				if (state.Request.TableOnly)
				{
					// FRAGILE --> If this option is set then we only take the lines from [Table] stopping before the next section (or end).
					// Callers may only want the [Table] section in most cases, so the size of the total response can be greatly reduced
					// by stripping out the [Table] section lines.
					lines = lines
						.SkipWhile(l => !Regex.IsMatch(l, @"^\[Table\]"))
						.TakeWhile(l => !Regex.IsMatch(l, @"^\[(?!Table)"))
						.ToArray();
				}
				list.Add(new RubyMultiOxtItem()
				{
					ReportName = tup.Name,  // The report name is like a key to link the request and response items
					Titles_RowCount = titlesRowCount,
					DispColLetters = dispColLetters,
					DispRowLetters = dispRowLetters,
					SigShowLetters = sigShowLetters,
					Seconds = repsecs,
					OxtLines = lines
				});
			}
			catch (Exception ex)
			{
				Log.Error(ex.Message);
				var bex = ex.GetBaseException();
				list.Add(new RubyMultiOxtItem()
				{
					ReportName = tup.Name,
					Seconds = watch.Elapsed.TotalSeconds,
					ErrorType = bex.GetType().Name,
					ErrorMessage = bex.Message
				});
			}
		}
		watch.Stop();
		Log.Info($"MultiOxtProc END =============== [{DateTime.Now.Subtract(multiOxtStartTime).TotalSeconds:F2}] ===============");
		// Setting the Itemds array here is the magic moment where this background thread
		// is saying that it has finished the OXT generation loop and the results are available.
		// Polling through the query endpoint will detect that the Items are available and the
		// query response will contain the items.
		state.Items = list.ToArray();
		var secs = DateTime.UtcNow.Subtract(state.Created).TotalSeconds;
		Logger.LogDebug(330, "Complete #{repcount}) {state.Items.Length} [{Secs:F2}]", repcount, state.Items.Length, secs);
		state.ProgressMessage = $"Completed {repcount} reports";
	}

	/// <summary>
	/// EXPERIMENTAL -- Runs on a Thread and generates multiple OXT reports in parallel using
	/// a Carbon engine instance for each report. Note that the parallel processing count is
	/// limited to the number of cores available.
	/// </summary>
	void MultiOxtParallelProc(object? o)
	{
		Log.Info("MultiOxtParallelProc START");
		var state = (MoxtState)o!;
		var watch = new Stopwatch();
		watch.Start();
		int repcount = state.Request.ReportNames.Length;
		string fullfilter = ComposeFilter(state.Request);
		state.ProgressMessage = "Starting";
		var holditems = new RubyMultiOxtItem[repcount];
		var ixlist = new List<int>();
		int donecount = 0;
		//state.ParallelCount = Math.Min(state.ParallelCount, Environment.ProcessorCount);

		var po = new ParallelOptions { MaxDegreeOfParallelism = state.ParallelCount };
		Parallel.For(0, repcount, po, ix =>
		{
			if (state.CancelSource.IsCancellationRequested) return;
			string name = state.Request.ReportNames[ix];
			Logger.LogDebug(889, "Start parallel {Ix} {Name}", ix, name);
			double offsecs1 = watch.Elapsed.TotalSeconds;
			lock (ixlist)
			{
				ixlist.Add(ix);
				state.ProgressMessage = string.Format("{0} ({1}/{2})", string.Join(" ", ixlist), donecount, repcount);
			}
			using (var wrap = new StateWrap(state.SessionId, LicProv, true))
			{
				try
				{
					string oxt = wrap.Engine.DrillDashboardTableAsOXT(name, fullfilter);
					string[] lines = CommonUtil.ReadStringLines(oxt).ToArray();
					int? titlesRowCount = GetMetaInt(lines, "Titles RowCount");
					bool? dispColLetters = GetMetaBool(lines, "Display ColumnLetters");
					bool? dispRowLetters = GetMetaBool(lines, "Display RowLetters");
					bool? sigShowLetters = GetMetaBool(lines, "Significance ShowLetters");
					if (state.Request.TableOnly)
					{
						// FRAGILE --> If this option is set then we only take the lines from [Table] stopping before the next section (or end).
						// Callers may only want the [Table] section in most cases, so the size of the total response can be greatly reduced
						// by stripping out the [Table] section lines.
						lines = lines
							.SkipWhile(l => !Regex.IsMatch(l, @"^\[Table\]"))
							.TakeWhile(l => !Regex.IsMatch(l, @"^\[(?!Table)"))
							.ToArray();
					}
					holditems[ix] = new RubyMultiOxtItem()
					{
						ReportName = name,
						Titles_RowCount = titlesRowCount,
						DispColLetters = dispColLetters,
						DispRowLetters = dispRowLetters,
						SigShowLetters = sigShowLetters,
						Seconds = watch.Elapsed.TotalSeconds - offsecs1,
						OxtLines = lines
					};
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "Parallel OXT[{Ix}] {Name}", ix, name);
					var bex = ex.GetBaseException();
					holditems[ix] = new RubyMultiOxtItem()
					{
						ReportName = name,
						Seconds = watch.Elapsed.TotalSeconds - offsecs1,
						ErrorType = bex.GetType().Name,
						ErrorMessage = bex.Message
					};
				}
			}
			lock (ixlist)
			{
				Interlocked.Increment(ref donecount);
				ixlist.Remove(ix);
				state.ProgressMessage = string.Format("{0} ({1}/{2})", string.Join("+", ixlist), donecount, repcount);
			}
			double offsecs2 = watch.Elapsed.TotalSeconds;
			double secs = offsecs2 - offsecs1;
			Logger.LogDebug(889, "End parallel {Ix} [{Secs:F2}] {Offsecs1:F0} {Offsecs2:F0}", ix, secs, offsecs1, offsecs2);
		});

		state.Items = holditems;
		double secs = watch.Elapsed.TotalSeconds;
		watch.Stop();
		state.ProgressMessage = $"Completed {repcount} reports [{secs:F2}]";
		Log.Info($"MultiOxtParallelProc END [{secs:F2}]");
	}

	static string ComposeFilter(MultiOxtRequest request)
	{
		var parts = new List<string>();
		// Period filters have special handling
		var perfilts = request.Filters.Where(f => f.IsPeriod && f.Label != null && f.Syntax != null).ToArray();
		if (perfilts.Length == 2)
		{
			parts.Add($"{perfilts[0].Label}({perfilts[0].Syntax}/{perfilts[1].Syntax})");
		}
		else if (perfilts.Length == 1)
		{
			parts.Add($"{perfilts[0].Label}({perfilts[0].Syntax})");
		}
		// Loop over normal filters
		foreach (var filt in request.Filters.Except(perfilts).Where(f => f.Label != null && f.Syntax != null))
		{
			parts.Add(filt.Syntax);
		}
		return string.Join("&", parts);
	}

	static string FixMultiName(string reportName)
	{
		reportName = reportName.Trim('/', '\\');
		return reportName.Replace('\\', '/');
	}

	#endregion

	#region Meta Helpers

	static IEnumerable<string> GetMetaLines(IEnumerable<string> lines)
	{
		return lines
			.SkipWhile(l => !Regex.IsMatch(l, @"^\[MetaData\]"))
			.TakeWhile(l => !Regex.IsMatch(l, @"^\[(?!MetaData)"));
	}

	static int? GetMetaInt(IEnumerable<string> lines, string key)
	{
		Match? m = GetMetaLines(lines)
			.Select(l => Regex.Match(l, $@"^{key}=(\d+)", RegexOptions.IgnoreCase))
			.FirstOrDefault(x => x.Success);
		return m == null ? null : int.Parse(m.Groups[1].Value);
	}

	static bool? GetMetaBool(IEnumerable<string> lines, string key)
	{
		Match? m = GetMetaLines(lines)
			.Select(l => Regex.Match(l, $@"^{key}=(true|false)", RegexOptions.IgnoreCase))
			.FirstOrDefault(x => x.Success);
		return m == null ? null : bool.Parse(m.Groups[1].Value);
	}

	#endregion

	#region Multi-OXT State

	public static MoxtState MakeState(MultiOxtRequest request)
	{
		lock (MoxtList)
		{
			var moxt = new MoxtState(request);
			MoxtList.Add(moxt);
			//Logger.LogInformation(890, "Multi OXT Id {MoxtId} added (count up to {MoxtCount})", moxt.Id, MoxtList.Count);
			MoxtCleanup();
			return moxt;
		}
	}

	public static MoxtState? GetState(Guid id)
	{
		lock (MoxtList)
		{
			return MoxtList.FirstOrDefault(m => m.Id == id);
		}
	}

	public static int StateCount
	{
		get
		{
			lock (MoxtList)
			{
				return MoxtList.Count;
			}
		}
	}

	public static bool CancelState(Guid id)
	{
		lock (MoxtList)
		{
			var state = MoxtList.FirstOrDefault(m => m.Id == id);
			if (state == null) return false;
			state.CancelSource.Cancel();
			return true;
		}
	}

	public static bool RemoveState(Guid id)
	{
		lock (MoxtList)
		{
			var state = MoxtList.FirstOrDefault(m => m.Id == id);
			if (state == null) return false;
			state.Dispose();
			MoxtList.Remove(state);
			return true;
		}
	}

	public static List<MoxtState> MoxtList = new List<MoxtState>();

	static void MoxtCleanup()
	{
		foreach (var moxt in MoxtList.ToArray())
		{
			int mins = (int)DateTime.UtcNow.Subtract(moxt.Created).TotalMinutes;
			if (mins > 20)
			{
				moxt.Dispose();
				MoxtList.Remove(moxt);
				//LogInfo(891, $"Multi OXT Id {moxt.Id} stale {mins} minutes (count down to {MoxtList.Count})");
			}
		}
	}

	#endregion
}
