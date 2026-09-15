using System.Reflection;
using Asp.Versioning;
using Azure.Storage.Blobs;
using ClinicQ.Domain.Billing;
using ClinicQ.Domain.Scheduling;
using ClinicQ.Web.Api;
using ClinicQ.Web.Data;
using ClinicQ.Web.Data.Repositories;
using ClinicQ.Web.Infrastructure.Email;
using ClinicQ.Web.Infrastructure.Pdf;
using ClinicQ.Web.Infrastructure.Storage;
using ClinicQ.Web.Jobs;
using ClinicQ.Web.Security;
using ClinicQ.Web.Services;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using SendGrid;

namespace ClinicQ.Web.Infrastructure;

/// <summary>
/// Composition root helpers, grouped by concern so Program.cs stays readable.
/// </summary>
public static class DependencyInjection
{
    /// <summary>SQL Server when ConnectionStrings:Default is set, otherwise SQLite (ConnectionStrings:Sqlite or app.db).</summary>
    public static IServiceCollection AddClinicQData(this IServiceCollection services, IConfiguration configuration)
    {
        var sqlServer = configuration.GetConnectionString("Default");
        var sqlite = configuration.GetConnectionString("Sqlite") ?? "Data Source=app.db";

        if (!string.IsNullOrWhiteSpace(sqlServer))
        {
            services.AddSingleton<IDbConnectionFactory>(new SqlServerConnectionFactory(sqlServer));
            services.AddScoped<ISlotRepository, SqlServerSlotRepository>();
            services.AddScoped<IBillingReconciliationRepository, SqlServerBillingReconciliationRepository>();
        }
        else
        {
            services.AddSingleton<IDbConnectionFactory>(new SqliteConnectionFactory(sqlite));
            services.AddScoped<ISlotRepository, SqliteSlotRepository>();
            services.AddScoped<IBillingReconciliationRepository, SqliteBillingReconciliationRepository>();
        }

        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
        services.AddScoped<ILabReportRepository, LabReportRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<DataSeeder>();
        return services;
    }

    public static IServiceCollection AddClinicQServices(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<SlotGenerator>();
        services.AddSingleton<FeeCalculator>();
        services.AddSingleton<IPdfService, PdfService>();

        services.AddScoped<AppointmentService>();
        services.AddScoped<BillingService>();
        services.AddScoped<PrescriptionService>();
        services.AddScoped<LabReportService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ApiExceptionFilter>();
        return services;
    }

    /// <summary>Email (SendGrid or console) and file storage (Azure Blob or local disk) chosen by configuration.</summary>
    public static IServiceCollection AddClinicQInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SendGridOptions>(configuration.GetSection(SendGridOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        var sendGrid = configuration.GetSection(SendGridOptions.SectionName).Get<SendGridOptions>() ?? new SendGridOptions();
        if (sendGrid.IsConfigured)
        {
            services.AddSingleton<ISendGridClient>(_ => new SendGridClient(sendGrid.ApiKey));
            services.AddSingleton<IEmailSender, SendGridEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();
        }

        var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
        if (storage.UseAzureBlob)
        {
            services.AddSingleton(_ => new BlobServiceClient(storage.AzureBlobConnectionString));
            services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
        }
        else
        {
            services.AddSingleton<IFileStorage, LocalDiskFileStorage>();
        }

        return services;
    }

    /// <summary>Cookie auth for the Razor Pages portal; JWT bearer for /api.</summary>
    public static IServiceCollection AddClinicQAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtTokenService>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/Denied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Cookie.Name = "ClinicQ.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
            })
            .AddJwtBearer();

        // The bearer handler validates with the same key the token service signs with (config or generated).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtTokenService>((options, tokens) =>
            {
                options.TokenValidationParameters = tokens.ValidationParameters;
                options.MapInboundClaims = false;
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddClinicQApi(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ClinicQ API",
                Version = "v1",
                Description = "Clinic appointment and patient records API. Obtain a token from POST /api/v1/auth/token and click Authorize."
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the access token (without the 'Bearer ' prefix).",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };
            options.AddSecurityDefinition("Bearer", scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });

            var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xml))
            {
                options.IncludeXmlComments(xml, includeControllerXmlComments: true);
            }
        });

        return services;
    }

    /// <summary>Hangfire with in-memory storage plus the recurring job classes.</summary>
    public static IServiceCollection AddClinicQJobs(this IServiceCollection services)
    {
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseMemoryStorage());
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 2;
            options.ShutdownTimeout = TimeSpan.FromSeconds(3);
        });

        services.AddScoped<AppointmentReminderJob>();
        services.AddScoped<FollowUpAlertJob>();
        services.AddScoped<BillingReconciliationJob>();
        return services;
    }

    public static TimeZoneInfo ResolveClinicTimeZone(IConfiguration configuration)
    {
        var id = configuration["Clinic:TimeZoneId"];
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    public static IOptions<T> OptionsOf<T>(this IServiceProvider provider) where T : class, new()
        => provider.GetRequiredService<IOptions<T>>();
}
