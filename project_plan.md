# 🚀 Startup SaaS Platform — Project Plan & Architecture

> **Stack:** .NET 8+, PostgreSQL, Dapper, Docker  
> **Philosophy:** Simple, clean, layered architecture. No CQRS, no MediatR, no microservices unless justified.

---

## 1. Solution Overview

```
BGV.sln
├── src/
│   ├── BGV.AuthAPI          # Authentication & Authorization service
│   ├── BGV.BackendAPI       # Core business API
│   ├── BGV.Web              # ASP.NET Core MVC Frontend
│   ├── BGV.Core             # Shared models, interfaces, constants
│   └── BGV.Infrastructure   # Shared DB helpers, utilities
├── docker/
│   ├── docker-compose.yml
│   ├── docker-compose.override.yml (dev)
│   └── docker-compose.prod.yml
├── db/
│   ├── migrations/
│   └── stored_procedures/
└── docs/
    └── (this document and related docs)
```

---

## 2. Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Docker Network                           │
│                                                                 │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐  │
│  │  BGV   │───▶│  AuthAPI     │    │   BackendAPI     │  │
│  │  Web (MVC)   │───▶│  :5001       │    │   :5002          │  │
│  │  :8080       │───▶│              │    │                  │  │
│  └──────────────┘    └──────┬───────┘    └──────┬───────────┘  │
│                             │                   │              │
│                    ┌────────▼───────────────────▼──────────┐   │
│                    │          PostgreSQL :5432              │   │
│                    └───────────────────────────────────────┘   │
│                                                                 │
│  ┌──────────────┐    ┌──────────────┐                          │
│  │    Redis     │    │    Seq /     │                          │
│  │  (Cache +    │    │  OpenTel     │                          │
│  │   Sessions)  │    │  (Logging)   │                          │
│  └──────────────┘    └──────────────┘                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Component 1 — Auth API (`BGV.AuthAPI`)

### Purpose
Centralized identity service for all apps and APIs. Extensible for SSO and MFA.

### Technology Choices
| Concern | Choice | Reason |
|---|---|---|
| Auth Framework | **OpenIddict** | Lightweight, flexible, .NET-native, supports OAuth2/OIDC, SSO |
| Token Storage | PostgreSQL | Consistent with stack |
| Password Hashing | ASP.NET Identity (hash only, no full Identity UI) | Battle-tested |
| MFA | TOTP (Time-based OTP via `OtpNet`) | Standard, app-compatible |
| Cache/Session | Redis | Fast token validation |

### Key Endpoints

```
POST   /api/v1/auth/register
POST   /api/v1/auth/login
POST   /api/v1/auth/refresh-token
POST   /api/v1/auth/logout
POST   /api/v1/auth/forgot-password
POST   /api/v1/auth/reset-password
POST   /api/v1/auth/verify-email
POST   /api/v1/auth/mfa/enable
POST   /api/v1/auth/mfa/verify

GET    /api/v1/users                  (Admin)
GET    /api/v1/users/{id}
PUT    /api/v1/users/{id}
DELETE /api/v1/users/{id}
POST   /api/v1/users/{id}/roles
DELETE /api/v1/users/{id}/roles/{role}

# OAuth2 / OIDC (OpenIddict standard endpoints)
GET/POST  /connect/authorize
POST      /connect/token
POST      /connect/logout
GET       /.well-known/openid-configuration
```

### Project Structure

```
BGV.AuthAPI/
├── Controllers/
│   ├── AuthController.cs
│   └── UserManagementController.cs
├── Services/
│   ├── ITokenService.cs / TokenService.cs
│   ├── IUserService.cs / UserService.cs
│   └── IMfaService.cs / MfaService.cs
├── Repositories/
│   ├── IUserRepository.cs / UserRepository.cs
│   └── IRoleRepository.cs / RoleRepository.cs
├── Models/
│   ├── Requests/
│   └── Responses/
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
├── Configuration/
│   └── OpenIddictConfig.cs
└── Program.cs
```

### Security Features
- JWT Access Tokens (short-lived: 15 min)
- Refresh Tokens (long-lived: 7 days, rotated on use)
- Token revocation via Redis allowlist
- Account lockout after N failed attempts
- Email verification flow
- TOTP-based MFA (Phase 2)
- SSO via OpenIddict (Phase 2)

---

## 4. Component 2 — Backend API (`BGV.BackendAPI`)

