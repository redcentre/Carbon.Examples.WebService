using System.Threading.Tasks;
using RCS.Azure.Data.Common;
using Carbon.Examples.WebService.Common;

namespace Carbon.Examples.WebService.WebApi.Controllers
{
    partial class DashboardController
	{
		async Task<AzDashboard[]> ListDashboardsImpl(string customerName, string jobName)
		{
			return await AzProc.ListDashboardsAsync(customerName, jobName);
		}

		async Task<AzDashboard> GetDashboardImpl(DashboardRequest request)
		{
			return await AzProc.GetDashboardAsync(request.CustomerName, request.JobName, request.DashboardName);
		}

		async Task<bool> DeleteDashboardImpl(DashboardRequest request)
		{
			return await AzProc.DeleteDashboardAsync(request.CustomerName, request.JobName, request.DashboardName);
		}

		async Task<AzDashboard> UpsertDashboardImpl(UpsertDashboardRequest request)
		{
			return await AzProc.UpsertDashboardAsync(request);
		}
	}
}
