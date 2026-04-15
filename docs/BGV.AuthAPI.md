BGV.AuthAPI Documentation
Project Overview
BGV.AuthAPI is the authentication and authorization service for the BGV solution. It exposes REST endpoints for user registration, login, password management, refresh tokens, logout, and user administration. It uses ASP.NET Core Identity, OpenIddict, Entity Framework Core, PostgreSQL, Redis, Serilog, and OpenTelemetry.
Architecture
- Web API project built on ASP.NET Core.
- Uses Identity for user management and roles.
- Uses OpenIddict to implement OAuth2/OpenID Connect flows.
- Stores users and auth data in PostgreSQL via ApplicationDbContext.
- Stores refresh tokens in Redis.
Startup Configuration (Program.cs)
- Registers controllers, Swagger, OpenTelemetry, and Redis.
- Configures ApplicationDbContext with PostgreSQL and OpenIddict support.
- Configures Identity with ApplicationUser and IdentityRole.
- Configures OpenIddict core, server, and validation.
- Adds custom scoped services: UserService, repositories, and exception middleware.
Identity and OpenIddict
- Identity uses ApplicationUser (derived from IdentityUser) to store users.
- AddEntityFrameworkStores<ApplicationDbContext>() connects Identity to the database.
- OpenIddict config enables authorization code and refresh token flows.
- OpenIddict endpoints: /connect/authorize, /connect/token, /connect/userinfo.
- OpenIddictConfig seeds a client application and API scope on startup.
Data Model
- ApplicationDbContext inherits IdentityDbContext<ApplicationUser>.
- ApplicationUser extends IdentityUser and adds an IsActive flag.
- Identity tables are created by EF migrations using the configured PostgreSQL connection.
API Endpoints
AuthController routes with base path /api/v1/auth:
- POST /register: Register a new user.
- POST ~/connect/token: OIDC Exchange (handles Login and Refresh).
- POST /logout: Revoke session and sign out.
- POST /forgot-password: request password reset.
- POST /reset-password: reset password using token.
- POST /verify-email: verify email address token.
UserManagementController routes with base path /api/v1/users:
- Requires Admin role authorization.
- GET /: return all users.
- GET /{id}: return a single user by ID.
- PUT /{id}: update user details.
- DELETE /{id}: delete a user.
- POST /{id}/roles: add a role to a user.
- DELETE /{id}/roles/{role}: remove a role from a user.
Token Flow
- Standard OIDC Exchange: The `Exchange` action handles `password` and `refresh_token` grant types.
- Principal Creation: `UserService` creates the `ClaimsPrincipal` and maps Identity claims to OIDC destinations.
- JWT Generation: OpenIddict generates signed JWT access tokens (encryption disabled in Dev for inspection).
- Persistence: OpenIddict manages token storage and revocation automatically via the PostgreSQL database.
Configuration
- DefaultConnection points to PostgreSQL.
- Redis connection is configured for refresh token storage.
- Jwt settings define Secret, Issuer, and Audience.
- Serilog settings enable console and Seq logging.
OpenID Connect Management API and UI
- The OpenIddict server exposes OIDC discovery metadata at /.well-known/openid-configuration.
- The API must expose these endpoints: /connect/authorize, /connect/token, /connect/userinfo.
- A UI or OIDC client should use the authorization code flow with PKCE when possible.
- The registered OpenIddict client is bgv-web with redirect URI https://localhost:5000/callback.
- Required client permission scopes for UI/API management include: openid, profile, email, roles, api, offline_access.
- For a browser-based UI, configure CORS to allow the UI origin and enable credentials.
- Example OIDC client settings in appsettings for a UI client:
"OpenIdConnect": {
"Authority": "https://localhost:5001",
"ClientId": "bgv-web",
"ClientSecret": "bgv-secret",
"ResponseType": "code",
"Scope": "openid profile email roles api offline_access",
"RedirectUri": "https://localhost:5000/callback",
"PostLogoutRedirectUri": "https://localhost:5000/"
}
- If an external UI or management dashboard needs to call the API, it should request both access and refresh tokens.
- If management APIs require admin access, use a client with role-based authorization and Admin role claims.
- For OpenID Connect UI workflows, ensure the UI can parse the discovery document and use the endpoints to authenticate, refresh tokens, and call the userinfo endpoint.
Flow Diagram
Client -> AuthController -> UserService -> UserManager -> ApplicationDbContext -> PostgreSQL
Client -> AuthController -> TokenService -> Redis / JWT generation
Client -> UserManagementController -> UserService -> UserRepository -> ApplicationDbContext -> PostgreSQL
Diagram
Client
|
v
AuthController
|
v
UserService
|
+---> UserManager & SignInManager -> ApplicationDbContext -> PostgreSQL
|
v
UserManagementController (Admin only) -> UserRepository -> ApplicationDbContext -> PostgreSQL

## Standard OIDC Endpoints Status
| Endpoint       | Path                                   | Status in BGV   | Description                                                                 |
|----------------|----------------------------------------|-----------------|-----------------------------------------------------------------------------|
| Discovery      | /.well-known/openid-configuration      | ✅ Automatic     | Provides metadata about your server's capabilities.                         |
| JWKS           | /.well-known/jwks.json                 | ✅ Automatic     | Contains public keys used to verify JWT signatures.                         |
| Token          | /connect/token                         | ✅ Implemented   | Exchanges credentials for access/refresh tokens.                            |
| UserInfo       | /connect/userinfo                      | ❌ Missing       | Returns claims about the authenticated user.                                |
| Logout         | /connect/logout                        | ⚠️ Incomplete    | The OIDC "End Session" endpoint (distinct from your custom API logout).     |
| Authorize      | /connect/authorize                     | ❌ Missing       | Used for browser-based logins (Authorization Code Flow).                     |
| Introspection  | /connect/introspect                    | ❌ Missing       | Allows APIs to check if a token is still valid (useful for opaque tokens).   |
| Revocation     | /connect/revocation                    | ❌ Missing       | Allows clients to invalidate a specific token.                              |

## Implementation Plan
### Step 1: Implement the UserInfo Endpoint
- Most critical missing piece for OIDC compliance.
- Although configured in Program.cs, AuthController requires an action to return user claims in standardized JSON format.

### Step 2: Implement OIDC Logout (End Session)
- Current implementation: `api/v1/auth/logout`.
- OIDC requires `/connect/logout` supporting `post_logout_redirect_uri` to safely redirect users back to the web app.

### Step 3: Enable Revocation and Introspection
- Optional but recommended for robust SaaS platforms.
- Improves refresh token security and allows APIs to validate tokens.

### Step 4: The "Authorize" Question
- Current flow: Password Grant (API-based login).
- OIDC standard: Authorization Code Flow with PKCE.
- Requires UI pages for credential entry and consent.
