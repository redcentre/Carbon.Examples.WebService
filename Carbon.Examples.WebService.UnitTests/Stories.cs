#define FAILS

using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RCS.Carbon.Shared;
using Carbon.Examples.WebService.Common;

namespace Carbon.Examples.WebService.UnitTests
{
    [TestClass]
	public class Stories : TestBase
	{
		[TestMethod]
		public async Task T100_Big_Story()
		{
			CarbonServiceException pex;
			ArgumentNullException anex;
			OpenCloudJobResponse resp;

			using var client = MakeClient();
#if FAILS
			Sep1("Bad Id");
			pex = await Assert.ThrowsExceptionAsync<CarbonServiceException>(() => client.LoginId("NOUSER", "BADPASS"));
			Trace(pex.Message);
			Assert.AreEqual("User Id 'NOUSER' not found", pex.Message);

			Sep1("Null Id");
			anex = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => client.LoginId(null, null));
			Trace(anex.Message);
			Assert.AreEqual("Value cannot be null. (Parameter 'id')", anex.Message);

			Sep1("Null Pass");
			anex = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => client.LoginId(TestAccountId, null));
			Trace(anex.Message);
			Assert.AreEqual("Value cannot be null. (Parameter 'password')", anex.Message);

			Sep1("Bad Id");
			pex = await Assert.ThrowsExceptionAsync<CarbonServiceException>(() => client.LoginId(TestAccountId, "BADPASS"));
			Trace(pex.Message);
			Assert.AreEqual("User '" + TestAccountName + "' Id '" + TestAccountId + "' incorrect password", pex.Message);
#endif
			Sep1("LoginId");
			SessionInfo sessinfo = await client.LoginId(TestAccountId, TestAccountPassword);
			Assert.IsNotNull(sessinfo);
			DumpSessinfo(sessinfo);

			Sep1("List Sessions");
			var sesslist = await client.ListSessions();
			Assert.IsTrue(sesslist.Length > 0);
			Trace($"Session list count -> {sesslist.Length}");
			var sess = sesslist.First(x => x.SessionId == sessinfo.SessionId);
			Assert.IsNotNull(sess);
			Dumpobj(sess);
#if FAILS
			Sep1("OpenCloudJob null cust");
			anex = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => client.OpenCloudJob(null, null));
			Trace(anex.Message);
			Assert.AreEqual("Value cannot be null. (Parameter 'customerName')", anex.Message);

			Sep1("OpenCloudJob null job");
			anex = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => client.OpenCloudJob(CustomerName, null));
			Trace(anex.Message);
			Assert.AreEqual("Value cannot be null. (Parameter 'jobName')", anex.Message);

			Sep1("OpenCloudJob blank cust");
			pex = await Assert.ThrowsExceptionAsync<CarbonServiceException>(() => client.OpenCloudJob("", JobName));
			Trace(pex.Message);
			Assert.AreEqual("Open cloud Customer '' Job 'demo' failed - Customer '' is not a registered storage account", pex.Message);

			Sep1("OpenCloudJob blank job");
			pex = await Assert.ThrowsExceptionAsync<CarbonServiceException>(() => client.OpenCloudJob(CustomerName, ""));
			Trace(pex.Message);
			Assert.AreEqual("Open cloud Customer 'client1rcs' Job '' failed - Job '' is not accessible in customer 'client1rcs'.", pex.Message);
#endif
			Sep1("OpenCloudJob");
			resp = await client.OpenCloudJob(CustomerName, JobName, true, true, true, JobTocType.ExecUser, true, true);
			Assert.IsNotNull(resp);
			Assert.IsNotNull(resp.Toc);
			Assert.IsNotNull(resp.DProps);
			Assert.IsNotNull(resp.VartreeNames);
			Assert.IsNotNull(resp.AxisTreeNames);
			Assert.IsNotNull(resp.JobIni);
			Trace($"TOC new {resp.Toc.Length} • Vartree {resp.VartreeNames.Length} • Axis {resp.AxisTreeNames.Length} • INI {resp.JobIni.Length} • DProps format {resp.DProps.Output.Format}");

			Sep1("Vartree List");
			string[] vtnames = await client.ListVartrees();
			Assert.IsTrue(vtnames.Length > 0);
			Dumpobj(vtnames);
