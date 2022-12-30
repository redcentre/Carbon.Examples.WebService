using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Carbon.Examples.WebService.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RCS.Carbon.Shared;
using RCS.Carbon.Tables;
using RCS.Carbon.Variables;
using RCS.RubyCloud.WebService;

namespace Carbon.Examples.WebService.WebApi.Controllers
{
	partial class JobController
	{
		async Task<ActionResult<OpenCloudJobResponse>> OpenCloudJobImpl(OpenCloudJobRequest request)
		{
			using var wrap = new StateWrap(SessionId, true);
			try
			{
				wrap.Engine.OpenJob(request.CustomerName, request.JobName);
			}
			catch (CarbonException ex)
			{
				return BadRequest(new ErrorResponse(201, $"Open cloud Customer '{request.CustomerName}' Job '{request.JobName}' failed - {ex.Message}"));
			}
			SessionManager.SetCustomerJob(SessionId, request.CustomerName, request.JobName);
			var dprops = request.GetDisplayProps ? wrap.Engine.GetProps() : null;
			var vtnames = request.GetVartreeNames ? wrap.Engine.ListVartreeNames().ToArray() : null;
			var axnames = request.GetAxisTreeNames ? wrap.Engine.ListAxisNames().ToArray() : null;
			GenNode[]? tocnodes = null;
			if (request.TocType != JobTocType.None)
			{
				if (request.TocType == JobTocType.Simple)
				{
					tocnodes = wrap.Engine.SimpleTOCGenNodes();
				}
				else if (request.TocType == JobTocType.ExecUser)
				{
					tocnodes = wrap.Engine.ExecUserTOCGenNodes();
				}
			}
			var jobini = request.GetIni ? wrap.Engine.GetJobIniAsNodes() : null;
			var response = new OpenCloudJobResponse(dprops, vtnames, axnames, tocnodes, jobini);
			Logger.LogInformation(240, "{RequestSequence} {Sid} Open job '{CustomerName}' Job '{JobName}' {DProps} {VtCount} {AxCount} {TocNewCount}", RequestSequence, Sid, request.CustomerName, request.JobName, dprops, vtnames?.Length, axnames?.Length, tocnodes?.Length);
			return await Task.FromResult(response);
		}

		async Task<ActionResult<bool>> CloseJobImpl()
		{
			using var wrap = new StateWrap(SessionId, false);
			bool closed = wrap.Engine.CloseJob();
			SessionManager.SetCustomerJob(SessionId, null, null);
			return await Task.FromResult(closed);
		}

		async Task<ActionResult<string[]>> ReadFileAsLinesImpl(ReadFileRequest request)
		{
			using var wrap = new StateWrap(SessionId, false);
			string[] lines = wrap.Engine.ReadFileLines(request.Name).ToArray();
			Logger.LogInformation(251, "{RequestSequence} {Sid} Set props", RequestSequence, Sid);
			return await Task.FromResult(lines);
		}

		//async Task<ActionResult<GenNode[]>> ListTocImpl()
		//{
		//	using var wrap = new StateWrap(SessionId, false);
		//	GenNode[] gnodes = wrap.Engine.ListSavedReports();
		//	Logger.LogInformation(241, "{RequestSequence} {Sid} List TOC Nodes -> {Count}", RequestSequence, Sid, gnodes.Length);
		//	return await Task.FromResult(gnodes);
		//}

		//async Task<ActionResult<GenNode[]>> ListLegacyTocImpl()
		//{
		//	using var wrap = new StateWrap(SessionId, false);
		//	GenNode[] gnodes = wrap.Engine.GetLegacyTocAsNodes();
		//	Logger.LogInformation(241, "{RequestSequence} {Sid} List legacy TOC Nodes -> {Count}", RequestSequence, Sid, gnodes.Length);
		//	return await Task.FromResult(gnodes);
		//}

		async Task<ActionResult<GenNode[]>> ListSimpleTocImpl(bool load)
		{
			using var wrap = new StateWrap(SessionId, false);
			GenNode[] gnodes = wrap.Engine.SimpleTOCGenNodes();
			Logger.LogInformation(241, "{RequestSequence} {Sid} List simple TOC Nodes {Load} -> {Count}", RequestSequence, Sid, load, gnodes.Length);
			return await Task.FromResult(gnodes);
		}

