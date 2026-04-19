using RCD.AuthAPI.Configuration;
using RCD.AuthAPI.Middleware;
using RCD.AuthAPI.Services;
using RCD.AuthAPI.Repositories;
using RCD.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Serilog;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── MVC + Razor Pages ─────────────────────────────────────────────────────────
// Controllers serve the OIDC endpoints and REST management API.
// Razor Pages serve the auth server UI: login, logout, profile, password management.
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();

// ── Swagger ───────────────────────────────────────────────────────────────────
// Configured with Authorization Code + PKCE (matching real browser flow).
// "Authorize" in Swagger UI redirects through the actual login page — the rcd-swagger
// public client handles the exchange. No client secret is needed or stored.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "RCD Auth API", Version = "v1" });

    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri("/connect/authorize", UriKind.Relative),
                TokenUrl = new Uri("/connect/token", UriKind.Relative),
                Scopes = new Dictionary<string, string>
                {
                    { "openid",         "OpenID Connect identity token" },
                    { "profile",        "User display name" },
                    { "email",          "User email address" },
                    { "roles",          "User roles" },
                    { "offline_access", "Refresh token for silent renewal" },
                    { "api",            "Access to RCD backend APIs" }
                }
            }
        }
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Serilog ───────────────────────────────────────────────────────────────────
// Structured logging to console and Seq. Level and sinks are configured in appsettings.
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithMachineName()
          .Enrich.WithThreadId();
});

// ── OpenTelemetry ─────────────────────────────────────────────────────────────
// Traces exported to Seq via OTLP. Console exporter is active for local debugging.
builder.Services.AddOpenTelemetry()
    .WithTracing(t =>
    {
        t.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("RCD.AuthAPI"))
         .AddAspNetCoreInstrumentation()
         .AddConsoleExporter()
         .AddOtlpExporter(opt =>
         {
             opt.Endpoint = new Uri("http://seq:5341/ingest/otlp/v1/traces");
             opt.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
         });
    });

// ── Database ──────────────────────────────────────────────────────────────────
// PostgreSQL via EF Core. UseOpenIddict() adds OpenIddict's entity sets (applications,
// authorizations, scopes, tokens) to the same DbContext so all OIDC state lives in one DB.
// Override connection string via env var: ConnectionStrings__DefaultConnection=...
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")!);
    options.UseOpenIddict();
});

// ── Identity ──────────────────────────────────────────────────────────────────
// ASP.NET Core Identity manages users, roles, password hashing, and lockout policy.
// AddDefaultTokenProviders() enables password-reset and email-confirmation token generation.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

// Override Identity's default cookie redirect paths to our custom Razor Pages.
// Without this, a bare [Authorize] on Manage pages would redirect to /Identity/Account/Login.
// Override cookie lifetime and sliding expiry for the auth server session here.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// ── Authentication ────────────────────────────────────────────────────────────
// Two authentication schemes coexist:
//   1. IdentityConstants.ApplicationScheme (cookie) — default scheme used by Razor Pages
//      and the /connect/authorize endpoint to check whether the user has a session.
//   2. OpenIddictValidationAspNetCoreDefaults (Bearer JWT) — used explicitly by API
//      controllers via [Authorize(AuthenticationSchemes = ...)].
// Setting the cookie as the global default means User.Identity.IsAuthenticated works
// in Razor Pages views without any extra decoration.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme    = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme       = IdentityConstants.ApplicationScheme;
});

