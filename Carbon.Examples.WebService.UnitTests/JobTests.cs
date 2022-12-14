using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Carbon.Examples.WebService.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RCS.Carbon.Shared;

namespace Carbon.Examples.WebService.UnitTests
{
	[TestClass]
	public class JobTests : TestBase
	{
		[TestMethod]
		public async Task T010_Pandas_Dataframe()
		{
			using var client = MakeClient();
			var postdata = new
			{
				top = new string[] { "Female", "Male", "Male", "Male", "Male", "Female", "Female", "Male", "Male", "Female" },
				side = new double[] { 30, 64, 30, 18, 30, 64, 30, 79, 64, 19 },
				props = "Decimals.Frequencies=2"
			};
			string json = JsonSerializer.Serialize(postdata);
			Trace(json);
			Sep1("Pandas output");
			string pandas = await client.PandasDataframe(json);
			Trace(pandas);
			Trace(NiceJson(pandas));
		}

		[TestMethod]
		public async Task T020_Pandas_Output_Formats()
		{
			using var client = MakeClient();
			SessionInfo sessinfo = await client.LoginId(TestAccountId, TestAccountPassword);
			Trace($"LoginId {sessinfo.SessionId}");
			Assert.IsNotNull(sessinfo);

			var resp = await client.OpenCloudJob(CustomerName, JobName, true, false, false, JobTocType.ExecUser, false, false);
			Assert.IsNotNull(resp);
			Assert.IsNotNull(resp.DProps);

			var sprops = new XSpecProperties();
			resp.DProps.Output.Format = XOutputFormat.CSV;
			string[] lines = await client.GenTab(null, "Age", "Region", null, null, sprops, resp.DProps);
			//DumpLines(lines);

			Sep1("Pandas 1");
			string json = await client.GenTabPandas1();
			Trace(json);
			Trace(NiceJson(json));

			Sep1("Pandas 2");
			json = await client.GenTabPandas2();
			Trace(json);
			Trace(NiceJson(json));

			bool closed = await client.CloseJob();
			Assert.IsTrue(closed);

			int count = await client.ReturnSession();
			Trace($"Return session -> {count}");
		}

		[TestMethod]
		public async Task T030_Reformat()
		{
			using var client = MakeClient();
			SessionInfo sessinfo = await client.LoginId(TestAccountId, TestAccountPassword);
			Trace($"LoginId {sessinfo.SessionId}");
			Assert.IsNotNull(sessinfo);

			var resp = await client.OpenCloudJob(CustomerName, JobName, true, false, false, JobTocType.ExecUser, false, false);
			Assert.IsNotNull(resp);
			Assert.IsNotNull(resp.DProps);

			Sep1("GenTab Age x Region CSV");
			var sprops = new XSpecProperties();
			resp.DProps.Output.Format = XOutputFormat.CSV;
			string[] lines1 = await client.GenTab(null, "Age", "Region", null, null, sprops, resp.DProps);
			DumpLines(lines1);

			Sep1("Reformat TSV");
			resp.DProps.Output.Format = XOutputFormat.TSV;
			string[] lines2 = await client.ReformatTable(resp.DProps);
			DumpLines(lines2);

			bool closed = await client.CloseJob();
			Assert.IsTrue(closed);

			int count = await client.ReturnSession();
			Trace($"Return session -> {count}");
		}

		[TestMethod]
		public async Task T200_Gentab()
		{
			using var client = MakeClient();
			SessionInfo sessinfo = await client.StartSessionFree("Unit Tests");
			Trace($"StartSessionFree {sessinfo.SessionId}");
			OpenCloudJobResponse jobresp = await client.OpenCloudJob("rcsruby", "demo");
			Trace($"OpenCloudJob {jobresp.DProps} {jobresp.JobIni} {jobresp.VartreeNames} {jobresp.AxisTreeNames}");
			var sprops = new XSpecProperties();
			var dprops = new XDisplayProperties();
			dprops.Output.Format = XOutputFormat.TSV;
			string[] lines = await client.GenTab(null, "Age", "Region", null, null, sprops, dprops);
			DumpLines(lines);
			bool closed = await client.CloseJob();
			Trace($"Closed -> {closed}");
		}

