using OpenIddict.Abstractions;

namespace RCD.AuthAPI.Configuration;

/// <summary>
/// Seeds OpenIddict client applications and custom scopes into the database on startup.
/// This runs inside <c>EnsureCreatedAsync</c> so the schema exists before we write.
/// <para>
/// Each <c>RegisterXxxClientAsync</c> method is idempotent — it checks by <c>client_id</c>
/// before creating, so restarting the application does not duplicate records.
/// </para>
/// <para>
/// Client URIs and secrets are read from <c>IConfiguration</c> (appsettings / env vars).
/// In production, set secrets via environment variables:
/// <list type="bullet">
///   <item><c>ClientSettings__NextJsClientSecret</c></item>
///   <item><c>ClientSettings__MvcClientSecret</c></item>
///   <item><c>ClientSettings__ServiceClientSecret</c></item>
/// </list>
/// </para>
/// </summary>
public static class OpenIddictConfig
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var manager      = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        await RegisterScopesAsync(scopeManager);
        await RegisterSpaClientAsync(manager, configuration);
        await RegisterNextJsClientAsync(manager, configuration);
        await RegisterMvcClientAsync(manager, configuration);
        await RegisterServiceClientAsync(manager, configuration);
        await RegisterSwaggerClientAsync(manager, configuration);
    }

    // ── Scopes ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers the OIDC standard scopes (<c>email</c>, <c>profile</c>) and custom scopes
    /// (<c>roles</c>, <c>api</c>) that clients can request.
    /// The <c>api</c> scope carries a resource claim (<c>rcd-api</c>) which downstream APIs
    /// can use to verify the token was issued for them.
    /// </summary>
    private static async Task RegisterScopesAsync(IOpenIddictScopeManager manager)
    {
        var scopes = new[]
        {
            (OpenIddictConstants.Scopes.Email,   "Email address"),
            (OpenIddictConstants.Scopes.Profile, "User display name"),
            ("roles",                            "User roles"),
        };

        foreach (var (name, display) in scopes)
        {
            if (await manager.FindByNameAsync(name) == null)
                await manager.CreateAsync(new OpenIddictScopeDescriptor { Name = name, DisplayName = display });
        }

        if (await manager.FindByNameAsync("api") == null)
        {
            await manager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "api",
                DisplayName = "RCD API access",
                // The resource value is checked by downstream APIs that use OpenIddict validation.
                // APIs configured with UseLocalServer() will only accept tokens that include their resource.
                Resources = { "rcd-api" }
            });
        }
    }

    // ── Clients ───────────────────────────────────────────────────────────────

    /// <summary>
    /// React SPA client — <b>public</b>, no client secret.
    /// PKCE is required (<see cref="OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange"/>)
    /// because a public browser client cannot securely store a secret.
    /// Redirect URI: configured via <c>ClientSettings:SpaRedirectUri</c>.
    /// </summary>
    private static async Task RegisterSpaClientAsync(IOpenIddictApplicationManager manager, IConfiguration config)
    {
        if (await manager.FindByClientIdAsync("rcd-spa") != null) return;

        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId    = "rcd-spa",
            ClientType  = OpenIddictConstants.ClientTypes.Public,
            DisplayName = "RCD React SPA",
            RedirectUris          = { new Uri(config["ClientSettings:SpaRedirectUri"]!) },
            PostLogoutRedirectUris = { new Uri(config["ClientSettings:SpaPostLogoutUri"]!) },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Logout,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,
                OpenIddictConstants.Permissions.Prefixes.Scope + "api",
            },
            // PKCE enforced — OpenIddict will reject authorization requests without a code_challenge.
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange }
        });
    }

    /// <summary>
    /// Next.js SSR client — <b>confidential</b>, has a client secret.
    /// The secret is known only to the Next.js server (never the browser).
    /// PKCE is still required as defence-in-depth even for confidential clients.
    /// Secret: set via env var <c>ClientSettings__NextJsClientSecret</c>.
    /// </summary>
    private static async Task RegisterNextJsClientAsync(IOpenIddictApplicationManager manager, IConfiguration config)
    {
        if (await manager.FindByClientIdAsync("rcd-nextjs") != null) return;

        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId     = "rcd-nextjs",
            ClientSecret = config["ClientSettings:NextJsClientSecret"],
            ClientType   = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName  = "RCD Next.js App",
            RedirectUris          = { new Uri(config["ClientSettings:NextJsRedirectUri"]!) },
            PostLogoutRedirectUris = { new Uri(config["ClientSettings:NextJsPostLogoutUri"]!) },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Logout,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,
                OpenIddictConstants.Permissions.Prefixes.Scope + "api",
            },
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange }
        });
    }

    /// <summary>
    /// .NET MVC client — <b>confidential</b>, has a client secret stored server-side.
    /// ASP.NET Core's <c>AddOpenIdConnect()</c> middleware handles the code exchange
    /// automatically; the MVC app never exposes tokens to the browser.
    /// Secret: set via env var <c>ClientSettings__MvcClientSecret</c>.
    /// </summary>
    private static async Task RegisterMvcClientAsync(IOpenIddictApplicationManager manager, IConfiguration config)
    {
        if (await manager.FindByClientIdAsync("rcd-mvc") != null) return;

        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId     = "rcd-mvc",
            ClientSecret = config["ClientSettings:MvcClientSecret"],
            ClientType   = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName  = "RCD MVC App",
            RedirectUris          = { new Uri(config["ClientSettings:MvcRedirectUri"]!) },
            PostLogoutRedirectUris = { new Uri(config["ClientSettings:MvcPostLogoutUri"]!) },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Logout,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,
                OpenIddictConstants.Permissions.Prefixes.Scope + "api",
            },
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange }
        });
    }

    /// <summary>
    /// Background service / API-to-API client — <b>confidential</b>, client credentials only.
    /// No user interaction, no authorization code flow, no refresh tokens.
    /// Use this when a background job or internal service needs to call a protected API.
    /// Secret: set via env var <c>ClientSettings__ServiceClientSecret</c>.
    /// </summary>
    private static async Task RegisterServiceClientAsync(IOpenIddictApplicationManager manager, IConfiguration config)
    {
        if (await manager.FindByClientIdAsync("rcd-service") != null) return;

        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId     = "rcd-service",
            ClientSecret = config["ClientSettings:ServiceClientSecret"],
            ClientType   = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName  = "RCD Backend Service",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + "api",
            }
        });
    }

    /// <summary>
    /// Swagger UI client — <b>public</b>, PKCE required, no client secret.
    /// Used only for interactive API exploration in the Swagger UI.
    /// The redirect URI must match exactly: <c>ClientSettings:SwaggerRedirectUri</c>
    /// (typically <c>https://localhost:{PORT}/swagger/oauth2-redirect.html</c> in development).
    /// Update this value if your dev port differs from the default.
    /// </summary>
    private static async Task RegisterSwaggerClientAsync(IOpenIddictApplicationManager manager, IConfiguration config)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId    = "rcd-swagger",
            ClientType  = OpenIddictConstants.ClientTypes.Public,
            DisplayName = "Swagger UI",
            RedirectUris = { new Uri(config["ClientSettings:SwaggerRedirectUri"]!) },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + "roles",
                OpenIddictConstants.Permissions.Prefixes.Scope + "api",
            },
            Requirements = { OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange }
        };

        var existing = await manager.FindByClientIdAsync("rcd-swagger");
        if (existing == null)
            await manager.CreateAsync(descriptor);
        else
            await manager.UpdateAsync(existing, descriptor);
    }
}
