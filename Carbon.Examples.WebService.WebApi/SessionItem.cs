using System;

namespace Carbon.Examples.WebService.WebApi
{
	/// <summary>
	/// Holds information about a single web service session.
	/// </summary>
	sealed class SessionItem
	{
		public SessionItem(string sessionId)
		{
			SessionId = sessionId;
			CreatedUtc = DateTime.UtcNow;
		}

		public string SessionId { get; }
		public DateTime CreatedUtc { get; }
		// The following members are for activity tracking
		public DateTime? LastActivityUtc { get; set; }
		public string? LastActivity { get; set; }
		public int ActivityCount { get; set; }
		public string? OpenCustomerName { get; set; }
		public string? OpenJobName { get; set; }
		public string? OpenReportName { get; set; }
		// Licence information for convenience
		public string? UserId { get; set; }
		public string? UserName { get; set; }
		public string[] Roles { get; set; } = Array.Empty<string>();

		public override string ToString() => $"({SessionId},{CreatedUtc:s},{LastActivity},{OpenCustomerName},{OpenJobName},{OpenReportName}";
	}
}