		async Task<ActionResult<GenNode[]>> ListFullTocImpl(bool load)
		{
			using var wrap = new StateWrap(SessionId, false);
			GenNode[] gnodes = wrap.Engine.FullTOCGenNodes();
			Logger.LogInformation(241, "{RequestSequence} {Sid} List full TOC Nodes {Load} -> {Count}", RequestSequence, Sid, load, gnodes.Length);
			return await Task.FromResult(gnodes);
		}

		async Task<ActionResult<GenNode[]>> ListExecUserTocImpl(bool load)
		{
			using var wrap = new StateWrap(SessionId, false);
			GenNode[] gnodes = wrap.Engine.ExecUserTOCGenNodes();
			Logger.LogInformation(241, "{RequestSequence} {Sid} List ExecUser TOC Nodes {Load} -> {Count}", RequestSequence, Sid, load, gnodes.Length);
			return await Task.FromResult(gnodes);
		}

		async Task<ActionResult<XDisplayProperties>> GetPropsImpl()
		{
			using var wrap = new StateWrap(SessionId, false);
			XDisplayProperties jobprops = wrap.Engine.GetProps();
			return await Task.FromResult(jobprops);
		}

		async Task<ActionResult<int>> SetPropsImpl(XDisplayProperties request)
		{
			using var wrap = new StateWrap(SessionId, true);
			wrap.Engine.SetProps(request);
			Logger.LogInformation(251, "{RequestSequence} {Sid} Set props", RequestSequence, Sid);
			return await Task.FromResult(0);
		}

		async Task<ActionResult<XlsxResponse>> FilterImpl(FilterRequest request)
		{
			//using var wrap = new StateWrap(SessionId, true);
			//bool result = wrap.Engine.DrillFilter(request.drill);
			//Logger.LogInformation(252, "{RequestSequence} {Sid} Filter '{Drill}'", RequestSequence, Sid, request.drill);
			//byte[] blob = XTableOutputManager.AsSingleXLSXBuffer(wrap.Engine.Job.DisplayTable);
			//string upname = Path.ChangeExtension(request.ReportName, ".xlsx");
			//var sess = SessionManager.FindSession(SessionId);
			//var azblob = await AzProc.UploadBufferForReport(null, sess.OpenCustomerName, sess.OpenJobName, upname, blob);
			return new XlsxResponse();
		}

		async Task<ActionResult<GenericResponse>> DeleteReportImpl(DeleteReportRequest request)
		{
			// TODO Test Carbon ▐ DeleteReport
			using var wrap = new StateWrap(SessionId, true);
			bool success = wrap.Engine.DeleteCBT(request.Name);
			return await Task.FromResult(new GenericResponse(0, $"Deleted {request.Name}"));
		}

		async Task<ActionResult<string[]>> GenTabImpl(GenTabRequest request)
		{
			using var wrap = new StateWrap(SessionId, true);
			string[] lines;
			if (request.DProps.Output.Format == XOutputFormat.XLSX)
			{
				// XLSX Excel output is a special case and does not return a typical set of report lines.
				// It natively generates the buffer of an XLSX workbook, which is converted to a single
				// base64 encoded string line for return.
				byte[] buff = wrap.Engine.GenTabAsXLSX(request.Name, request.Top, request.Side, request.Filter, request.Weight, request.SProps, request.DProps);
				lines = new string[] { Convert.ToBase64String(buff) };
			}
			else
			{
				// All other reports can be returned as lines. The lines maybe null for format None.
				string report = wrap.Engine.GenTab(request.Name, request.Top, request.Side, request.Filter, request.Weight, request.SProps, request.DProps);
				if (report == null)
				{
					return NoContent();
				}
				lines = CommonUtil.ReadStringLines(report).ToArray();
			}
			Logger.LogInformation(253, "{RequestSequence} {Sid} GenTab({Format},{Top},{Side},{Filter},{Weight}) -> #{Length})", RequestSequence, Sid, request.DProps.Output.Format, request.Top, request.Side, request.Filter, request.Weight, lines?.Length);
			return await Task.FromResult(lines);
		}

		async Task<ActionResult<GenericResponse>> LoadReportImpl(LoadReportRequest request)
		{
			using var wrap = new StateWrap(SessionId, true);
			SessionManager.SetReportName(SessionId, request.Name);
			wrap.Engine.TableLoadCBT(request.Name);
			return await Task.FromResult(new GenericResponse(0, $"Loaded {request.Name}"));
		}

