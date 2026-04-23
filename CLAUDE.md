# RCD Solution

OIDC-based platform with a central auth server and multiple client applications.

## Projects

| Project | Purpose |
|---|---|
| `RCD.Core` | Shared models only (`Result<T>`, `TokenPair`). No external dependencies. |
| `RCD.Infrastructure` | Two distinct roles: (1) EF Core `ApplicationDbContext` + `ApplicationUser` for Identity/OpenIddict; (2) `NpgsqlConnectionFactory` + DbUp migration runner for BackendAPI's Dapper-based data access. |
| `RCD.AuthAPI` | OpenIddict OIDC auth server. Login UI (Razor Pages) + OIDC endpoints + user management API. |
| `RCD.BackendAPI` | Protected resource API. Dapper + stored procedures. Validates tokens issued by RCD.AuthAPI. |
| `RCD.Web` | ASP.NET Core MVC client. Authenticates via Authorization Code flow. HTMX for partial-page updates. |

## Test Projects

| Project | Covers |
|---|---|
| `tests/RCD.Core.Tests` | `Result`, `Result<T>`, `TokenPair` |
| `tests/RCD.AuthAPI.Tests` | `UserService`, `EmailService`, `UserRepository`, `ExceptionHandlingMiddleware`, `AuthController`, `UserManagementController`, all Razor Page models |

Run: `dotnet test RCD.sln`. 100% line coverage on all authored classes. Excluded from coverage: `Program.cs`, `OpenIddictConfig` (need DB integration tests), `ApplicationDbContext`, Razor-compiler-generated `Pages_*` view classes.

## Tech Stack

| Concern | Choice | Where |
|---|---|---|
| OIDC server | OpenIddict 5.7 | AuthAPI |
| User/role management | ASP.NET Core Identity | AuthAPI |
| ORM | EF Core + Npgsql | AuthAPI (Identity + OpenIddict tables only) |
| Data access | Dapper + stored procedures | BackendAPI (all business queries) |
| Migrations | DbUp-PostgreSQL | Infrastructure → BackendAPI |
| Cache / session | Redis | AuthAPI, BackendAPI |
| Logging | Serilog → Seq | All services |
| Tracing | OpenTelemetry → Seq | All services |
| Validation | FluentValidation | BackendAPI |
| API versioning | URL segment (`/api/v{version}/`) | BackendAPI |
| Frontend | Bootstrap 5 + HTMX | RCD.Web |
| Rate limiting | ASP.NET Core built-in | BackendAPI |

## Key Architectural Decisions

- **Auth server hosts its own login UI** — Razor Pages inside RCD.AuthAPI. No separate UI project.
- **Password Grant is intentionally excluded** — All browser clients use Authorization Code + PKCE. This enables SSO and MFA-readiness.
- **Two auth schemes in RCD.AuthAPI** — Identity cookie (default, for Razor Pages + `/connect/authorize`) and OpenIddict validation (explicit, for API controllers via `[Authorize(AuthenticationSchemes = ...)]`).
- **BackendAPI uses Dapper + stored procedures, not EF Core** — All reads and writes go through stored procedures. No raw SQL strings or ORM magic in BackendAPI repositories.
- **RCD.Web stores tokens in HttpOnly encrypted cookies** — Tokens from AuthAPI are never exposed to JavaScript. A `DelegatingHandler` on `HttpClient` attaches the Bearer token to outgoing BackendAPI calls.
- **`Result<T>` pattern** — All service methods return `Result` or `Result<T>` from RCD.Core. No exceptions for business-logic failures.
- **Secrets never in appsettings.json** — Production values supplied via environment variables using `Key__SubKey` notation. Dev values live in `appsettings.Development.json` only.

## OIDC Clients Registered

| client_id | Type | Grant | Used by |
|---|---|---|---|
| `rcd-spa` | Public | Auth Code + PKCE | React SPA |
| `rcd-nextjs` | Confidential | Auth Code + PKCE | Next.js SSR |
| `rcd-mvc` | Confidential | Auth Code + PKCE | .NET MVC (RCD.Web) |
| `rcd-service` | Confidential | Client Credentials | API-to-API |
| `rcd-swagger` | Public | Auth Code + PKCE | Swagger UI |

## Authorization Policies (BackendAPI)

```csharp
options.AddPolicy("ClientOnly",  p => p.RequireRole("Client"));
options.AddPolicy("VendorOnly",  p => p.RequireRole("Vendor"));
options.AddPolicy("BackOffice",  p => p.RequireRole("Admin", "Support"));
```

## Cross-Cutting Concerns

- **Error responses** — RFC 7807 `ProblemDetails` from `ExceptionHandlingMiddleware` in every service.
- **Correlation IDs** — propagated across service calls via request headers.
- **CORS** — locked to known origins listed in `ClientSettings:AllowedOrigins`.
- **Rate limiting** — fixed window per IP on all BackendAPI public endpoints.

## Naming Conventions

| Item | Convention | Example |
|---|---|---|
| DB tables | `snake_case` | `client_orders` |
| Stored procedures | `sp_VerbNoun` | `sp_GetClientById` |
| DB migrations | `YYYYMMDD_Description.sql` | `20250401_InitialSchema.sql` |
| Controllers | PascalCase + suffix | `ClientController.cs` |
| Services | Interface + Impl pair | `IClientService` / `ClientService` |
| Repositories | Interface + Impl pair | `IClientRepository` / `ClientRepository` |
| Request/Response models | Descriptive | `CreateClientRequest`, `ClientResponse` |

## Development Setup

- Update `AppBaseUrl` and `ClientSettings:SwaggerRedirectUri` in `appsettings.Development.json` to match your actual dev port.
- `EmailService` logs links to console/Seq — no mail server needed locally.
- OpenIddict clients are seeded on startup (idempotent — safe to restart).
- Docker Compose is in `/docker/`. Postgres, Redis, and Seq are defined there.
