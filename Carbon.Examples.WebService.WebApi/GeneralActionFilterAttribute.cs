using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using RCS.Carbon.Shared;
using Carbon.Examples.WebService.Common;
using Carbon.Examples.WebService.WebApi.Controllers;

namespace Carbon.Examples.WebService.WebApi
{
    /// <ignore/>
    [ServiceFilter(typeof(ILoggerFactory))]
	public class GeneralActionFilterAttribute : ActionFilterAttribute
	{
		/// <ignore/>
		public const string RequestSequenceItemKey = "count";
		/// <ignore/>
		public const string RequestStartItemKey = "started";
		/// <ignore/>
		public const string EmptySid = "---";

		static int requestSequence = 1000;
		const string HeaderElapsed = "x-service-elapsed";
		readonly ILogger logger;

		/// <ignore/>
		public GeneralActionFilterAttribute(ILoggerFactory logfac)
		{
			//var logfac = (ILoggerFactory)context.HttpContext.RequestServices.GetService(typeof(ILoggerFactory));
			logger = logfac.CreateLogger("FILT");
		}

		/// <ignore/>
		public override void OnActionExecuting(ActionExecutingContext context)
		{
			++requestSequence;
			var req = context.HttpContext.Request;
			context.HttpContext.Items[RequestSequenceItemKey] = requestSequence;
			context.HttpContext.Items[RequestStartItemKey] = DateTime.Now;
			string? sessionId = GetSesssId(req);
			string sid = sessionId?[..3] ?? "---";
			logger.LogDebug("{RequestSequence} {Sid} {Method} {Path}", requestSequence, sid, req.Method, req.Path);
			string method = req.Method;
			string url = req.Path.ToString();
			SessionManager.UpdateActivity(sessionId ?? "-", $"{method} {url}");
			Trce.Log($"{req.Method} {req.Path}");
			base.OnActionExecuting(context);
		}

		/// <ignore/>
		public override void OnResultExecuting(ResultExecutingContext context)
		{
			int requestSequence = -1;
			DateTime started;
			double secs = 0.0;
			if (context.HttpContext.Items.TryGetValue(RequestSequenceItemKey, out object? val1))
			{
				if (val1 is int i)
				{
					requestSequence = i;
				}
			}
			if (context.HttpContext.Items.TryGetValue(RequestStartItemKey, out object? val2))
			{
				if (val2 is DateTime dt)
				{
					started = dt;
					secs = DateTime.Now.Subtract(started).TotalSeconds;
					context.HttpContext.Response.Headers.Add(HeaderElapsed, secs.ToString("F3"));
				}
			}
			string? sessionId = GetSesssId(context.HttpContext.Request);
			string sid = sessionId?[..3] ?? EmptySid;
			int code = 0;
			string? showtext;
			if (context.Result is ObjectResult or)
			{
				object? orval = or.Value;
				if (orval is ErrorResponse er)
				{
					code = er.Code;
					showtext = er.Message;
				}
				else
				{
					showtext = ServiceUtility.NiceObj(orval);
				}
			}
			else if (context.Result is JsonResult jr)
			{
				showtext = ServiceUtility.NiceObj(jr.Value);
			}
			else
			{
				showtext = ServiceUtility.NiceObj(context.Result);
			}
			logger.LogDebug("{RequestSequence} {Sid} {Status} [{Seconds}] {Code} {SampleResponse}", requestSequence, sid, context.HttpContext.Response.StatusCode, secs.ToString("F2"), code, showtext);
			base.OnResultExecuting(context);
		}

		static string? GetSesssId(HttpRequest request)
		{
			if (request.Headers.TryGetValue(CarbonServiceClient.SessionIdHeaderKey, out StringValues values))
			{
				if (values.Count == 1 && values[0]?.Length == SessionController.SessionIdLength)
				{
					return values[0];
				}
			}
			return null;
		}
	}
}
