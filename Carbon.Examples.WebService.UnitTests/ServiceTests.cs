using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Carbon.Examples.WebService.Common;

namespace Carbon.Examples.WebService.UnitTests
{
	[TestClass]
	public class ServiceTests : TestBase
	{
		[TestMethod]
		public async Task T010_Service_Info()
		{
			using var client = MakeClient();
			var info = await client.GetServiceInfo();
			Dumpobj(info);
		}

		[TestMethod]
		public async Task T020_Mock_Error()
		{
			using var client = MakeClient();
			var ex = await Assert.ThrowsExceptionAsync<CarbonServiceException>(() => client.MockError(123));
			Trace(ex.Message);
			Assert.AreEqual("This is a fake error for request number 123", ex.Message);
		}

		[TestMethod]
		public async Task T030_Bad_Service_Uri()
		{
			using var client = new CarbonServiceClient("http://notexist.com/carbon", "Client 1.0");
			var ex = await Assert.ThrowsExceptionAsync<CarbonServiceException>(() => client.GetServiceInfo());
			Trace(ex.Message);
			Assert.AreEqual("The GET response from 'http://notexist.com/carbon/service/info' status NotFound is not in a recognised format. The address may be incorrect or the service is faulting.", ex.Message);
		}
	}
}