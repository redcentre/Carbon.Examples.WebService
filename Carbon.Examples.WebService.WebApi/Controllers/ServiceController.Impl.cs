using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Carbon.Examples.WebService.Common;
using Microsoft.AspNetCore.Mvc;
using RCS.Carbon.Shared;
using RCS.Carbon.Variables;

namespace Carbon.Examples.WebService.WebApi.Controllers
{
	partial class ServiceController
	{
		async Task<ActionResult<int>> MockErrorImpl(int number)
		{
			if (number == Guid.NewGuid().GetHashCode())
			{
				return await Task.FromResult(number);	// Buy a lottery ticket if this happens!
			}
			throw new Exception($"This is a fake error for request number {number}");
		}

		async Task<ActionResult<ServiceInfo>> GetServiceInfoImpl()
		{
			var asm = typeof(Program).Assembly;
			var an = asm.GetName();
			var casm = typeof(Licence).Assembly;
			var can = casm.GetName();
			var info = new ServiceInfo()
			{
				Version = an.Version!.ToString(),
				Build = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
				FileVersion = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version,
				CarbonVersion = can.Version!.ToString(),
				CarbonFileVersion = casm.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version,
				CarbonBuild = casm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
				Copyright = asm.GetCustomAttribute<AssemblyCopyrightAttribute>()!.Copyright,
				Company = asm.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company,
				Product = asm.GetCustomAttribute<AssemblyProductAttribute>()!.Product,
				Title = asm.GetCustomAttribute<AssemblyTitleAttribute>()!.Title,
				Description = asm.GetCustomAttribute<AssemblyDescriptionAttribute>()!.Description,
				HostMachine = Environment.MachineName,
				HostAccount = Environment.UserName,
				TempFolder = Path.GetTempPath(),
				LicensingBaseAddress = Config["Service:LicensingBaseAddress"]
			};
			return await Task.FromResult(info);
		}

		async Task<ActionResult<bool>> StartLogImpl()
		{
			VarLib.StartLog();
			return await Task.FromResult(true);
		}

		async Task<ActionResult<bool>> EndLogImpl()
		{
			VarLib.EndLog();
			return await Task.FromResult(true);
		}

		async Task<ActionResult<bool>> ClearLogImpl()
		{
			VarLib.ClearLog();
			return await Task.FromResult(true);
		}

		async Task<ActionResult<string>> ListLogImpl()
		{
			string body = VarLib.GetLog();
			return await Task.FromResult(body);
		}
	}
}