		[TestMethod]
		public async Task T220_Save_Report()
		{
			using var client = MakeClient();
			await client.LoginId(TestAccountId, TestAccountPassword);
			OpenCloudJobResponse jobresp = await client.OpenCloudJob(CustomerName, JobName, tocType: JobTocType.ExecUser);
			Sep1("Open job toc");
			DumpToc(jobresp.Toc!);
			Sep1("ListExecUserToc");
			GenNode[] toc = await client.ListExecUserToc(true);
			DumpToc(toc);

			var sprops = new XSpecProperties();
			var dprops = new XDisplayProperties();
			dprops.Output.Format = XOutputFormat.TSV;
			string[] lines = await client.GenTab(null, "Age", "Region", null, null, sprops, dprops);
			Trace($"Report lines -> {lines.Length}");
			GenericResponse gr = await client.SaveReport("Age x Region", null);
			Trace($"Save 1 code -> {gr.Code}");

			Sep1("After save 1 toc");
			toc = await client.ListExecUserToc(true);
			DumpToc(toc);

			dprops.Output.Format = XOutputFormat.CSV;
			lines = await client.GenTab(null, "Gender", "Occupation", null, null, sprops, dprops);
			Trace($"Report lines -> {lines.Length}");
			gr = await client.SaveReport("Gender x Occupation", "SubFolderB");
			Trace($"Save 2 code -> {gr.Code}");

			Sep1("After save 2 toc");
			toc = await client.ListExecUserToc(true);
			DumpToc(toc);

			bool closed = await client.CloseJob();
			Trace($"Closed -> {closed}");
		}

		[TestMethod]
		public async Task T300_Free_Customers()
		{
			using var client = MakeClient();
			SessionInfo info = await client.StartSessionFree("Unit Tests", true);
			Dumpobj(info);
		}

		[TestMethod]
		public async Task T400_SingleXlsx()
		{
			using var client = MakeClient();
			SessionInfo info = await client.LoginId(TestAccountId, TestAccountPassword);
			Trace($"Session -> {info.SessionId}");
			//Dumpobj(info);
			OpenCloudJobResponse jobresp = await client.OpenCloudJob(CustomerName, JobName, false, true, true, JobTocType.ExecUser, false, false);
			//Dumpobj(jobresp);
			DumpToc(jobresp.Toc!);
			var rptnode = GenNode.WalkNodes(jobresp.Toc).First(n => n.Type == "Table");
			Trace($"Report node -> {rptnode}");
			string rptname = "Tables/Exec/FolderA/AgeReg.cbt";
			var loadreq = new LoadReportRequest(rptname);
			await client.LoadReport(loadreq);
			var resp = await client.GenerateXlsx();
			Dumpobj(resp);
			string book1 = MakeTempFile("T400-book1.xlsx");
			long length1 = await DownloadUrlToFile(resp.ExcelUri, book1);
			Trace($"Download {book1} ({length1})");

			var req = new QuickUpdateRequest()
			{
				ShowFreq = true,
				ShowColPct = true,
				ShowRowPct = true,
				ShowSig = true,
				Filter = "Region(1)"
			};
			var resp2 = await client.QuickUpdateReport(req);
			Dumpobj(resp2);
			string book2 = MakeTempFile("T400-book2.xlsx");
			long length2 = await DownloadUrlToFile(resp.ExcelUri, book2);
			Trace($"Download {book2} ({length2})");
			Assert.AreNotEqual(length1, length2);

			req.Filter = null;
			var resp3 = await client.QuickUpdateReport(req);
			Dumpobj(resp3);
			string book3 = MakeTempFile("T400-book3.xlsx");
			long length3 = await DownloadUrlToFile(resp.ExcelUri, book3);
			Trace($"Download {book3} ({length3})");

			//var filtreq = new FilterRequest("GEN(1)");
			//var respfilt = await client.Filter(filtreq);

			bool closed = await client.CloseJob();
			Trace($"Closed -> {closed}");
			int count = await client.LogoffSession();
			Trace($"LogoffSession -> {count}");
		}

		[TestMethod]
		public async Task T500_Delete_Toc()
		{
			using var client = MakeClient();
			SessionInfo info = await client.LoginId(TestAccountId, TestAccountPassword);
			Trace($"Session -> {info.SessionId}");
			OpenCloudJobResponse jobresp = await client.OpenCloudJob(CustomerName, JobName, false, true, true, JobTocType.ExecUser, false, false);
			DumpToc(jobresp.Toc!);
			bool closed = await client.CloseJob();
			Trace($"Closed -> {closed}");
		}

	}
}