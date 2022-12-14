using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RCS.Carbon.Shared;
using Carbon.Examples.WebService.Common;
using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using System.Threading;
using System.Net.Http.Headers;

namespace Carbon.Examples.WebService.UnitTests
{
	public class TestBase
	{
		// ╔═══════════════════════════════════════════════════════════════════╗
		// ║  The following values must be defined as environment variables    ║
		// ║  by each Windows user who is running the Carbon service tests.    ║
		// ╚═══════════════════════════════════════════════════════════════════╝
		protected readonly string TestAccountId = Environment.GetEnvironmentVariable("RCSTESTID")!;
		protected readonly string TestAccountName = Environment.GetEnvironmentVariable("RCSTESTNAME")!;
		protected readonly string TestAccountPassword = Environment.GetEnvironmentVariable("RCSTESTPASS")!;

		// ╔═══════════════════════════════════════════════════════════════════╗
		// ║  The following customer and job names are being used for testing, ║
		// ║  but this can change and it should be done here.                  ║
		// ╚═══════════════════════════════════════════════════════════════════╝
		protected const string CustomerName = "client1rcs";
		protected const string JobName = "demo";

		// ╔═══════════════════════════════════════════════════════════════════╗
		// ║  Change the service base address for testing in the debugger      ║
		// ║  or at the published public address.                              ║
		// ╚═══════════════════════════════════════════════════════════════════╝
		protected const string BaseUri = "http://localhost:5086/";
		//protected const string BaseUri = "http://rcsapps.azurewebsites.net/carbon/";

		protected readonly JsonSerializerOptions Jopts = new JsonSerializerOptions() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

		public TestContext TestContext { get; set; }

		protected CarbonServiceClient MakeClient()
		{
			var client = new CarbonServiceClient(BaseUri, "Test 1.0");
			Trace($"MakeClient -> {client.BaseAddress}");
			return client;
		}

		protected void Dumpobj(object value)
		{
			if (value == null)
			{
				TestContext.WriteLine("NULL");
				return;
			}
			string json = JsonSerializer.Serialize(value, new JsonSerializerOptions() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
			TestContext.WriteLine(json);
		}

		protected void Sep1(string? title = null)
		{
			if (title != null)
			{
				int len = title.Length + 4;
				Trace("┌" + new string('─', len) + "┐");
				Trace("│  " + title + "  │");
				Trace("└" + new string('─', len) + "┘");
			}
		}

		protected void DumpSessinfo(SessionInfo? sessinfo)
		{
			if (sessinfo == null)
			{
				Trace($"SessionInfo NULL");
				return;
			}
			Trace($"SessionId ..... {sessinfo.SessionId}");
			Trace($"Id ............ {sessinfo.Id}");
			Trace($"Name .......... {sessinfo.Name}");
			Trace($"Email ......... {sessinfo.Email}");
			Trace($"Roles ......... {string.Join(" + ", sessinfo.Roles!)}");
			foreach (var cust in sessinfo.SessionCusts!)
			{
				Trace($"|  {cust.Id} {cust.Name}");
				foreach (var job in cust.SessionJobs!)
				{
					string vtjoin = string.Join(" + ", job.VartreeNames!);
					Trace($"|  |  {job.Id} {job.Name} • {vtjoin}");
				}
			}
		}

		protected void DumpMultiOxtResponse(MultiOxtResponse multiresp)
		{
			Trace($"MultiOXT Id ............. {multiresp.Id}");
			Trace($"MultiOXT Created ........ {multiresp.Created}");
			Trace($"MultiOXT IsCancelled .... {multiresp.IsCancelled}");
			foreach (var item in multiresp.Items)
			{
				if (item.ErrorType != null)
				{
					Trace($"\u2588 {item.ReportName} -> {item.ErrorType} {item.ErrorMessage}");
				}
				else
				{
					Trace($"\u2588 {item.ReportName} Lines={item.OxtLines.Length} DispColLetters={item.DispColLetters} DispRowLetters={item.DispRowLetters} SigShowLetters={item.SigShowLetters} TitlesRowCount={item.Titles_RowCount} [{item.Seconds:F2}]");
					foreach (string line in item.OxtLines)
					{
						Trace($"| {line}");
					}
				}
			}
		}

		protected string? Join(IEnumerable<string>? parts)
		{
			if (parts == null) return "NULL";
			return "[" + string.Join(",", parts) + "]";
		}

		protected void DumpNodes(IEnumerable<GenNode> roots, int max = int.MaxValue)
		{
			foreach (var node in GenNode.WalkNodes(roots).Take(max))
			{
				string pfx = string.Join("", Enumerable.Repeat("│  ", node.Level));
				Trace($"{pfx}{node}");
			}
		}

		protected string NiceJson(string json)
		{
			var doc = JsonDocument.Parse(json);
			return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
		}

		protected void DumpLines(IEnumerable<string> lines, int max = int.MaxValue)
		{
			foreach (string line in lines.Take(max)) Trace($"║ {line}");
		}

		protected void DumpToc(GenNode[] roots)
		{
			foreach (var node in GenNode.WalkNodes(roots))
			{
				string pfx = string.Join("", Enumerable.Repeat("|  ", node.Level));
				Trace($"{pfx}{node}");
			}
		}

		protected void Trace(string message) => TestContext.WriteLine(message);

		protected async Task<long> DownloadUrlToFile(string url, string filename)
		{
			using var http = new HttpClient();
			var stream1 = await http.GetStreamAsync(url);
			using var writer = new FileStream(filename, FileMode.Create, FileAccess.Write);
			await stream1.CopyToAsync(writer);
			return writer.Position;
		}

		protected static string MakeTempFile(string name) => Path.Combine(Path.GetTempPath(), name);
	}
}