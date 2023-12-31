﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RCS.Carbon.Shared;
using RCS.Carbon.Variables;
using Carbon.Examples.WebService.Common;
using tab = RCS.Carbon.Tables;
using lic = RCS.Licensing.Stdlib;

namespace Carbon.Examples.WebService.WebApi.Controllers
{
    partial class SessionController
	{
		async Task<ActionResult<SessionInfo>> StartSessionFreeImpl(AuthenticateFreeRequest request)
		{
			var engine = new tab.CrossTabEngine();
			LicenceInfo licence = await engine.GetFreeLicence(request.Email, LicensingBaseAddress, request.SkipCache);
			string sessionId = MakeSessionId();
			SessionManager.StartSession(sessionId, licence);
			var accinfo = LicToInfo(licence, sessionId);
			string[] state = engine.SaveState();
			SessionManager.SaveState(sessionId, state);
			Logger.LogInformation(103, "{RequestSequence} {Sid} Start Free Session {SessionId} Id {LicenceId} Name {LicenceName}", RequestSequence, GeneralActionFilterAttribute.EmptySid, sessionId, licence.Id, licence.Name);
			return Ok(accinfo);
		}

		async Task<ActionResult<SessionInfo>> LoginIdImpl(AuthenticateIdRequest request)
		{
			try
			{
				var engine = new tab.CrossTabEngine();
				LicenceInfo licence = await engine.LoginId(request.Id, request.Password, LicensingBaseAddress, request.SkipCache);
				string sessionId = MakeSessionId();
				SessionManager.StartSession(sessionId, licence);
				var accinfo = LicToInfo(licence, sessionId);
				string[] state = engine.SaveState();
				SessionManager.SaveState(sessionId, state);
				Logger.LogInformation(100, "{RequestSequence} {Sid} Login Session {SessionId} Id {LicenceId} Name {LicenceName}", RequestSequence, GeneralActionFilterAttribute.EmptySid, sessionId, licence.Id, licence.Name);
				return Ok(accinfo);
			}
			catch (CarbonException ex)
			{
				return BadRequest(new ErrorResponse(ex.Code, ex.Message));
			}
		}

		async Task<ActionResult<SessionInfo>> AuthenticateNameImpl(AuthenticateNameRequest request)
		{
			try
			{
				var engine = new tab.CrossTabEngine();
				LicenceInfo licence = await engine.GetLicenceName(request.Name, request.Password, LicensingBaseAddress);
				string sessionId = MakeSessionId();
				SessionManager.StartSession(sessionId, licence);
				var accinfo = LicToInfo(licence, sessionId);
				string[] state = engine.SaveState();
				SessionManager.SaveState(sessionId, state);
				Logger.LogInformation(100, "{RequestSequence} {Sid} Login Session {SessionName} Name {LicenceName}", RequestSequence, GeneralActionFilterAttribute.EmptySid, sessionId, licence.Name);
				return Ok(accinfo);
			}
			catch (CarbonException ex)
			{
				return BadRequest(new ErrorResponse(ex.Code, ex.Message));
			}
		}

		async Task<ActionResult<bool>> EndSessionImpl(string sessionId)
		{
			bool success = SessionManager.EndSession(sessionId);
			long total = SessionManager.DeleteState(sessionId);
			double kb = total / 1024.0;
			Logger.LogInformation(104, "{RequestSequence} {Sid} End Session {SessionId} {Total}", RequestSequence, Sid, sessionId, total);
			return await Task.FromResult(success);
		}

		async Task<ActionResult<int>> LogoffSessionImpl(string sessionId)
		{
			var engine = new tab.CrossTabEngine();
			SessionItem? si = SessionManager.FindSession(SessionId);
			int count = -1;
			if (si != null)
			{
				if (si.UserId != null)
				{
					count = await engine.LogoutId(si.UserId, LicensingBaseAddress);
				}
				bool success = SessionManager.EndSession(sessionId);
				long total = SessionManager.DeleteState(sessionId);
				string showuserid = si.UserId ?? "NULL";
				Logger.LogInformation(102, "{RequestSequence} {Sid} Logoff Session {SessionId} Count {Count} User Id {UserId} Success {Success} State {Total}", RequestSequence, Sid, sessionId, count, showuserid, success, total);
				return Ok(count);
			}
			else
			{
				Logger.LogWarning(103, "{RequestSequence} {Sid} Logoff Session {SessionId} not found", RequestSequence, Sid, sessionId);
				return Ok(-1);
			}
		}

