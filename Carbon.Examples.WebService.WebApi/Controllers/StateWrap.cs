using System;
using RCS.Carbon.Tables;

namespace Carbon.Examples.WebService.WebApi.Controllers
{
	sealed class StateWrap : IDisposable
	{
		readonly string _sid;
		readonly bool _save;
		public StateWrap(string sessionId, bool saveState = false)
		{
			_sid = sessionId;
			_save = saveState;
			Engine = new CrossTabEngine();
			string?[] state = SessionManager.LoadState(_sid);
			Engine.RestoreState(state);
		}
		public void Dispose()
		{
			if (_save)
			{
				string[] state = Engine.SaveState();
				SessionManager.SaveState(_sid, state);
			}
		}
		public CrossTabEngine Engine { get; }
	}
}