		async Task<ActionResult<XlsxResponse>> GenerateXlsxImpl()
		{
			var watch = new Stopwatch();
			watch.Start();
			using var wrap = new StateWrap(SessionId, true);
			byte[] blob = XTableOutputManager.AsSingleXLSXBuffer(wrap.Engine.Job.DisplayTable);
			double xlsxsecs = watch.Elapsed.TotalSeconds;
			watch.Restart();
			var sess = SessionManager.FindSession(SessionId);
			string upname = Path.ChangeExtension(sess.OpenReportName, ".xlsx");
			var azblob = await AzProc.UploadBufferForReport(null, sess.OpenCustomerName, sess.OpenJobName, upname, blob);
			double upsecs = watch.Elapsed.TotalSeconds;
			return new XlsxResponse()
			{
				ReportName = sess.OpenReportName,
				ExcelBytes = blob.Length,
				ExcelSecs = xlsxsecs,
				UploadSecs = upsecs,
				ShowFrequencies = wrap.Engine.Job.DisplayTable.DisplayProps.Cells.Frequencies.Visible,
				ShowColPercents = wrap.Engine.Job.DisplayTable.DisplayProps.Cells.ColumnPercents.Visible,
				ShowRowPercents = wrap.Engine.Job.DisplayTable.DisplayProps.Cells.RowPercents.Visible,
				ShowSignificance = wrap.Engine.Job.DisplayTable.DisplayProps.Significance.Visible,
				OriginalFilter = null,	// REMEMBER: This isn't in the Carbon properties yet
				ExcelUri = azblob.Uri.AbsoluteUri
			};
		}

		async Task<ActionResult<MultiOxtResponse>> MultiOxtImpl(MultiOxtRequest request)
		{
			var moxt = new MoxtState(request);
			MultiOxtProc(moxt);
			var response = new MultiOxtResponse();
			response.Id = moxt.Id;
			response.Created = moxt.Created;
			response.ProgressMessage = moxt.ProgressMessage;
			response.Items = moxt.Items;
			return await Task.FromResult(response);
		}

		#region OXT Helpers