### Purpose
Core business logic API. Starts as a single project with logical separation by domain (Client, Vendor, BackOffice). Can be split later.

### Technology Choices
| Concern | Choice |
|---|---|
| Framework | ASP.NET Core 8 Minimal API / Controllers (Controllers preferred for clarity) |
| Auth | JWT Bearer — validates against AuthAPI |
| Logging | Serilog → Seq (dev) / Elasticsearch or file (prod) |
| Tracing | OpenTelemetry → Jaeger or Console |
| Rate Limiting | ASP.NET Core built-in Rate Limiting middleware |
| Validation | FluentValidation |
| DB | Dapper + PostgreSQL stored procedures |
| Caching | IMemoryCache / IDistributedCache (Redis) |
| Docs | Swagger/OpenAPI with versioning |

### Project Structure

```
BGV.BackendAPI/
├── Controllers/
│   ├── V1/
│   │   ├── Client/
│   │   │   └── ClientController.cs
│   │   ├── Vendor/
│   │   │   └── VendorController.cs
│   │   └── BackOffice/
│   │       └── BackOfficeController.cs
├── Services/
│   ├── Client/
│   ├── Vendor/
│   └── BackOffice/
├── Repositories/
│   ├── Client/
│   ├── Vendor/
│   └── BackOffice/
├── Models/
│   ├── Requests/
│   ├── Responses/
│   └── Entities/
├── Middleware/
│   ├── ExceptionHandlingMiddleware.cs
│   └── RequestLoggingMiddleware.cs
├── Filters/
│   └── ValidationFilter.cs
├── Configuration/
│   ├── SwaggerConfig.cs
│   ├── RateLimitConfig.cs
│   └── SerilogConfig.cs
└── Program.cs
```

### Key Cross-Cutting Concerns

**Versioning**
```csharp
// URL segment versioning
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class ClientController : ControllerBase { }
```

**Rate Limiting** (built-in, .NET 8)
```csharp
// Fixed window per IP
builder.Services.AddRateLimiter(options => {
    options.AddFixedWindowLimiter("fixed", opt => {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
    });
});
```

**Logging with Serilog**
- Structured JSON logs
- Request/response logging middleware
- Correlation ID propagation across services

**OpenTelemetry Tracing**
- Trace IDs propagated to DB calls
- Exportable to Jaeger / Zipkin

**Global Exception Handler**
- Returns RFC 7807 `ProblemDetails`
- Hides stack traces in production

**Authorization Policies**
```csharp
builder.Services.AddAuthorization(options => {
    options.AddPolicy("ClientOnly", p => p.RequireRole("Client"));
    options.AddPolicy("VendorOnly", p => p.RequireRole("Vendor"));
    options.AddPolicy("BackOffice", p => p.RequireRole("Admin", "Support"));
});
```

---

## 5. Component 3 — Web Frontend (`BGV.Web`)

### Purpose
ASP.NET Core MVC application. Mobile-first, AJAX-driven, component architecture.

### Technology Choices
| Concern | Choice |
|---|---|
| Framework | ASP.NET Core 8 MVC |
| CSS Framework | Bootstrap 5 (mobile-first) |
| JS | Vanilla JS + HTMX **or** Alpine.js (lightweight, no SPA overhead) |
| Ajax Rendering | HTMX (preferred) or Fetch API with Partial Views |
| Component Pattern | ViewComponents + Tag Helpers + Partial Views |
| Auth | Cookie-based session backed by AuthAPI JWT |
| Validation | jQuery Unobtrusive Validation + Server-side |
| Bundling | LibMan or npm + rollup (keep it simple) |

> **Why HTMX?** Enables AJAX page updates with minimal JS, works perfectly with ASP.NET MVC Partial Views, keeps the stack .NET-native.

### Project Structure

```
BGV.Web/
├── Controllers/
│   ├── AccountController.cs
│   ├── DashboardController.cs
│   ├── ClientController.cs
│   └── BackOfficeController.cs
├── Views/
│   ├── Shared/
│   │   ├── _Layout.cshtml
│   │   ├── _NavBar.cshtml
│   │   ├── _Sidebar.cshtml
│   │   └── _Alerts.cshtml
│   ├── Dashboard/
│   └── Client/
├── ViewComponents/
│   ├── AlertViewComponent.cs
│   ├── PaginationViewComponent.cs
│   ├── DataTableViewComponent.cs
│   └── StatsCardViewComponent.cs
├── Services/
│   ├── IAuthService.cs / AuthService.cs       (calls AuthAPI)
│   └── IApiService.cs / ApiService.cs         (calls BackendAPI)
├── Helpers/
│   └── HttpClientHelper.cs
├── Middleware/
│   └── AuthenticationMiddleware.cs
├── wwwroot/
│   ├── css/
│   ├── js/
│   └── lib/
└── Program.cs
```