		async Task<ActionResult<int>> ReturnSessionImpl(string sessionId)
		{
			var engine = new tab.CrossTabEngine();
			SessionItem? si = SessionManager.FindSession(SessionId);
			int count = -1;
			if (si != null)
			{
				if (si.UserId != null)
				{
					count = await engine.ReturnId(si.UserId, LicensingBaseAddress);
				}
				bool success = SessionManager.EndSession(sessionId);
				long total = SessionManager.DeleteState(sessionId);
				string showuserid = si.UserId ?? "NULL";
				Logger.LogInformation(102, "{RequestSequence} {Sid} Return Session {SessionId} Count {Count} User Id {UserId} Success {Success} State {Total}", RequestSequence, Sid, sessionId, count, showuserid, success, total);
				return Ok(count);
			}
			else
			{
				Logger.LogWarning(103, "{RequestSequence} {Sid} Return Session {SessionId} not found", RequestSequence, Sid, sessionId);
				return Ok(-1);
			}
		}

		async Task<ActionResult<SessionStatus[]>> ListSessionsImpl()
		{
			var list = SessionManager.ListSessions().Select(s => new SessionStatus()
			{
				SessionId = s.Item1,
				ActivityCount = s.Item2.ActivityCount,
				CreatedUtc = s.Item2.CreatedUtc,
				LastActivity = s.Item2.LastActivity,
				LastActivityUtc = s.Item2.LastActivityUtc,
				UserId = s.Item2.UserId,
				UserName = s.Item2.UserName
			}).ToArray();
			return await Task.FromResult(list);
		}

		async Task<GenericResponse> ChangePasswordImpl(ChangePasswordRequest request)
		{
			var licreq = new lic.ChangePasswordRequest()
			{
				UserId = request.UserId,
				OldPassword = request.OldPassword,
				NewPassword = request.Newpassword
			};
			lic.ErrorResponse resp = await Lic.ChangePassword(licreq);
			return new GenericResponse(resp.Code, resp.Message);
		}

		async Task<GenericResponse> UpdateAccountImpl(UpdateAccountRequest request)
		{
			var licreq = new lic.UpdateAccountRequest()
			{
				UserId = request.UserId,
				UserName = request.UserName,
				Comment = request.Comment,
				Email = request.Email
			};
			lic.ErrorResponse resp = await Lic.UpdateAccount(licreq);
			return new GenericResponse(resp.Code, resp.Message);
		}

		static SessionInfo LicToInfo(LicenceInfo licence, string sessionId) => new SessionInfo()
		{
			SessionId = sessionId,
			Id = licence.Id,
			Name = licence.Name,
			Email = licence.Email,
			Roles = licence.Roles,
			SessionCusts = licence.Customers.Select(c => new SessionCust()
			{
				Id = c.Id,
				Name = c.Name,
				DisplayName = c.DisplayName,
				AgencyId = c.AgencyId,
				Info = c.Info,
				Logo = c.Logo,
				Url = c.Url,
				Sequence = c.Sequence,
				ParentAgency = new SessionAgency() { Id = c.ParentAgency.Id, Name = c.ParentAgency.Name },
				SessionJobs = c.Jobs.Select(j => new SessionJob()
				{
					Id = j.Id,
					Name= j.Name,
					DisplayName = j.DisplayName,
					VartreeNames = j.VartreeNames,
					Description = j.Description,
					Info = c.Info,
					Logo = c.Logo,
					Url = c.Url,
					Sequence = c.Sequence
				}).ToArray()
			}).ToArray()
		};

		/// <ignore/>
		public const int SessionIdLength = 10;

		/// <summary>
		/// Generates a random Session Id which can take about 10^15 values assuming that the
		/// Guid hash codes are equidistributed, which is likely because it is suspected that 
		/// they are crypto-strong. A session Id contains about 50 bits of entropy.
		/// </summary>
		static string MakeSessionId()
		{
			const string NiceChars = "123456789ABCDEFGHKLMNPQRSTVWXYZ";
			var chars = from i in Enumerable.Range(0, SessionIdLength)
						let r = Guid.NewGuid().GetHashCode() & 0x7fffffff
						let x = r % NiceChars.Length
						select NiceChars[x];
			return new string(chars.ToArray());
		}
	}
}
