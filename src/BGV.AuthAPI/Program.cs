using BGV.AuthAPI.Configuration;
using BGV.AuthAPI.Middleware;
using BGV.AuthAPI.Services;
using BGV.AuthAPI.Repositories;
using BGV.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Serilog;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using Microsoft.OpenApi.Models;
using OpenIddict.Server;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BGV Auth API", Version = "v1" });

    // Define the OAuth2 Password Flow for Swagger
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            Password = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri("/connect/token", UriKind.Relative),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "OpenID Connect" },
                    { "profile", "User profile" },
                    { "offline_access", "Refresh tokens" }
                }
            }
        }
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Serilog
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithMachineName()
          .Enrich.WithThreadId();
});

// OpenTelemetry
//Traces
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("BGV.AuthAPI"))
            .AddAspNetCoreInstrumentation()
            //.AddNpgsqlInstrumentation()
            //.AddNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
            .AddConsoleExporter()
            .AddOtlpExporter(opt => 
            {
                opt.Endpoint = new Uri("http://seq:5341/ingest/otlp/v1/traces");
                opt.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
            });
    });

#region OpenTelemetry Metrics and Logs (commented out for now)
// Metrics
// builder.Services.AddOpenTelemetry()
//     .WithMetrics(metricProviderBuilder =>
//     {
//         metricProviderBuilder
//             .AddAspNetCoreInstrumentation()
//             .AddRuntimeInstrumentation()
//             .AddOtlpExporter(options =>
//             {
//                 options.Endpoint = new Uri("http://localhost:5341");
//             });
//     });

// Logs
// builder.Logging.AddOpenTelemetry(logs =>
// {
//     logs.IncludeFormattedMessage = true;
//     logs.AddOtlpExporter(options =>
//     {
//         options.Endpoint = new Uri("http://localhost:5341");
//     });
// });
#endregion

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")!);
    options.UseOpenIddict();
});

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

// Ensure the API uses OpenIddict's JWT validation by default.
// This prevents Identity from redirecting unauthorized API calls to a login page (302).
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
});

// OpenIddict
builder.Services.AddOpenIddict()
                .AddCore(options =>
                {
                    options.UseEntityFrameworkCore()
                           .UseDbContext<ApplicationDbContext>();
                })
                .AddServer(options =>
                {
                    options.SetAuthorizationEndpointUris("/connect/authorize")
                           .SetTokenEndpointUris("/connect/token")
                           .SetLogoutEndpointUris("/connect/logout")
                           .SetUserinfoEndpointUris("/connect/userinfo");

                    options.AllowAuthorizationCodeFlow()
                           .AllowPasswordFlow()
                           .AllowRefreshTokenFlow();

                    // Disabling encryption allows the access token to be emitted as a standard 
                    // signed JWT, making it readable by tools like jwt.io and other APIs.
                    options.DisableAccessTokenEncryption();

                    if (builder.Environment.IsDevelopment())
                    {
                        options.AddDevelopmentEncryptionCertificate()
                               .AddDevelopmentSigningCertificate();
                    }

                    var aspNetCoreBuilder = options.UseAspNetCore()
                           .EnableAuthorizationEndpointPassthrough()
                           .EnableTokenEndpointPassthrough()
                           .EnableLogoutEndpointPassthrough()
                           .EnableUserinfoEndpointPassthrough();

                    if (builder.Environment.IsDevelopment())
                    {
                        aspNetCoreBuilder.DisableTransportSecurityRequirement();
                    }
                })
                .AddValidation(options =>
                {
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });

// Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Services
builder.Services.AddScoped<IUserService, UserService>();
// builder.Services.AddScoped<IMfaService, MfaService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();

// Middleware
// ExceptionHandlingMiddleware is added to the pipeline via app.UseMiddleware<ExceptionHandlingMiddleware>(),
// so it should not be registered directly as a DI service with RequestDelegate.

var app = builder.Build();

// Initialize database and OpenIddict
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var dbContext = services.GetRequiredService<ApplicationDbContext>();

    // Retry logic: Attempt to connect to the DB up to 10 times with a 3-second delay
    for (int i = 1; i <= 10; i++)
    {
        try
        {
            await dbContext.Database.EnsureCreatedAsync();
            await OpenIddictConfig.InitializeAsync(services);
            logger.LogInformation("Database and OpenIddict initialized successfully.");
            break;
        }
        catch (Exception ex) when (i < 10)
        {
            logger.LogWarning("Database connection failed. Postgres might still be starting. Retrying in 3s... (Attempt {Attempt}/10)", i);
            await Task.Delay(3000);
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only redirect to HTTPS in production to avoid 302 issues in local Docker/Dev environments
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();