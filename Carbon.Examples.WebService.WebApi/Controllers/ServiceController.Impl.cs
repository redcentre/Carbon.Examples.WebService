using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RCS.Carbon.Variables;
using Carbon.Examples.WebService.Common;

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
	}
}
