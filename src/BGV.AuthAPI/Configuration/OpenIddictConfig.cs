using OpenIddict.Abstractions;

namespace BGV.AuthAPI.Configuration;

public static class OpenIddictConfig
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        const string RolesScope = "roles";

        using var scope = services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        // Register the client application
        if (await manager.FindByClientIdAsync("bgv-web") == null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "bgv-web",
                ClientSecret = "bgv-secret", // In production, use proper secret
                DisplayName = "BGV Web Application",
                RedirectUris = { new Uri("https://localhost:5000/callback") },
                PostLogoutRedirectUris = { new Uri("https://localhost:5000/") },
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Logout,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.Password,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Prefixes.Scope + RolesScope,
                    OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess
                }
            });
        }

        // Register standard OIDC and custom scopes
        // These must exist in the database for OpenIddict to validate them during a token request
        var standardScopes = new[] 
        { 
            OpenIddictConstants.Scopes.Email, 
            OpenIddictConstants.Scopes.Profile, 
            RolesScope 
        };

        foreach (var scopeName in standardScopes)
        {
            if (await scopeManager.FindByNameAsync(scopeName) == null)
            {
                await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
                {
                    Name = scopeName,
                    DisplayName = $"{scopeName} scope"
                });
            }
        }

        // Register the API scope
        if (await scopeManager.FindByNameAsync("api") == null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "api",
                DisplayName = "BGV API access",
                Resources = { "bgv-api" }
            });
        }
    }
}