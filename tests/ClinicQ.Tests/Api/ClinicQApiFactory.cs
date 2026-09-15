using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClinicQ.Web.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ClinicQ.Tests.Api;

/// <summary>
/// Boots the real application (Program.cs) against a private in-memory SQLite database. Reference data and
/// the demo users are seeded by startup; the generated appointment history is switched off to keep tests fast.
/// </summary>
public sealed class ClinicQApiFactory : WebApplicationFactory<Program>
{
    private readonly string _database = $"Data Source=clinicq-api-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = string.Empty,      // force the SQLite fallback
                ["ConnectionStrings:Sqlite"] = _database,
                ["Seed:Enabled"] = "true",
                ["Seed:DemoActivity"] = "false",
                ["Jwt:SigningKey"] = "integration-test-signing-key-at-least-32-chars",
                ["Jwt:Issuer"] = "ClinicQ",
                ["Jwt:Audience"] = "ClinicQ.Api",
                ["Storage:LocalRootPath"] = Path.Combine(Path.GetTempPath(), "clinicq-tests", Guid.NewGuid().ToString("N"))
            });
        });
    }

    /// <summary>Signs in through /api/v1/auth/token and returns a client with the bearer token attached.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string username = "admin", string password = "Passw0rd!")
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/token", new TokenRequest { Username = username, Password = password });
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }
}