#if FAILS
			Sep1("Vartree not found");
			pex = await Assert.ThrowsExceptionAsync<CarbonServiceException>(() => client.VartreeAsNodes("NOVARTREE"));
			Trace(pex.Message);
			Assert.AreEqual("Blob 'NOVARTREE.vtr' does not exist", pex.Message);
#endif
			Sep1("Vartree");
			GenNode[] vtroots = await client.VartreeAsNodes("VarTree");
			Assert.IsTrue(vtroots.Length > 0);
			Trace($"Vartree root nodes -> {vtroots.Length}");
			DumpNodes(vtroots, 12);
			Assert.AreEqual("Case", vtroots[0].Children[0].Name);

			Sep1("Axis Tree List");
			string[] axnames = await client.ListAxisTrees();
			Assert.IsTrue(axnames.Length > 0);
			Dumpobj(axnames);
#if FAILS
			Sep1("Axis not found");
			pex = await Assert.ThrowsExceptionAsync<CarbonServiceException>(() => client.AxisTreeAsNodes("NOAXIS"));
			Trace(pex.Message);
			Assert.AreEqual("Blob 'NOAXIS.atr' does not exist", pex.Message);
#endif
			Sep1("Axis Tree");
			GenNode[] axroots = await client.AxisTreeAsNodes("Test");
			Assert.IsTrue(axroots.Length > 0);
			Trace($"Axis tree root nodes -> {axroots.Length}");
			DumpNodes(axroots, 12);
			Assert.AreEqual("Banner1", axroots[0].Children[0].Name);
#if FAILS
			Sep1("Axis not found");
			pex = await Assert.ThrowsExceptionAsync<CarbonServiceException>(() => client.GetVarMeta("NOVAR"));
			Trace(pex.Message);
			Assert.AreEqual(@"Blob 'CaseData\novar.met' does not exist", pex.Message);
#endif
			Sep1("Varmeta Age");
			VarMeta vmage = await client.GetVarMeta("Age");
			Assert.IsNotNull(vmage);
			foreach (var meta in vmage.Metadata) Trace($"META {meta.Key}={meta.Value}");
			Trace($"Age root nodes -> {vmage.Nodes.Length}");
			DumpNodes(vmage.Nodes, 12);
			Assert.AreEqual("15-25", vmage.Nodes[0].Children[0].Description);

			Sep1("Varmeta BIM");
			VarMeta vmbim = await client.GetVarMeta("BIM");
			Assert.IsNotNull(vmbim);
			Assert.IsNotNull(vmbim.Metadata);
			Assert.IsNotNull(vmbim.Nodes);
			foreach (var meta in vmage.Metadata) Trace($"META {meta.Key}={meta.Value}");
			Trace($"BIM root nodes -> {vmbim.Nodes.Length}");
			DumpNodes(vmbim.Nodes, 12);
			Assert.AreEqual("BrandX", vmbim.Nodes[0].Children[0].Description);

			Sep1("TOC Simple");
			GenNode[] tocroots = await client.ListSimpleToc(true);
			Trace($"TOC Simple root nodes -> {tocroots.Length}");
			DumpNodes(tocroots, 12);

			Sep1("TOC ExecUser");
			tocroots = await client.ListExecUserToc(true);
			Trace($"TOC ExecUser root nodes -> {tocroots.Length}");
			DumpNodes(tocroots, 12);

			Sep1("TOC Full");
			tocroots = await client.ListFullToc(true);
			Trace($"TOC Full root nodes -> {tocroots.Length}");
			DumpNodes(tocroots, 12);

			//Sep1("TOC (legacy)");
			//GenNode[] tocoldroots = await client.ListLegacyToc();
			//Trace($"TOC legacy root nodes -> {tocoldroots.Length}");
			//DumpNodes(tocoldroots, 12);
			//var trknode = GenNode.WalkNodes(tocoldroots).FirstOrDefault(n => n.Type == "Table");
			//Assert.IsNotNull(trknode);
			//string name = $"{trknode.Description}/{trknode.Name}.rpt";
			//string[] trklines = await client.ReadFileAsLines(name);
			//Assert.IsNotNull(trklines);
			//Sep1($"Dump {name} ({trklines.Length})");
			//DumpLines(trklines, 12);


			Sep1("GenTab Age x Region TSV (default)");
			var sprops = new XSpecProperties();
			var dprops = new XDisplayProperties();
			dprops.Output.Format = XOutputFormat.TSV;