// ── OpenIddict ────────────────────────────────────────────────────────────────
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        // Persists OIDC applications, scopes, authorizations, and tokens in PostgreSQL.
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })
    .AddServer(options =>
    {
        // Standard OIDC endpoint URIs. Passthrough is enabled for each so our controllers
        // handle the request logic; OpenIddict handles the protocol validation around them.
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetLogoutEndpointUris("/connect/logout")
               .SetUserinfoEndpointUris("/connect/userinfo");

        // Supported grant types:
        //   Authorization Code — browser-based clients (SPA, Next.js, MVC). PKCE enforced per client.
        //   Client Credentials — machine-to-machine (background services calling protected APIs).
        //   Refresh Token      — silent access token renewal for interactive clients.
        // Password Grant is intentionally excluded: it bypasses the auth server login UI,
        // preventing SSO session reuse, MFA, and account lockout protection.
        options.AllowAuthorizationCodeFlow()
               .AllowClientCredentialsFlow()
               .AllowRefreshTokenFlow();

        // Emit access tokens as standard signed JWTs so downstream APIs can validate them
        // locally using the JWKS endpoint without a round-trip to this server.
        options.DisableAccessTokenEncryption();

        // Development-only: use auto-generated ephemeral certificates.
        // In production, replace with persistent signing certificates stored in Key Vault or a cert store.
        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }

        // EnableXxxEndpointPassthrough hands request handling to our controllers.
        // Without passthrough, OpenIddict would handle requests internally.
        var aspNetCore = options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableLogoutEndpointPassthrough()
            .EnableUserinfoEndpointPassthrough();

        // Allow HTTP in development (Docker, localhost).
        // This flag must never reach production — HTTPS is required for token security.
        if (builder.Environment.IsDevelopment())
            aspNetCore.DisableTransportSecurityRequirement();
    })
    .AddValidation(options =>
    {
        // Validate tokens issued by this server locally (reads signing keys from the server's store).
        // API controllers decorated with [Authorize(AuthenticationSchemes = OpenIddictValidation...)]
        // use this validator — no external network call is required per request.
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// ── CORS ──────────────────────────────────────────────────────────────────────
// Browser SPA and Next.js clients call /connect/token from JavaScript, which requires
// CORS. Add each client origin to ClientSettings:AllowedOrigins in appsettings.
// Override via env var: ClientSettings__AllowedOrigins__0=https://your-spa.com
builder.Services.AddCors(options =>
{
    options.AddPolicy("SpaClients", policy =>
    {
        var origins = builder.Configuration
            .GetSection("ClientSettings:AllowedOrigins")
            .Get<string[]>() ?? [];

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── Redis Cache ───────────────────────────────────────────────────────────────
// Registers IDistributedCache backed by Redis. Available for injection in any service
// that needs distributed caching (e.g. rate-limit counters, query result caching in BackendAPI).
// Override via env var: ConnectionStrings__Redis=redis-host:6379
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
// EmailService is a logging stub in development. Replace the implementation with a real
// provider (SendGrid, SMTP via MailKit, etc.) before sending to production.
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// Initialise the DB schema and seed OpenIddict clients/scopes on startup.
// Retries up to 10 times to handle containers where Postgres starts after this service.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger   = services.GetRequiredService<ILogger<Program>>();
    var dbContext = services.GetRequiredService<ApplicationDbContext>();

    for (int i = 1; i <= 10; i++)
    {
        try
        {
            await dbContext.Database.EnsureCreatedAsync();
            await OpenIddictConfig.InitializeAsync(services, app.Configuration);
            logger.LogInformation("Database and OpenIddict initialized successfully.");
            break;
        }
        catch (Exception) when (i < 10)
        {
            logger.LogWarning("DB not ready. Retrying in 3s... ({Attempt}/10)", i);
            await Task.Delay(3000);
        }
    }
}

// ── HTTP Pipeline ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // OAuthClientId must match the rcd-swagger client registered in OpenIddictConfig.
    // OAuthUsePkce() generates a code_verifier/challenge pair automatically for each auth attempt.
    // Update SwaggerRedirectUri in appsettings.Development.json if your dev port differs from 7001.
    app.UseSwaggerUI(c =>
    {
        c.OAuthClientId("rcd-swagger");
        c.OAuthUsePkce();
        c.OAuthScopes("openid", "profile", "email");
    });
}

// Only force HTTPS redirect in production; HTTP is acceptable in local dev/Docker.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// Serve wwwroot static files (CSS, JS, images for the auth server UI).
app.UseStaticFiles();

// Apply CORS before authentication so preflight OPTIONS requests are answered correctly.
app.UseCors("SpaClients");

// Global exception handler — returns RFC 7807 Problem Details JSON for unhandled errors.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Session must be before UseAuthentication so session data is available to auth middleware.
// UseRouting before UseAuthentication is required when using MapControllers + MapRazorPages
// with the new minimal hosting model.
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

await app.RunAsync();
