using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Carbon.Examples.WebService.Common;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using RCS.Carbon.Shared;
using RCS.Carbon.Variables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System.Drawing;
using System.Collections.Generic;
using System.Diagnostics;
using RCS.Carbon.Tables;

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

		async Task<ActionResult<string[]>> ReadTiming1Impl(ReadTimingRequest1 request)
		{
			var watch = new Stopwatch();
			var lines = new List<string>();
			var svc = new BlobServiceClient(request.AzConnect);
			var cc = svc.GetBlobContainerClient(request.Container);
			string[] names = request.Names.Split(",;".ToCharArray());
			foreach (string name in names)
			{
				watch.Restart();
				try
				{
					int lineCount = 0;
					int charCount = 0;
					var bc = cc.GetBlockBlobClient(name);
					var props = bc.GetProperties();
					using (var reader = new StreamReader(bc.OpenRead()))
					{
						while (!reader.EndOfStream)
						{
							++lineCount;
							string? line = reader.ReadLine();
							charCount += line!.Length;
						}
					}
					double kb = props.Value.ContentLength / 1024.0;
					lines.Add($"{name} ({kb:F1} KB) -> {lineCount} lines {charCount} chars [{watch.Elapsed.TotalSeconds:F3}]");
				}
				catch (Exception ex)
				{
					lines.Add($"{name} -> {ex.Message}");
				}
			}
			return await Task.FromResult(lines.ToArray());
		}

		async Task<ActionResult<string[]>> ReadTiming2Impl(ReadTimingRequest2 request)
		{
			var engine = new CrossTabEngine();
			await engine.LoginId("G1234567", "37Reddot2");
			string[] lines = engine.LoadTest(request.Customer, request.Job, request.Vars, request.Count);
			return lines;
		}
	}
}