#if FAILS
			//string[] linesx = await client.GenTab(null, "FOO", "BAR", null, null, sprops, dprops);
			// Weird crash send to RS
#endif
			string[] lines = await client.GenTab(null, "Age", "Region", null, null, sprops, dprops);
			DumpLines(lines);
			Assert.IsTrue(lines[3].StartsWith("\t15-25"));
			Assert.IsTrue(lines[4].StartsWith("NE\t479"));
			Assert.IsTrue(lines[5].StartsWith("\t24.82%"));
			Assert.IsTrue(lines[6].StartsWith("\t18.99%"));
			var dprops2 = await client.GetProps();
			Assert.AreEqual(XOutputFormat.TSV, dprops2.Output.Format);
			Assert.IsTrue(dprops2.Cells.RowPercents.Visible);
			Assert.IsTrue(dprops2.Cells.ColumnPercents.Visible);

			Sep1("GenTab Age x Region TSV (freq only)");
			dprops.Cells.RowPercents.Visible = false;
			dprops.Cells.ColumnPercents.Visible = false;
			lines = await client.GenTab(null, "Age", "Region", null, null, sprops, dprops);
			DumpLines(lines);
			Assert.IsTrue(lines[3].StartsWith("\t15-25"));
			Assert.IsTrue(lines[4].StartsWith("NE\t479"));
			Assert.IsTrue(lines[5].StartsWith("SE\t459"));
			Assert.IsTrue(lines[6].StartsWith("SW\t523"));
			dprops2 = await client.GetProps();
			Assert.AreEqual(XOutputFormat.TSV, dprops2.Output.Format);
			Assert.IsFalse(dprops2.Cells.RowPercents.Visible);
			Assert.IsFalse(dprops2.Cells.ColumnPercents.Visible);

			Sep1("Reformat as TSV");
			dprops2.Columns.Groups.Visible = false;
			dprops2.Columns.Labels.Visible = true;
			dprops2.Rows.Groups.Visible = false;
			dprops2.Rows.Labels.Visible = true;
			dprops2.Output.Format = XOutputFormat.SSV;
			lines = await client.ReformatTable(dprops2);
			DumpLines(lines);
			Assert.IsTrue(Regex.IsMatch(lines[3], @"^\s+\+------\+"));
			Assert.IsTrue(Regex.IsMatch(lines[4], @"^\s+\|15-25 \|"));
			Assert.IsTrue(Regex.IsMatch(lines[5], @"^\s+\|      \|"));
			Assert.IsTrue(Regex.IsMatch(lines[6], @"^-+\+------\+"));
			Assert.IsTrue(Regex.IsMatch(lines[7], @"^NE\s+\|479   \|"));

			Sep1("Pandas input");
			var postdata = new
			{
				top = new string[] { "Female", "Male", "Male", "Male", "Male", "Female", "Female", "Male", "Male", "Female" },
				side = new double[] { 30, 64, 30, 18, 30, 64, 30, 79, 64, 19 }
			};
			string json = JsonSerializer.Serialize(postdata);
			Trace(json);
			Sep1("Pandas output");
			string pandas = await client.PandasDataframe(json);
			Trace(NiceJson(pandas));

			Sep1("CloseJob");
			bool ended = await client.CloseJob();
			Assert.IsTrue(ended);
			Trace($"ReturnClose job -> {ended}");

			Sep1("ReturnSession");
			int count = await client.ReturnSession();
			Trace($"Return session -> {count}");
		}
	}
}