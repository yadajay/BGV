# RCD.AuthAPI

OpenIddict OIDC authorization server. Hosts the login UI, OIDC endpoints, and user management.

## Flows Supported

| Flow | Grant type | Clients |
|---|---|---|
| Authorization Code + PKCE | `authorization_code` | rcd-spa, rcd-nextjs, rcd-mvc, rcd-swagger |
| Client Credentials | `client_credentials` | rcd-service |
| Refresh Token | `refresh_token` | all interactive clients |

Password Grant is **not supported** — removed intentionally. SSO works via the auth server session cookie.

## Key Files

| File | Purpose |
|---|---|
| `Program.cs` | All service registration and pipeline. Read section comments before changing auth/OpenIddict config. |
| `Configuration/OpenIddictConfig.cs` | Seeds OIDC clients and scopes on startup. Idempotent. Add new clients here. |
| `Controllers/AuthController.cs` | OIDC endpoints: `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/logout`. |
| `Services/UserService.cs` | All user operations. `BuildPrincipalAsync` (private) is the single place claims are built — all grant types call it. |
| `Services/EmailService.cs` | Stub — logs links. Replace with real provider (SendGrid/SMTP) before production. |
| `Pages/Account/Login.cshtml` | Login form. After success redirects to `ReturnUrl` which is the `/connect/authorize?...` URL. |
| `Pages/Manage/` | Authenticated user self-service: profile view, change password. |

## Authentication Schemes

Two schemes coexist:

- **`IdentityConstants.ApplicationScheme`** (cookie) — global default. Used by Razor Pages and the `/connect/authorize` handler to check if the user has a session.
- **`OpenIddictValidationAspNetCoreDefaults`** (Bearer JWT) — used explicitly by API controllers: `[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]`.

Do not change `DefaultAuthenticateScheme` without updating all API controller `[Authorize]` attributes.

## Secrets / Configuration

- `appsettings.json` — all sensitive values are `""`. Never put secrets here.
- `appsettings.Development.json` — dev values only (localhost ports, dev secrets).
- Production: set via environment variables using `__` separator, e.g. `ClientSettings__MvcClientSecret`.

## Service Layer Rules

- All service methods return `Result` or `Result<T>` (from `RCD.Core`). Controllers check `.Success` and map to HTTP status.
- Password-reset and email-verification tokens are base64url-encoded via `WebEncoders.Base64UrlEncode` before being embedded in links, and decoded with `WebEncoders.Base64UrlDecode` on receipt.
- `UserService` depends on `IEmailService` — injected, not newed up. Swap implementations without touching service logic.

## Adding a New OIDC Client

1. Add URI config keys to `appsettings.Development.json` and document env var names.
2. Add a `RegisterXxxClientAsync` method in `OpenIddictConfig.cs` following the existing pattern.
3. Call it from `InitializeAsync`.
4. Add allowed origin to `ClientSettings:AllowedOrigins` if it's a browser client.