### AJAX / HTMX Pattern
```html
<!-- Page loads fast, table loads async -->
<div id="client-table"
     hx-get="/Client/TablePartial"
     hx-trigger="load"
     hx-indicator="#spinner">
  <div id="spinner" class="htmx-indicator">Loading...</div>
</div>
```

```csharp
// Controller returns partial view for AJAX calls
[HttpGet("TablePartial")]
public IActionResult TablePartial([FromQuery] ClientFilterModel filter)
{
    var data = await _apiService.GetClientsAsync(filter);
    return PartialView("_ClientTable", data);
}
```

### Auth Flow (Web ↔ AuthAPI)
1. User submits login form → Web calls AuthAPI `/auth/login`
2. AuthAPI returns `access_token` + `refresh_token`
3. Web stores tokens in **HttpOnly encrypted cookies** (never localStorage)
4. Outgoing API calls attach Bearer token via `HttpClient` delegating handler
5. On 401 → auto-refresh via refresh token → transparent to user

---

## 6. Component 4 — Database Layer

### PostgreSQL with Dapper + Stored Procedures

**Design Rules:**
- All reads via stored procedures
- All writes (insert/update/delete) via stored procedures
- Dapper for mapping, no ORM magic
- Migrations tracked via **DbUp** (lightweight SQL migration runner)

### Repository Pattern

```csharp
public class ClientRepository : IClientRepository
{
    private readonly IDbConnectionFactory _db;

    public async Task<IEnumerable<ClientDto>> GetAllAsync(ClientFilter filter)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<ClientDto>(
            "sp_GetClients",
            filter,
            commandType: CommandType.StoredProcedure
        );
    }
}
```

### Shared Infrastructure (`BGV.Infrastructure`)

```
BGV.Infrastructure/
├── Data/
│   ├── IDbConnectionFactory.cs
│   └── NpgsqlConnectionFactory.cs
├── Repositories/
│   └── BaseRepository.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

### Database Schema (Initial)

```sql
-- Core tables
users               (id, email, password_hash, is_active, created_at ...)
user_roles          (user_id, role_id)
roles               (id, name, description)
refresh_tokens      (id, user_id, token_hash, expires_at, revoked_at)

-- Business tables (examples)
clients             (id, name, email, status, created_at, created_by ...)
vendors             (id, name, email, status, created_at, created_by ...)
audit_logs          (id, user_id, action, entity, entity_id, timestamp, ip)
```

---

## 7. Docker Setup

### `docker-compose.yml` (Base)

```yaml
version: '3.9'

services:
  authapi:
    image: BGV-authapi
    build: ./src/BGV.AuthAPI
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__Default=${DB_CONNECTION}
      - Jwt__Secret=${JWT_SECRET}
    ports:
      - "5001:8080"
    depends_on:
      - postgres
      - redis

  backendapi:
    image: BGV-backendapi
    build: ./src/BGV.BackendAPI
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__Default=${DB_CONNECTION}
      - AuthAPI__BaseUrl=http://authapi:8080
    ports:
      - "5002:8080"
    depends_on:
      - postgres
      - authapi

  web:
    image: BGV-web
    build: ./src/BGV.Web
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - AuthAPI__BaseUrl=http://authapi:8080
      - BackendAPI__BaseUrl=http://backendapi:8080
    ports:
      - "8080:8080"
    depends_on:
      - authapi
      - backendapi

  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: BGV_db
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  seq:
    image: datalust/seq:latest
    environment:
      ACCEPT_EULA: Y
    ports:
      - "5341:80"

volumes:
  pgdata:
```

### Dockerfile Template (each service)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY ["BGV.AuthAPI/BGV.AuthAPI.csproj", "BGV.AuthAPI/"]
COPY ["BGV.Core/BGV.Core.csproj", "BGV.Core/"]
COPY ["BGV.Infrastructure/BGV.Infrastructure.csproj", "BGV.Infrastructure/"]
RUN dotnet restore "BGV.AuthAPI/BGV.AuthAPI.csproj"
COPY . .
RUN dotnet build "BGV.AuthAPI/BGV.AuthAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BGV.AuthAPI/BGV.AuthAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BGV.AuthAPI.dll"]
```

