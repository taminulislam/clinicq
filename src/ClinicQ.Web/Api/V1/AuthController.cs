using ClinicQ.Web.Api.Contracts;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQ.Web.Api.V1;

/// <summary>Issues JWT bearer tokens for API clients.</summary>
[AllowAnonymous]
public sealed class AuthController : ApiControllerBase
{
    private readonly IUserRepository _users;
    private readonly JwtTokenService _tokens;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserRepository users, JwtTokenService tokens, ILogger<AuthController> logger)
    {
        _users = users;
        _tokens = tokens;
        _logger = logger;
    }

    /// <summary>Exchange username/password for a bearer token. Seeded users: admin, reception, drpatel, billing (password Passw0rd!).</summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Token([FromBody] TokenRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByUsernameAsync(request.Username, cancellationToken);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed token request for {Username}", request.Username);
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid credentials", Detail = "Username or password is incorrect." });
        }

        var issued = _tokens.Issue(user, DateTime.UtcNow);
        return Ok(new TokenResponse(issued.AccessToken, issued.TokenType, issued.ExpiresAt, user.Username, user.DisplayName, user.Role));
    }
}
