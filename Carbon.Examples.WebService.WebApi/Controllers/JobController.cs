﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Carbon.Examples.WebService.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RCS.Carbon.Export;
using RCS.Carbon.Import;
using RCS.Carbon.Shared;
using RCS.RubyCloud.WebService;
using TSAPI.Public.Domain.Interviews;
using TSAPI.Public.Domain.Metadata;
using TSAPI.Public.Queries;

namespace Carbon.Examples.WebService.WebApi.Controllers;

partial class JobController
{
	#region Job

	async Task<ActionResult<OpenCloudJobResponse>> OpenCloudJobImpl(OpenCloudJobRequest request)
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		try
		{
			wrap.Engine.OpenJob(request.CustomerName, request.JobName);
			if (request.VartreeName != null)
			{
				wrap.Engine.SetTreeNames(request.VartreeName);
			}
		}
		catch (CarbonException ex)
		{
			return BadRequest(new ErrorResponse(201, $"Open cloud Customer '{request.CustomerName}' Job '{request.JobName}' Vartree '{request.VartreeName}' failed - {ex.Message}"));
		}
		SessionManager.SetCustomerJob(SessionId, request.CustomerName, request.JobName, request.VartreeName);
		// Some of the following information could be returned by a Carbon OpenJob call, which would reduce the traffic
		// of getting it now in chatty calls. As an experiment the vartree names are returned from the open.
		var dprops = request.GetDisplayProps ? wrap.Engine.GetProps() : null;
		string[]? vtnames = request.GetVartreeNames ? wrap.Engine.Job.ListVartreeNames().ToArray() : null;
		string[]? axnames = request.GetAxisTreeNames ? wrap.Engine.Job.GetAxisNames().ToArray() : null;
		GenNode[]? drillFilters = request.GetDrills ? wrap.Engine.DrillFiltersAsNodes() : null;
		GenNode[]? tocnodes = null;
		if (request.TocType != JobTocType.None)
		{
			if (request.TocType == JobTocType.Simple)
			{
				tocnodes = wrap.Engine.SimpleTOCGenNodes();
			}
			else if (request.TocType == JobTocType.ExecUser)
			{
				tocnodes = wrap.Engine.ExecUserTOCGenNodes();
			}
		}
		var response = new OpenCloudJobResponse(dprops, vtnames, axnames, tocnodes, drillFilters)
		{
			ShowCaseFilter = wrap.Engine.Job.JobINI.ShowCaseFilter,
			ShowAxisLocks = wrap.Engine.Job.ShowAxisLocks,
			TreesDescOnly = wrap.Engine.Job.TreesDescOnly,
			Cases = wrap.Engine.Job.JobINI.Cases,
			TryAzureTemp = wrap.Engine.Job.JobINI.TryAzureTemp
		};
		LogInfo(200, "Open {CustomerName} Job {JobName} {DProps} {VtCount} {AxCount} {TocNewCount}", request.CustomerName, request.JobName, dprops, response.VartreeNames?.Length, axnames?.Length, tocnodes?.Length);
		return await Task.FromResult(response);
	}

	async Task<ActionResult<bool>> CloseJobImpl()
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		bool closed = wrap.Engine.CloseJob();
		SessionManager.SetCustomerJob(SessionId, null, null, null);
		return await Task.FromResult(closed);
	}

	async Task<ActionResult<string[]>> ReadFileAsLinesImpl(ReadFileRequest request)
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		string[] lines = wrap.Engine.ReadFileLines(request.Name).ToArray();
		LogInfo(210, "Set props");
		return await Task.FromResult(lines);
	}

	#endregion

	#region TOC

	async Task<ActionResult<GenNode[]>> ListSimpleTocImpl(bool load)
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		GenNode[] gnodes = wrap.Engine.SimpleTOCGenNodes();
		LogInfo(212, "List simple TOC Nodes {Load} -> {Count}", RequestSequence, Sid, load, gnodes.Length);
		return await Task.FromResult(gnodes);
	}

	async Task<ActionResult<GenNode[]>> ListFullTocImpl(bool load)
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		GenNode[] gnodes = wrap.Engine.FullTOCGenNodes();
		LogInfo(214, "List full TOC Nodes {Load} -> {Count}", RequestSequence, Sid, load, gnodes.Length);
		return await Task.FromResult(gnodes);
	}

	async Task<ActionResult<GenNode[]>> ListExecUserTocImpl(bool load)
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		GenNode[] gnodes = wrap.Engine.ExecUserTOCGenNodes();
		LogInfo(216, "List ExecUser TOC Nodes {Load} -> {Count}", RequestSequence, Sid, load, gnodes.Length);
		return await Task.FromResult(gnodes);
	}

	async Task<ActionResult<GenericResponse>> DeleteInUserTocImpl(DeleteInUserTocRequest request)
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		bool success = wrap.Engine.DeleteInUserTOC(request.Name, true, out string message);
		return await Task.FromResult(new GenericResponse(success ? 0 : 1, message));
	}

	#endregion

	#region Report

	async Task<ActionResult<string[]>> GenTabImpl(GenTabRequest request)
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		string[] lines;
		if (request.DProps.Output.Format == XOutputFormat.XLSX)
		{
			// XLSX Excel output is a special case and does not return a typical set of report lines.
			// It natively generates the buffer of an XLSX workbook, which is converted to a single
			// base64 encoded string line for return.
			byte[] buff = wrap.Engine.GenTabAsXLSX(request.Name, request.Top, request.Side, request.Filter, request.Weight, request.SProps, request.DProps);
			lines = new string[] { Convert.ToBase64String(buff) };
		}
		else
		{
			// All other reports can be returned as lines. The lines maybe null for format None.
			string report = wrap.Engine.GenTab(request.Name, request.Top, request.Side, request.Filter, request.Weight, request.SProps, request.DProps);
			if (report == null)
			{
				return NoContent();
			}
			lines = CommonUtil.ReadStringLines(report).ToArray();
		}
		LogInfo(230, "GenTab({Format},{Top},{Side},{Filter},{Weight}) -> #{Length})", request.DProps.Output.Format, request.Top, request.Side, request.Filter, request.Weight, lines?.Length);
		return await Task.FromResult(lines);
	}

	async Task<ActionResult<GenericResponse>> LoadReportImpl(LoadReportRequest request)
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		SessionManager.SetReportName(SessionId, request.Name);
		wrap.Engine.TableLoadCBT(request.Name);
		//using (Logger.BeginScope(new Dictionary<string, object?> { { "RequestSequence", RequestSequence }, { "Sid", Sid } }))
		//{
		//	LogInfo(232, "LoadReport {Name}", request.Name);
		//}
		LogDebug(232, "LoadReportImpl {Name} TEST", request.Name);
		return await Task.FromResult(new GenericResponse(0, $"Loaded {request.Name}"));
	}

	async Task<ActionResult<bool>> UnloadReportImpl()
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		bool changed = SessionManager.SetReportName(SessionId, null);
		return await Task.FromResult(changed);
	}

	async Task<ActionResult<XlsxResponse>> GenerateXlsxImpl()
	{
		var watch = new Stopwatch();
		watch.Start();
		using var wrap = new StateWrap(SessionId, LicProv, true);
		return await MakeXlsxAndUpload(wrap, "Generate");
	}

	/// <summary>
	/// This method generates multiple OXTs sequentially in a single call. It's only used in unit tests at
	/// the moment because it will probably cause web service call from a client to timeout.
	/// </summary>
	async Task<ActionResult<MultiOxtResponse>> MultiOxtImpl(MultiOxtRequest request)
	{
		var moxt = new MoxtState(request);
		MultiOxtSequentialProc(moxt);
		var response = new MultiOxtResponse
		{
			Id = moxt.Id,
			Created = moxt.Created,
			ProgressMessage = moxt.ProgressMessage,
			Items = moxt.Items
		};
		return await Task.FromResult(response);
	}

	DateTime multiOxtStartTime;

	/// <summary>
	/// This starts the asynchronous generation of mutiple OXTs. The processing runs on a dedicated
	/// Thread and the client can call MultiOxtQuery to track progress, using the Guid Id returned
	/// here as the key to the processing. Rememeber that this web session only lasts for the duration
	/// of this method it cannot be referenced from the async thread. The caller can request single-thread
	/// sequential processing in the traditional way, or parallel processing on multiple cores. The actual
	/// number cores used will be limited to the number available.
	/// </summary>
	async Task<ActionResult<Guid>> MultiOxtStartImpl(MultiOxtRequest request)
	{
		multiOxtStartTime = DateTime.Now;
		LogInfo(240, "MultiOxtStartImpl Enter");
		var state = MakeState(request);
		state.SessionId = SessionId;
		state.ParallelCount = request.ParallelCount;
		ParameterizedThreadStart proc = request.ParallelCount > 1 ? MultiOxtParallelProc : MultiOxtSequentialProc;
		var t = new Thread(proc);
		t.Start(state);
		LogInfo(242, "MultiOxtStartImpl Exit {StateId} tid={ManagedThreadId}", state.Id, t.ManagedThreadId);
		return await Task.FromResult(state.Id);
	}

	/// <summary>
	/// Queries the progress of multi OXT processing running on a dedicated thread, using the Guid Id
	/// that was returned by MultiOxtStart.
	/// </summary>
	async Task<ActionResult<MultiOxtResponse>> MultiOxtQueryImpl(Guid id)
	{
		var response = new MultiOxtResponse();
		var moxt = GetState(id);
		if (moxt != null)
		{
			response.Id = moxt.Id;
			response.Created = moxt.Created;
			response.ProgressMessage = moxt.ProgressMessage;
			response.IsCancelled = moxt.CancelSource.IsCancellationRequested;
			response.ParallelCount = moxt.ParallelCount;
			response.Items = moxt.Items;
			if (moxt.Items != null)
			{
				// When the Items array has a value then the loop over the multi-reports
				// is finished and we can remove the state. The caller must recognise that
				// the Items are present and realise that the reports are finished.
				RemoveState(moxt.Id);
				LogDebug(250, "Multi OXT Id {Id} complete and removed (count down to {MoxtCount})", id, MoxtList.Count);
			}
			else
			{
				//Global.LogInfo(893, $"Multi OXT Id {id} running - {moxt.ProgressMessage}");
			}
		}
		else
		{
			// There is no specific error return for this. The returned Id will be Guid.Empty.
			//Global.LogInfo(894, $"Multi OXT Id {id} not found in {Global.StateCount} items");
		}
		return await Task.FromResult(response);
	}

	async Task<ActionResult<bool>> MultiOxtCancelImpl(Guid id)
	{
		bool success = CancelState(id);
		return await Task.FromResult(success);
	}

	async Task<ActionResult<GenericResponse>> SaveReportImpl(SaveReportRequest request)
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		bool success = wrap.Engine.SaveTableUserTOC(request.Name, request.Sub, true);
		LogInfo(260, "SaveReport {Name}+{Sub}", RequestSequence, Sid, request.Name, request.Sub);
		return await Task.FromResult(new GenericResponse(0, request.Name));
	}

	async Task<ActionResult<XlsxResponse>> QuickUpdateReportImpl(QuickUpdateRequest request)
	{
		var watch = new Stopwatch();
		watch.Start();
		using var wrap = new StateWrap(SessionId, LicProv, true);
		bool result = wrap.Engine.QuickEdit(request.ShowFreq, request.ShowColPct, request.ShowRowPct, request.ShowSig, request.Filter);
		LogInfo(262, "QuickEdit {Freq} {Col} {Row} {Sig} {Filter}", RequestSequence, Sid, request.ShowFreq, request.ShowColPct, request.ShowColPct, request.ShowSig, request.Filter);
		return await MakeXlsxAndUpload(wrap, "Quick");
	}

	#endregion

	#region Props

	async Task<ActionResult<XDisplayProperties>> GetPropsImpl()
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		XDisplayProperties jobprops = wrap.Engine.GetProps();
		string json = JsonSerializer.Serialize(jobprops);
		return await Task.FromResult(jobprops);
	}

	async Task<ActionResult<XlsxResponse>> SetPropsImpl(XDisplayProperties request)
	{
		string json = JsonSerializer.Serialize(request);
		var watch = new Stopwatch();
		watch.Start();
		using var wrap = new StateWrap(SessionId, LicProv, true);
		wrap.Engine.SetProps(request);
		LogInfo(264, "Set props", RequestSequence, Sid);
		return await MakeXlsxAndUpload(wrap, "SetProps");
	}

	#endregion

	#region Spec

	async Task<ActionResult<SpecAggregate>> GetNewSpecImpl()
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		GenNode[]? axis = null;
		GenNode[]? func = null;
		try
		{
			axis = wrap.Engine.AxisTreeAsNodes();
		}
		catch (CarbonException ex)
		{
			LogTrace(266, $"Axistree -> {ex.Message} (ignored)");
		}
		try
		{
			func = wrap.Engine.FunctionListAsNodes();
		}
		catch (CarbonException ex)
		{
			LogTrace(267, $"Functree -> {ex.Message} (ignored)");
		}
		var sa = new SpecAggregate()
		{
			VariableTree = wrap.Engine.VarTreeAsNodes(),
			AxisTree = axis,
			FunctionTree = func,
			Spec = wrap.Engine.GetNewSpec()
		};
		return await Task.FromResult(sa);
	}

	async Task<ActionResult<SpecAggregate>> GetEditSpecImpl()
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		// Client apps probably need an aggregation of different bits
		// of information to enable a complete spec edit and run.
		GenNode[]? axis = null;
		GenNode[]? func = null;
		try
		{
			axis = wrap.Engine.AxisTreeAsNodes();
		}
		catch (CarbonException ex)
		{
			LogTrace(268, $"Axistree -> {ex.Message} (ignored)");
		}
		try
		{
			func = wrap.Engine.FunctionListAsNodes();
		}
		catch (CarbonException ex)
		{
			LogTrace(269, $"Functree -> {ex.Message} (ignored)");
		}
		var sa = new SpecAggregate()
		{
			VariableTree = wrap.Engine.VarTreeAsNodes(),
			AxisTree = axis,
			FunctionTree = func,
			Spec = wrap.Engine.GetEditSpec()
		};
		return await Task.FromResult(sa);
	}

	async Task<ActionResult<GenericResponse>> ValidateSpecImpl(TableSpec spec)
	{
		var watch = new Stopwatch();
		watch.Start();
		using var wrap = new StateWrap(SessionId, LicProv, false);
		try
		{
			wrap.Engine.Validate(spec);
			LogDebug(270, "ValidateSpecImpl {Spec} -> success", spec);
			return await Task.FromResult(new GenericResponse(0, "Valid"));
		}
		catch (CarbonException ex)
		{
			LogDebug(271, "ValidateSpecImpl {Spec} -> {Message}", spec, ex.Message);
			return await Task.FromResult(new GenericResponse(1, ex.Message));
		}
	}

	async Task<ActionResult<GenericResponse>> ValidateExpImpl(ValidateExpRequest request)
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		try
		{
			wrap.Engine.ValidateExp(request.Expression);
			LogDebug(277, "ValidateExp {Expression} -> success", request.Expression);
			return await Task.FromResult(new GenericResponse(0, "Valid"));
		}
		catch (CarbonException ex)
		{
			LogDebug(278, "ValidateSpecImpl {Expression} -> {Message}", request.Expression, ex.Message);
			return await Task.FromResult(new GenericResponse(1, ex.Message));
		}
	}

	async Task<ActionResult<XlsxResponse>> RunSpecImpl(RunSpecRequest request)
	{
		Trace.WriteLine(JsonSerializer.Serialize(request.Spec));
		var watch = new Stopwatch();
		watch.Start();
		using var wrap = new StateWrap(SessionId, LicProv, true);
		LogDebug(274, "RunSpecImpl {Spec}", request.Spec);
		wrap.Engine.Job.DisplayTable.DisplayProps.Output.Format = XOutputFormat.None;
		wrap.Engine.GenTab(request.Name, request.Spec);
		return await MakeXlsxAndUpload(wrap, "RunSpec");
	}

	async Task<ActionResult<string[]>> ListVartreesImpl()
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		string[] names = wrap.Engine.ListVartreeNames().ToArray();
		LogInfo(276, "List vartrees {Count}", RequestSequence, Sid, names.Length);
		return await Task.FromResult(names);
	}

	async Task<ActionResult<bool>> SetVartreeNameImpl(string name)
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		wrap.Engine.SetTreeNames(name);
		return await Task.FromResult(true);
	}

	async Task<ActionResult<GenNode[]>> VariableTreeAsNodesImpl()
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		GenNode[] gnodes = wrap.Engine.VarTreeAsNodes();
		LogInfo(280, "List vartree nodes -> {Count}", RequestSequence, Sid, gnodes?.Length);
		return await Task.FromResult(gnodes);
	}

	async Task<ActionResult<GenNode[]>> AxisTreeAsNodesImpl()
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		GenNode[] gnodes = wrap.Engine.AxisTreeAsNodes();
		LogInfo(281, "Get axis tree Nodes -> {Count}", RequestSequence, Sid, gnodes?.Length);
		return await Task.FromResult(gnodes);
	}

	async Task<ActionResult<GenNode[]>> FunctionTreeAsNodesImpl()
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		GenNode[] gnodes = wrap.Engine.FunctionListAsNodes();
		LogInfo(282, "Get function tree Nodes -> {Count}", RequestSequence, Sid, gnodes?.Length);
		return await Task.FromResult(gnodes);
	}

	async Task<ActionResult<GenNode[]>> ListAxisTreeChildrenImpl(string name)
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		GenNode[] gnodes = wrap.Engine.AxisAsNodes(name);
		LogInfo(283, "List axis '{Name}' child nodes -> {Count}", RequestSequence, Sid, name, gnodes?.Length);
		return await Task.FromResult(gnodes);
	}

	async Task<ActionResult<GenNode[]>> VarAsNodesImpl(string name)
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		GenNode[] nodes = wrap.Engine.VarAsNodes(name);
		LogInfo(284, "Get varmeta '{Name}' Nodes -> {Count}", RequestSequence, Sid, name, nodes?.Length);
		return await Task.FromResult(nodes);
	}

	async Task<ActionResult<GenNode[]>> FunctionActionImpl(FunctionActionRequest request)
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		var nodes = wrap.Engine.FunctionAction(
			request.Action.ToString(),
			request.Expression,
			request.NewExpression,
			request.Label
		);
		return await Task.FromResult(nodes);
	}

	async Task<ActionResult<GenNode[]>> NestImpl(NestRequest request)
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		string join = string.Join("&", request.Variables);
		GenNode[] nodes = wrap.Engine.Nest(request.Axis, join);
		return await Task.FromResult(nodes);
	}

	async Task<ActionResult<string>> ImportFullImpl(ImportSettings request)
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		var importer = new ImportEngine(LicProv);
		importer.AttachJob(wrap.Engine);
		importer.LoadVarTree(importer.Job.VarTree.Name);
		PImport pi = await importer.ImportAsync(request);
		return pi.Report();
	}

	async Task<ActionResult<string>> ImportPartialImpl(ImportSettings request)
	{
		using var wrap = new StateWrap(SessionId, LicProv, true);
		var importer = new ImportEngine(LicProv);
		importer.AttachJob(wrap.Engine);
		importer.LoadVarTree(importer.Job.VarTree.Name);
		bool b = await importer.ImportPartialAsync(request);
		if (!b)
		{
			throw new ApplicationException(importer.Job.Message);
		}
		return importer.Job.Message;
	}

	#endregion

	#region Manually Defined Job Endpoints

	/// <summary>
	/// Exports filtered TSAPI compliant metadata from a job (survey).
	/// </summary>
	/// <remarks>
	/// &#x1F536; Note that this endpoint replaces the <c>carbon/tsapi/query/metadata</c> endpoint in the Bayes Price licensing service.
	/// This endpoint should be used if you only want to retrieve metadata. To retrieve both metadata and interviews then it is more efficient
	/// to call the <c>job/tsapi</c> endpoint which returns an aggregate of both sets of data.
	/// </remarks>
	/// <param name="varnames">The names of the variables to be included in the export.</param>
	/// <param name="filter">A filter to apply to the export.</param>
	/// <response code="200">A TSAPI compliant <c>SurveyMetadata</c> serialized in the response body.</response>
	/// <response code="403">The request failed because no authenticated session has been established with the web service.</response>
	[HttpGet]
	[Route("tsapi/metadata")]
	[AuthFilter]
	[Produces("application/json", "text/xml")]
	[ProducesResponseType(typeof(OpenCloudJobResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<SurveyMetadata>> TsapiMetadata([FromQuery] string[] varnames, [FromQuery] string filter)
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		var exporter = new ExportEngine(LicProv);
		exporter.AttachJob(wrap.Engine);
		var query = new InterviewsQuery()
		{
			Variables = [.. varnames]
		};
		TSAPIData data = exporter.ExportTSAPI(query, filter);
		return await Task.FromResult(data.MetaData);
	}

	/// <summary>
	/// Exports filtered TSAPI compliant interviews from a job (survey).
	/// </summary>
	/// <remarks>
	/// &#x1F536; Note that this endpoint replaces the <c>carbon/tsapi/query/interview</c> endpoint in the Bayes Price licensing service.
	/// This endpoint should be used if you only want to retrieve interviews. To retrieve both metadata and interviews then it is more efficient
	/// to call the <c>job/tsapi</c> endpoint which returns an aggregate of both sets of data.
	/// </remarks>
	/// <param name="varnames">The names of the variables to be included in the export.</param>
	/// <param name="filter">A filter to apply to the export.</param>
	/// <response code="200">A TSAPI compliant <c>Interview</c> array serialized in the response body.</response>
	/// <response code="403">The request failed because no authenticated session has been established with the web service.</response>
	[HttpGet]
	[Route("tsapi/interview")]
	[AuthFilter]
	[Produces("application/json", "text/xml")]
	[ProducesResponseType(typeof(OpenCloudJobResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<Interview[]>> TsapiInterview([FromQuery] string[] varnames, [FromQuery] string filter)
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		var exporter = new ExportEngine(LicProv);
		exporter.AttachJob(wrap.Engine);
		var query = new InterviewsQuery()
		{
			Variables = [.. varnames]
		};
		TSAPIData data = exporter.ExportTSAPI(query, filter);
		return await Task.FromResult(data.Interviews);
	}

	/// <summary>
	/// Exports filtered TSAPI compliant metadata AND interviews from a job (survey).
	/// </summary>
	/// <remarks>
	/// This endpoint should be used to more efficiently retrieve both the metadata and interviews for a job (survey).
	/// The response properties <c>MetaData</c> and <c>Interviews</c> contain the TSAPI compliant retrieved data, other
	/// properties are reserved for future use will be null.
	/// </remarks>
	/// <param name="varnames">The names of the variables to be included in the export.</param>
	/// <param name="filter">A filter to apply to the export.</param>
	/// <response code="200">A serialized <c>TSAPIData</c> object which has properties containing both the metadata and interviews for a job (survey).</response>
	/// <response code="403">The request failed because no authenticated session has been established with the web service.</response>
	[HttpGet]
	[Route("tsapi")]
	[AuthFilter]
	[Produces("application/json", "text/xml")]
	[ProducesResponseType(typeof(TSAPIData), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<TSAPIData>> TsapiCombined([FromQuery] string[] varnames, [FromQuery] string filter)
	{
		using var wrap = new StateWrap(SessionId, LicProv, false);
		var exporter = new ExportEngine(LicProv);
		exporter.AttachJob(wrap.Engine);
		var query = new InterviewsQuery()
		{
			Variables = [.. varnames]
		};
		TSAPIData data = exporter.ExportTSAPI(query, filter);
		// The following are for Carbon use only and are not returned here.
		data.ExportMetadata = null;
		data.ExportInterview = null;
		return await Task.FromResult(data);
	}

	#endregion
}