		void MultiOxtProc(MoxtState state)
		{
			var watch = new Stopwatch();
			using var wrap = new StateWrap(SessionId, true);
			var list = new List<RubyMultiOxtItem>();
			string fullfilter = ComposeFilter(state.Request);
			int repcount = state.Request.ReportNames.Length;
			DateTime start = DateTime.Now;
			state.ProgressMessage = "Starting";
			foreach (var tup in state.Request.ReportNames.Select((n, i) => new { Name = n, Ix = i }))
			{
				if (state.CancelSource.IsCancellationRequested)
				{
					// QUESTION What to do when a multi-oxt is cancelled?
				}
				try
				{
					state.ProgressMessage = $"Running report {tup.Ix + 1}/{state.Request.ReportNames.Length}";
					watch.Restart();
					string fixname = FixMultiName(tup.Name);
					Trce.Log($"MultiOxtProc {tup.Name}");
					string oxt = wrap.Engine.DrillDashboardTableAsOXT(tup.Name, fullfilter);
					string[] lines = CommonUtil.ReadStringLines(oxt).ToArray();

					double repsecs = watch.Elapsed.TotalSeconds;
					double totalsecs = DateTime.Now.Subtract(start).TotalSeconds;
					Logger.LogTrace(888, "Loop {Ix}/{Count} {RepSecs,5:F1}/{TotalSecs:F1} {FixName}", tup.Ix + 1, repcount, repsecs, totalsecs, fixname);
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
			state.Items = list.ToArray();
			Logger.LogTrace(330, $"Complete #{repcount}) {state.Items.Length} [{Secs:F2}]", repcount, state.Items.Length, Secs);
			state.ProgressMessage = $"Completed {repcount} reports";
		}

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

		async Task<ActionResult<Guid>> MultiOxtStartImpl(MultiOxtRequest request)
		{
			Trce.Log("MultiOxtStartImpl");
			var state = MakeState(request);
			_ = Task.Factory.StartNew((s) =>
			{
				var moxt = (MoxtState)s!;
				MultiOxtProc(moxt);
			}, state, TaskCreationOptions.LongRunning);
			return await Task.FromResult(state.Id);
		}

		async Task<ActionResult<MultiOxtResponse>> MultiOxtQueryImpl(Guid id)
		{
			var response = new MultiOxtResponse();
			var moxt = GetState(id);
			if (moxt != null)
			{
				response.Id = moxt.Id;
				response.Created = moxt.Created;
				response.ProgressMessage = moxt.ProgressMessage;
				response.IsCancelled = moxt.CancelSource.IsCancellationRequested;
				response.Items = moxt.Items;
				if (moxt.Items != null)
				{
					// When the Items array has a value then the loop over the multi-reports
					// is finished and we can remove the state. The caller must recognise that
					// the Items are present and realise that the reports are finished.
					RemoveState(moxt.Id);
					Trce.Log($"Multi OXT Id {id} complete and removed (count down to {MoxtList.Count})");
				}
				else
				{
					//Global.LogInfo(893, $"Multi OXT Id {id} running - {moxt.ProgressMessage}");
				}
			}
			else
			{
				// There is no specific error return for this. The returned Id will be Guid.Empty.
				//Global.LogInfo(894, $"Multi OXT Id {id} not found in {Global.StateCount} items");
			}
			return await Task.FromResult(response);
		}

		async Task<ActionResult<bool>> MultiOxtCancelImpl(Guid id)
		{
			bool success = CancelState(id);
			return await Task.FromResult(success);
		}

		async Task<ActionResult<GenericResponse>> FunctionActionImpl(FunctionActionRequest request)
		{
			// TODO Implement Carbon ▐ FunctionAction
			throw new CarbonServiceException(666, "FunctionAction is not implemented");
		}

		async Task<ActionResult<XDisplayProperties>> GetPropertiesImpl()
		{
			using var wrap = new StateWrap(SessionId, true);
			var dprops = wrap.Engine.GetProps();
			Logger.LogInformation(254, "{RequestSequence} {Sid} GetProperties", RequestSequence, Sid);
			return await Task.FromResult(dprops);
		}

		async Task<ActionResult<string[]>> ReformatTableImpl(XDisplayProperties dprops)
		{
			using var wrap = new StateWrap(SessionId, true);
			string report = wrap.Engine.ReformatTable(dprops);
			string[] lines = CommonUtil.ReadStringLines(report).ToArray();
			Logger.LogInformation(253, "{RequestSequence} {Sid} ReformatTableImpl({Format}) -> #{Length})", RequestSequence, Sid, dprops.Output.Format, lines?.Length);
			return await Task.FromResult(lines);
		}

		async Task<ActionResult<GenericResponse>> SaveReportImpl(SaveReportRequest request)
		{
			using var wrap = new StateWrap(SessionId, true);
			bool success = wrap.Engine.SaveTableUserTOC(request.Name, request.Sub, true);
			Logger.LogInformation(254, "{RequestSequence} {Sid} SaveReport {Name}+{Sub}", RequestSequence, Sid, request.Name, request.Sub);
			return await Task.FromResult(new GenericResponse(0, request.Name));
		}

		async Task<ActionResult<GenNode[]>> ListReportsImpl()
		{
			using var wrap = new StateWrap(SessionId, false);
			GenNode[] nodes = wrap.Engine.ListSavedReports();
			Logger.LogInformation(255, "{RequestSequence} {Sid} ListReports {Count}", RequestSequence, Sid, nodes.Length);
			return Ok(await Task.FromResult(nodes));
		}

		async Task<ActionResult<GenericResponse>> BlankReportImpl()
		{
			// TODO Implement Carbon ▐ BlankReport
			throw new CarbonServiceException(666, "BlankReport is not implemented");
		}

		async Task<ActionResult<XlsxResponse>> QuickUpdateReportImpl(QuickUpdateRequest request)
		{
			var watch = new Stopwatch();
			watch.Start();
			using var wrap = new StateWrap(SessionId, false);
			wrap.Engine.Job.DisplayTable.DisplayProps.Cells.Frequencies.Visible = request.ShowFreq;
			wrap.Engine.Job.DisplayTable.DisplayProps.Cells.ColumnPercents.Visible = request.ShowColPct;
			wrap.Engine.Job.DisplayTable.DisplayProps.Cells.RowPercents.Visible = request.ShowRowPct;
			wrap.Engine.Job.DisplayTable.DisplayProps.Significance.Visible = request.ShowSig;
			wrap.Engine.SwitchFilter(request.Filter);
			wrap.Engine.Job.DisplayTable.PreFormat();
			// TODO Implement Carbon ▐ QuickUpdateReport
			byte[] blob = XTableOutputManager.AsSingleXLSXBuffer(wrap.Engine.Job.DisplayTable);
			double xlsxsecs = watch.Elapsed.TotalSeconds;
			watch.Restart();
			var sess = SessionManager.FindSession(SessionId);
			string upname = Path.ChangeExtension(sess.OpenReportName!, ".xlsx");
			var azblob = await AzProc.UploadBufferForReport(null, sess.OpenCustomerName, sess.OpenJobName, upname, blob);
			double upsecs = watch.Elapsed.TotalSeconds;
			return new XlsxResponse()
			{
				ReportName = sess.OpenReportName!,
				ExcelBytes = blob.Length,
				ExcelSecs = xlsxsecs,
				UploadSecs = upsecs,
				ShowFrequencies = wrap.Engine.Job.DisplayTable.DisplayProps.Cells.Frequencies.Visible,
				ShowColPercents = wrap.Engine.Job.DisplayTable.DisplayProps.Cells.ColumnPercents.Visible,
				ShowRowPercents = wrap.Engine.Job.DisplayTable.DisplayProps.Cells.RowPercents.Visible,
				ShowSignificance = wrap.Engine.Job.DisplayTable.DisplayProps.Significance.Visible,
				OriginalFilter = null,  // REMEMBER: This isn't in the Carbon properties yet
				ExcelUri = azblob.Uri.AbsoluteUri
			};
		}

		async Task<ActionResult<string>> GenTabPandas1Impl()
		{
			using var wrap = new StateWrap(SessionId, false);
			string json = wrap.Engine.TableAsFormat(XOutputFormat.Pandas1);
			Logger.LogInformation(247, "{RequestSequence} {Sid} Pandas1 {Length}", RequestSequence, Sid, json.Length);
			return new JsonResult(await Task.FromResult(json));
		}

		async Task<ActionResult<string>> GenTabPandas2Impl()
		{
			using var wrap = new StateWrap(SessionId, false);
			string json = wrap.Engine.TableAsFormat(XOutputFormat.Pandas2);
			Logger.LogInformation(248, "{RequestSequence} {Sid} Pandas2 {Length}", RequestSequence, Sid, json.Length);
			return new JsonResult(await Task.FromResult(json));
		}

		async Task<ActionResult<string[]>> ListVartreesImpl()
		{
			using var wrap = new StateWrap(SessionId, false);
			string[] names = wrap.Engine.ListVartreeNames().ToArray();
			Logger.LogInformation(242, "{RequestSequence} {Sid} List vartrees {Count}", RequestSequence, Sid, names.Length);
			return Ok(await Task.FromResult(names));
		}

		async Task<ActionResult<GenNode[]>> VartreeAsNodesImpl(string name)
		{
			using var wrap = new StateWrap(SessionId, false);
			GenNode[] gnodes = wrap.Engine.GetVartreeAsNodes(name);
			Logger.LogInformation(248, "{RequestSequence} {Sid} Get vartree '{Name}' Nodes -> {Count}", RequestSequence, Sid, name, gnodes.Length);
			return await Task.FromResult(gnodes);
		}

		async Task<ActionResult<string[]>> ListAxisTreesImpl()
		{
			using var wrap = new StateWrap(SessionId, false);
			string[] names = wrap.Engine.ListAxisNames().ToArray();
			Logger.LogInformation(242, "{RequestSequence} {Sid} List Axis Trees {Count}", RequestSequence, Sid, names.Length);
			return await Task.FromResult(names);
		}

		async Task<ActionResult<GenNode[]>> AxisTreeAsNodesImpl(string name)
		{
			using var wrap = new StateWrap(SessionId, false);
			GenNode[] gnodes = wrap.Engine.GetAxisAsNodes(name);
			Logger.LogInformation(248, "{RequestSequence} {Sid} Get axis tree '{Name}' Nodes -> {Count}", RequestSequence, Sid, name, gnodes.Length);
			return await Task.FromResult(gnodes);
		}

		async Task<ActionResult<VarMeta>> GetVarMetaImpl([FromRoute] string name)
		{
			using var wrap = new StateWrap(SessionId, false);
			VarMetaResult vmr = wrap.Engine.GetVarMetaParsed(name);
			if (vmr == null)
			{
				return NotFound(new ErrorResponse(666, $"Variable '{name}' not found"));
			}
			MetaData[] metas = vmr.Metadata.Select(m => new MetaData(m.Name, m.Value)).ToArray();
			var vm = new VarMeta(vmr.RootNodes, metas); // ```` NEEDED?
			Logger.LogInformation(250, "{RequestSequence} {Sid} Get varmeta '{Name}' Nodes -> {Count}", RequestSequence, Sid, name, vm.Nodes.Length);
			return await Task.FromResult(vm);
		}
	}
}
