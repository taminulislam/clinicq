namespace ClinicQ.Web.Api.Contracts;

public sealed class TokenRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed record TokenResponse(string AccessToken, string TokenType, DateTime ExpiresAt, string Username, string DisplayName, string Role);
