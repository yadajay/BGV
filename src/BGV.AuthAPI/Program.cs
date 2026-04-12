using BGV.AuthAPI.Configuration;
using BGV.AuthAPI.Middleware;
using BGV.AuthAPI.Services;
using BGV.AuthAPI.Repositories;
using BGV.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Serilog;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "BGV Auth API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
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
                           .SetUserinfoEndpointUris("/connect/userinfo");

                    options.AllowAuthorizationCodeFlow()
                           .AllowRefreshTokenFlow();

                    options.AddDevelopmentEncryptionCertificate()
                           .AddDevelopmentSigningCertificate();

                    options.UseAspNetCore()
                           .EnableAuthorizationEndpointPassthrough()
                           .EnableTokenEndpointPassthrough();
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
builder.Services.AddScoped<ITokenService, TokenService>();
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
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await OpenIddictConfig.InitializeAsync(scope.ServiceProvider);
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