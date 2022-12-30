using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using RCS.Azure.Data.Processor;
using RCS.Carbon.Shared;
using RCS.Licensing.Stdlib;
using Carbon.Examples.WebService.Common;
using System.Collections.Generic;
using System.Linq;

namespace Carbon.Examples.WebService.WebApi.Controllers
{
	/// <ignore/>
	public abstract class ServiceControllerBase : ControllerBase, IDisposable
	{
		/// <ignore/>
		//protected const string LicensingBaseAddress = "https://rcsapps.azurewebsites.net/licensing2/";
		protected const string LicensingBaseAddress = "https://rcsapps.azurewebsites.net/licensing2test/";
		//protected const string LicensingBaseAddress = "http://localhost:52123/";

		/// <summary>
		/// All derived controllers can use this logger service.
		/// </summary>
		protected readonly ILogger Logger;

		/// <summary>
		/// All derived controllers can use this configuration service.
		/// </summary>
		protected readonly IConfiguration Config;

		/// <ignore/>
		public ServiceControllerBase(ILoggerFactory logfac, IConfiguration config)
		{
			Logger = logfac.CreateLogger("WEBC");
			Config = config;
		}

		/// <ignore/>
		public void Dispose()
		{
		}

		/// <summary>
		/// Get the Session Id out of the current request headers. The caller of this property knows that a session
		/// must be started, so a failure to get the value is considered a request failure.
		/// </summary>
		protected string SessionId
		{
			get
			{
				HttpRequest req = HttpContext.Request;
				[DoesNotReturn]
				void Chuck(string message) => throw new CarbonServiceException(1000, $"Header '{CarbonServiceClient.SessionIdHeaderKey}' {message}'. Request {req.Method} {req.Path}. Session ????.");
				if (!HttpContext.Request.Headers.TryGetValue(CarbonServiceClient.SessionIdHeaderKey, out StringValues values))
				{
					Chuck("not found");
				}
				if (values.Count != 1)
				{
					Chuck($"Value count {values.Count} expected 1");
				}
				string? id = values[0];
				if (id == null)
				{
					Chuck("Value is null");
				}
				if (id.Length != SessionController.SessionIdLength)
				{
					Chuck($"Value '{id}' not a session Id");
				}
				return id;
			}
		}

		/// <summary>
		/// Elapsed time since the standard filter attribute detected action starting.
		/// </summary>
		protected double? Secs
		{
			get
			{
				try
				{
					DateTime? start = HttpContext.Items.TryGetValue(GeneralActionFilterAttribute.RequestStartItemKey, out object? value) ? (DateTime?)value : null;
					return start == null ? null : DateTime.Now.Subtract(start.Value).TotalSeconds;
				}
				catch (ObjectDisposedException)
				{
					return null;
				}
			}
		}

		/// <summary>
		/// An abbreviated Session ID slug to help logging.
		/// </summary>
		protected string Sid => SessionId?[..3] ?? GeneralActionFilterAttribute.EmptySid;

		/// <summary>
		/// Attempts to get the request sequence out of the context items.
		/// </summary>
		protected int? RequestSequence =>  GetContextItemInt(GeneralActionFilterAttribute.RequestSequenceItemKey);

		string? GetContextItemString(string key)
		{
			if (!HttpContext.Items.TryGetValue(key, out object? value)) return null;
			if (value is string s) return s;
			return null;
		}

		int? GetContextItemInt(string key)
		{
			if (!HttpContext.Items.TryGetValue(key, out object? value)) return null;
			if (value is int i) return i;
			return null;
		}

		StorageProcessor? _azproc;
		/// <summary>
		/// Lazy reference to a single instance of an RCS Azure data processor.
		/// </summary>
		protected StorageProcessor AzProc => LazyInitializer.EnsureInitialized(ref _azproc, () =>
		{
			var azp = new StorageProcessor(
				Config["SysStorageConnect"],
				Config["Service:ConfigContainerName"],
				Config["Service:ArtefactsContainerName"],
				Config["Service:SessionContainerName"]
			);
			Logger.LogTrace(700, $"Created {azp.GetType().Name}");
			return azp;
		});

		LicensingClient? _lic;
		/// <summary>
		/// Lazy reference to a single instance of a licensing service client.
		/// </summary>
		protected LicensingClient Lic => LazyInitializer.EnsureInitialized(ref _lic, () =>
		{
			var lic = new LicensingClient(
				$"CarbonWebApi/1.0",
				Config["AuthKeys"].Split(",")[0],
				Config["Service:LicensingBaseAddress"],
				Config.GetValue<int>("Service:LicensingTimeout")
			);
			Logger.LogTrace(700, "Created {LicType} {BaseAddr}", lic.GetType().Name, lic.BaseAddress);
			return lic;
		});

		protected static void DumpNodes(IEnumerable<GenNode> nodes)
		{
			foreach (var node in GenNode.WalkNodes(nodes))
			{
				string pfx = string.Join("", Enumerable.Repeat("|  ", node.Level));
				System.Diagnostics.Trace.WriteLine($"@@@@ {pfx}{node}");
			}
		}
	}
}