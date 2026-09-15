using System.Text.Json.Serialization;
using ClinicQ.Web.Data;
using ClinicQ.Web.Infrastructure;
using ClinicQ.Web.Jobs;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using QuestPDF.Infrastructure;
using Serilog;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Serilog structured logging. Console sink is always on; the Application Insights sink is opt-in:
//   1. dotnet add package Serilog.Sinks.ApplicationInsights
//   2. set APPLICATIONINSIGHTS_CONNECTION_STRING (App Service setting / Key Vault reference)
//   3. add: .WriteTo.ApplicationInsights(services.GetRequiredService<TelemetryConfiguration>(), TelemetryConverter.Traces)
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ClinicQ")
    .WriteTo.Console());

builder.Services
    .AddClinicQData(builder.Configuration)
    .AddClinicQServices()
    .AddClinicQInfrastructure(builder.Configuration)
    .AddClinicQAuth(builder.Configuration)
    .AddClinicQApi()
    .AddClinicQJobs();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToFolder("/Account");
    options.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ClinicQ API v1");
    options.DocumentTitle = "ClinicQ API";
});

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter(app.Environment.IsDevelopment()) },
    DashboardTitle = "ClinicQ Jobs"
});

app.MapHealthChecks("/health", new HealthCheckOptions { AllowCachingResponses = false }).AllowAnonymous();
app.MapControllers();
app.MapRazorPages();

await InitializeDatabaseAsync(app);
RecurringJobScheduler.Register(app.Services.GetRequiredService<IRecurringJobManager>(), DependencyInjection.ResolveClinicTimeZone(app.Configuration));

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

    if (app.Configuration.GetValue("Seed:Enabled", true))
    {
        var includeDemoActivity = app.Configuration.GetValue("Seed:DemoActivity", true);
        await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync(includeDemoActivity);
    }
}

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program
{
}
