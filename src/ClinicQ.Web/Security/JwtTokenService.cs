using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClinicQ.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClinicQ.Web.Security;

public sealed record IssuedToken(string AccessToken, DateTime ExpiresAt, string TokenType = "Bearer");

/// <summary>
/// Issues and validates HMAC-SHA256 JWTs for the REST API. The same <see cref="SigningKey"/> instance is
/// shared with the JwtBearer handler so a randomly generated development key still validates.
/// </summary>
public sealed class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options, ILogger<JwtTokenService> logger)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            logger.LogWarning("Jwt:SigningKey is not configured; using a random per-process key. Tokens will not survive a restart.");
            SigningKey = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64));
        }
        else
        {
            if (_options.SigningKey.Length < 32)
            {
                throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
            }

            SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        }
    }

    public SymmetricSecurityKey SigningKey { get; }

    public string Issuer => _options.Issuer;

    public string Audience => _options.Audience;

    public IssuedToken Issue(AppUser user, DateTime utcNow)
    {
        var expires = utcNow.AddMinutes(_options.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.DisplayName),
            new(ClaimTypes.Role, user.Role)
        };

        if (user.BranchId.HasValue)
        {
            claims.Add(new Claim("branch_id", user.BranchId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: utcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _options.Issuer,
        ValidateAudience = true,
        ValidAudience = _options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
}
