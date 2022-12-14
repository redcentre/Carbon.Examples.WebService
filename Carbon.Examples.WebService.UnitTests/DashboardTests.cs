using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Carbon.Examples.WebService.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RCS.Azure.Data.Common;

namespace Carbon.Examples.WebService.UnitTests
{
	[TestClass]
	public class DashboardTests : TestBase
	{
		Random rand = new Random();

		[TestMethod]
		public async Task T010_KPI_MultiOxt()
		{
			Sep1("AuthenticateName");
			using var client = MakeClient();
			SessionInfo sinfo = await client.AuthenticateName(TestAccountName, TestAccountPassword);
			Trace($"Login Id {sinfo.Id} Name {sinfo.Name} Session {sinfo.SessionId}");

			Sep1("OpenCloudJob");
			OpenCloudJobResponse jobinfo = await client.OpenCloudJob(CustomerName, JobName);

			Sep1("MultiOxt");
			var multireq = new MultiOxtRequest()
			{
				Filters = new FilterPair[0],// { new FilterPair("North", "Region(1;4)") },
				ReportNames = KPIReporNames,
				TableOnly = true
			};
			Dumpobj(multireq);
			MultiOxtResponse multiresp = await client.MultiOxt(multireq);
			DumpMultiOxtResponse(multiresp);

			await client.CloseJob();
			int count = await client.LogoffSession();
			Trace($"Logoff count = {count}");
		}

		[TestMethod]
		public async Task T020_KPI_MultiOxt_Start()
		{
			Sep1("AuthenticateName");
			using var client = MakeClient();
			SessionInfo sinfo = await client.AuthenticateName(TestAccountName, TestAccountPassword);
			Trace($"Login Id {sinfo.Id} Name {sinfo.Name} Session {sinfo.SessionId}");

			Sep1("OpenCloudJob");
			OpenCloudJobResponse jobinfo = await client.OpenCloudJob(CustomerName, JobName);

			Sep1("MultiOxt");
			var multireq = new MultiOxtRequest()
			{
				Filters = new FilterPair[] { new FilterPair("Quarter", null, true), new FilterPair("Quarter", null, true), new FilterPair("Custom", null, false) },
				ReportNames = KPIReporNames,
				TableOnly = true
			};
			Dumpobj(multireq);
			Guid id = await client.MultiOxtStart(multireq);
			MultiOxtResponse? resp = null;
			while (resp?.Items == null)
			{
				await Task.Delay(350);
				resp = await client.MultiOxtQuery(id);
				Trace($"POLL {resp.ProgressMessage}");
			}
			if (resp.IsCancelled)
			{
				Trace("CANCELLED");
			}
			else
			{
				DumpMultiOxtResponse(resp);
			}

			int count = await client.LogoffSession();
			Trace($"Logoff count = {count}");
		}

		[TestMethod]
		public async Task T030_List_Dashboards()
		{
			using var client = MakeClient();
			SessionInfo sinfo = await client.AuthenticateName(TestAccountName, TestAccountPassword);
			Trace($"Login Id {sinfo.Id} Name {sinfo.Name} Session {sinfo.SessionId}");
			OpenCloudJobResponse jobinfo = await client.OpenCloudJob(CustomerName, JobName);

			var dashlist = await client.ListDashboards(CustomerName, JobName);
			Trace($"List count -> {dashlist.Length}");
			foreach (var dash in dashlist)
			{
				Trace($"| {dash.Name} • {dash.DisplayName} • {dash.Comment}");
			}

			bool closed = await client.CloseJob();
			Trace($"CloseJob -> {closed}");
			int count = await client.LogoffSession();
			Trace($"Logoff count = {count}");
		}

		[TestMethod]
		public async Task T040_KPI_Upsert()
		{
			using var client = MakeClient();
			SessionInfo sinfo = await client.AuthenticateName(TestAccountName, TestAccountPassword);
			Trace($"Login Id {sinfo.Id} Name {sinfo.Name} Session {sinfo.SessionId}");
			OpenCloudJobResponse jobinfo = await client.OpenCloudJob(CustomerName, JobName);

			var dashlist = await client.ListDashboards(CustomerName, JobName);
			Trace($"Pre upsert count -> {dashlist.Length}");
			foreach (var dash in dashlist)
			{
				Trace($"| {dash.Name} • {dash.DisplayName} • {dash.Comment}");
			}

			string dashfile1 = @"K:\u1\Excel\BigInteger factoring speeds.xlsx";
			FileInfo dashinfo = new FileInfo(dashfile1);
			var azdash = new UpsertDashboardRequest()
			{
				CustomerName = CustomerName,
				JobName = JobName,
				UserName = TestAccountName,
				Name = dashfile1,
				DisplayName = "Factoring Speeds",
				CreatedUtc = dashinfo.CreationTimeUtc,
				ModifiedUtc = dashinfo.LastWriteTimeUtc,
				Comment = "This is a comment for the factoring speeds dashboard",
				IsShared = true,
				Buffer = File.ReadAllBytes(dashfile1)
			};
			var updash = await client.UpsertDashboard(azdash);
			updash.Buffer = null;
			Dumpobj(updash);

			dashlist = await client.ListDashboards(CustomerName, JobName);
			Trace($"Post upsert count -> {dashlist.Length}");
			Assert.IsTrue(dashlist.Count(d => d.Name == updash.Name) == 1);

			bool closed = await client.CloseJob();
			Trace($"CloseJob -> {closed}");
			int count = await client.LogoffSession();
			Trace($"Logoff count = {count}");
		}

		static readonly string[] KPIReporNames =
	{
			@"\DashboardSource\Dashboard_KPI\z1_KPI_BrandAwa_Summary",
			@"\DashboardSource\Dashboard_KPI\z2_KPI_BIM",
			@"\DashboardSource\Dashboard_KPI\z3_Funnel_BrandX",
			@"\DashboardSource\Dashboard_KPI\z4_Funnel_BrandY",
			@"\DashboardSource\Dashboard_KPI\z5_Share_BBL",
			@"\DashboardSource\Dashboard_KPI\z6_Seasonal",
			@"\DashboardSource\Dashboard_KPI\z7_RegionalSales",
			@"\DashboardSource\Dashboard_KPI\z8_BrandXProvenRecallMonth",
			@"\DashboardSource\Dashboard_KPI\z9_BrandXProvenRecallQrt"
		};
	}
}