---

## 8. Cross-Cutting Shared Projects

### `BGV.Core` (no dependencies)
- DTOs / ViewModels shared between projects
- Common interfaces (`ICurrentUser`, `IAuditableEntity`)
- Enums, constants
- Result/Error pattern: `Result<T>` wrapper

### `BGV.Infrastructure` (depends on Core)
- `NpgsqlConnectionFactory`
- Redis cache helper
- `HttpClientFactory` wrappers
- DbUp migration runner integration
- Audit logging service

---

## 9. Security Checklist

- [x] HTTPS enforced (HSTS headers)
- [x] HttpOnly + Secure + SameSite cookies
- [x] JWT with short expiry + refresh rotation
- [x] Token revocation via Redis
- [x] SQL injection prevention (Dapper parameterized)
- [x] CORS locked to known origins
- [x] Rate limiting on all public endpoints
- [x] Input validation (FluentValidation + model binding)
- [x] Audit log for all sensitive operations
- [x] Secrets via environment variables / Docker secrets (never in code)
- [x] No stack traces in production responses
- [x] Role-based + policy-based authorization

---

## 10. Development Phases

### Phase 1 — Foundation (Weeks 1–3)
- [ ] Solution setup, shared projects (`Core`, `Infrastructure`)
- [ ] Docker Compose (dev environment)
- [ ] PostgreSQL schema + DbUp migrations
- [ ] Auth API: Register, Login, JWT, Refresh Token
- [ ] Backend API: Project skeleton, middleware, Swagger
- [ ] Web: Layout, Login/Register pages, Auth flow

### Phase 2 — Core Features (Weeks 4–7)
- [ ] User management (CRUD, roles)
- [ ] Client module (BackendAPI + Web)
- [ ] Vendor module (BackendAPI + Web)
- [ ] Logging (Serilog → Seq), Tracing (OpenTelemetry)
- [ ] Rate limiting, global error handling

### Phase 3 — Admin & Hardening (Weeks 8–10)
- [ ] BackOffice module
- [ ] Audit logs UI
- [ ] Dashboard with HTMX-loaded widgets
- [ ] MFA (TOTP)
- [ ] Full security review

### Phase 4 — Production Readiness (Weeks 11–12)
- [ ] Docker Compose production config
- [ ] Health check endpoints (`/health`)
- [ ] CI/CD pipeline (GitHub Actions)
- [ ] Environment-specific config (dev/staging/prod)
- [ ] SSO (OpenIddict OIDC flows)

---

## 11. NuGet Package Reference

| Package | Used In |
|---|---|
| `OpenIddict.AspNetCore` | AuthAPI |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | BackendAPI |
| `Dapper` | Infrastructure |
| `Npgsql` | Infrastructure |
| `DbUp-PostgreSQL` | Infrastructure (migrations) |
| `Serilog.AspNetCore` | All APIs |
| `Serilog.Sinks.Seq` | All APIs |
| `OpenTelemetry.Extensions.Hosting` | All APIs |
| `FluentValidation.AspNetCore` | BackendAPI |
| `Asp.Versioning.Mvc` | BackendAPI |
| `StackExchange.Redis` | AuthAPI, BackendAPI |
| `OtpNet` | AuthAPI (MFA) |
| `Swashbuckle.AspNetCore` | BackendAPI |
| `htmx` (CDN/npm) | Web |
| `Bootstrap 5` (CDN/npm) | Web |

---

## 12. Folder / File Naming Conventions

| Item | Convention | Example |
|---|---|---|
| Projects | PascalCase | `BGV.AuthAPI` |
| Controllers | PascalCase + suffix | `ClientController.cs` |
| Services | Interface + Impl | `IClientService` / `ClientService` |
| Repositories | Interface + Impl | `IClientRepository` / `ClientRepository` |
| DTOs / Models | Descriptive | `CreateClientRequest`, `ClientResponse` |
| DB tables | snake_case | `client_orders` |
| Stored Procs | `sp_VerbNoun` | `sp_GetClientById` |
| Migrations | `YYYYMMDD_Description` | `20250401_InitialSchema.sql` |

---

## 13. Docs
- pandoc -s project_plan.md -o docs/project_plan.docx

---

*Document version: 1.0 — Created April 2026*
