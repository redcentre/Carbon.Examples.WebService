namespace RCS.Carbon.Example.WebService.Common.DTO;

public sealed class AuthenticateFreeRequest
{
	public AuthenticateFreeRequest(string email, bool skipCache = false)
	{
		Email = email;
		SkipCache = skipCache;
	}

	public string Email { get; set; }

	public bool SkipCache { get; set; } = false;
}
