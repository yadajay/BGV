using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using RCD.AuthAPI.Controllers;
using RCD.AuthAPI.Models.Requests;
using RCD.AuthAPI.Services;
using RCD.Core.Models;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;
using MvcSignInResult  = Microsoft.AspNetCore.Mvc.SignInResult;
using MvcSignOutResult = Microsoft.AspNetCore.Mvc.SignOutResult;

namespace RCD.AuthAPI.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IUserService>                   _svc      = new();
    private readonly Mock<IOpenIddictTokenManager>        _tokens   = new();
    private readonly Mock<IOpenIddictAuthorizationManager> _auths    = new();
    private readonly AuthController                       _ctrl;

    public AuthControllerTests()
    {
        _ctrl = new AuthController(_svc.Object, _tokens.Object, _auths.Object);
    }

    // ── REST: Register ────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ServiceFails_ReturnsBadRequest()
    {
        _svc.Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ReturnsAsync(Result<string>.Fail("Duplicate email"));

        var result = await _ctrl.Register(new RegisterRequest());
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(bad.Value);
    }

    [Fact]
    public async Task Register_Success_ReturnsOk()
    {
        _svc.Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ReturnsAsync(Result<string>.Ok("uid1"));

        var result = await _ctrl.Register(new RegisterRequest());
        Assert.IsType<OkObjectResult>(result);
    }

    // ── REST: ForgotPassword ──────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_AlwaysReturnsOk()
    {
        _svc.Setup(s => s.ForgotPasswordAsync(It.IsAny<string>()))
            .ReturnsAsync(Result.Ok());

        var result = await _ctrl.ForgotPassword(new ForgotPasswordRequest { Email = "x@y.com" });
        Assert.IsType<OkObjectResult>(result);
    }

    // ── REST: ResetPassword ───────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_ServiceFails_ReturnsBadRequest()
    {
        _svc.Setup(s => s.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ReturnsAsync(Result.Fail("Invalid token"));

        var result = await _ctrl.ResetPassword(new ResetPasswordRequest());
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ResetPassword_Success_ReturnsOk()
    {
        _svc.Setup(s => s.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>()))
            .ReturnsAsync(Result.Ok());

        var result = await _ctrl.ResetPassword(new ResetPasswordRequest());
        Assert.IsType<OkObjectResult>(result);
    }

    // ── REST: VerifyEmail ─────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_ServiceFails_ReturnsBadRequest()
    {
        _svc.Setup(s => s.VerifyEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Fail("Bad token"));

        var result = await _ctrl.VerifyEmail(new VerifyEmailRequest { UserId = "uid", Token = "tok" });
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task VerifyEmail_Success_ReturnsOk()
    {
        _svc.Setup(s => s.VerifyEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Ok());

        var result = await _ctrl.VerifyEmail(new VerifyEmailRequest { UserId = "uid", Token = "tok" });
        Assert.IsType<OkObjectResult>(result);
    }

    // ── REST: ApiLogout (authorizationId == null) ─────────────────────────────

    [Fact]
    public async Task ApiLogout_NoAuthorizationId_JustCallsLogout()
    {
        // Principal has no authorization-ID claim → GetAuthorizationId() returns null
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        _svc.Setup(s => s.LogoutAsync()).Returns(Task.CompletedTask);

        _ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var result = await _ctrl.ApiLogout();

        Assert.IsType<OkObjectResult>(result);
        _svc.Verify(s => s.LogoutAsync(), Times.Once);
        _tokens.Verify(t => t.FindByAuthorizationIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── REST: ApiLogout (authorizationId != null, authorization found) ────────

    [Fact]
    public async Task ApiLogout_WithAuthorizationId_RevokesTokensAndAuthorization()
    {
        // "oi_au_id" is the OpenIddict claim type for authorization ID
        const string authId = "auth-123";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("oi_au_id", authId)
        }));

        var fakeToken = new object();
        var fakeAuth  = new object();

        _tokens.Setup(t => t.FindByAuthorizationIdAsync(authId, It.IsAny<CancellationToken>()))
            .Returns(OneItemAsync(fakeToken));
        _tokens.Setup(t => t.TryRevokeAsync(fakeToken, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true));

        _auths.Setup(a => a.FindByIdAsync(authId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<object?>(fakeAuth));
        _auths.Setup(a => a.TryRevokeAsync(fakeAuth, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true));

        _svc.Setup(s => s.LogoutAsync()).Returns(Task.CompletedTask);

        _ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var result = await _ctrl.ApiLogout();

        Assert.IsType<OkObjectResult>(result);
        _tokens.Verify(t => t.TryRevokeAsync(fakeToken, It.IsAny<CancellationToken>()), Times.Once);
        _auths.Verify(a => a.TryRevokeAsync(fakeAuth, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApiLogout_WithAuthorizationId_AuthorizationNotFound_SkipsAuthRevocation()
    {
        const string authId = "auth-456";
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("oi_au_id", authId)
        }));

        _tokens.Setup(t => t.FindByAuthorizationIdAsync(authId, It.IsAny<CancellationToken>()))
            .Returns(EmptyAsync());
        _auths.Setup(a => a.FindByIdAsync(authId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<object?>(result: null));

        _svc.Setup(s => s.LogoutAsync()).Returns(Task.CompletedTask);

        _ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var result = await _ctrl.ApiLogout();

        Assert.IsType<OkObjectResult>(result);
        _auths.Verify(a => a.TryRevokeAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── OIDC: Userinfo ────────────────────────────────────────────────────────

    [Fact]
    public async Task Userinfo_UserNotFound_ReturnsChallenge()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(Claims.Subject, "uid1")
        }, "Bearer"));

        _svc.Setup(s => s.GetUserByIdAsync("uid1"))
            .ReturnsAsync((RCD.AuthAPI.Models.Responses.UserResponse?)null);

        var authService = new Mock<IAuthenticationService>();
        authService
            .Setup(a => a.ChallengeAsync(It.IsAny<HttpContext>(), It.IsAny<string?>(), It.IsAny<AuthenticationProperties?>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(authService.Object);

        _ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal,
                RequestServices = services.BuildServiceProvider()
            }
        };

        var result = await _ctrl.Userinfo();
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Userinfo_UserFound_ReturnsOkWithClaims()
    {
        var user = new RCD.AuthAPI.Models.Responses.UserResponse
        {
            Id    = "uid1",
            Email = "u@x.com"
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(Claims.Subject, "uid1")
        }, "Bearer"));

        _svc.Setup(s => s.GetUserByIdAsync("uid1")).ReturnsAsync(user);

        _ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var result = await _ctrl.Userinfo();
        var ok = Assert.IsType<OkObjectResult>(result);
        var dict = Assert.IsType<Dictionary<string, object>>(ok.Value);
        Assert.Equal("uid1",    dict[Claims.Subject].ToString());
        Assert.Equal("u@x.com", dict[Claims.Email].ToString());
    }

    // ── OIDC: Authorize ───────────────────────────────────────────────────────

    [Fact]
    public async Task Authorize_NoCookieSession_ReturnsChallenge()
    {
        // Set up the OpenIddict server request feature so GetOpenIddictServerRequest() returns non-null
        var oidcRequest = new OpenIddictRequest();
        var transaction = new OpenIddictServerTransaction();
        transaction.Request = oidcRequest;
        var feature = new OpenIddictServerAspNetCoreFeature { Transaction = transaction };

        // Auth service returns failure (no cookie)
        var authService = new Mock<IAuthenticationService>();
        authService
            .Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), IdentityConstants.ApplicationScheme))
            .ReturnsAsync(AuthenticateResult.Fail("No cookie"));

        var services = new ServiceCollection();
        services.AddSingleton(authService.Object);

        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Features.Set(feature);

        _ctrl.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await _ctrl.Authorize();
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task Authorize_CookieOk_ServiceFails_ReturnsForbid()
    {
        var oidcRequest = new OpenIddictRequest();
        var transaction = new OpenIddictServerTransaction();
        transaction.Request = oidcRequest;
        var feature = new OpenIddictServerAspNetCoreFeature { Transaction = transaction };

        var cookiePrincipal = new ClaimsPrincipal(new ClaimsIdentity("cookie"));
        var authService = new Mock<IAuthenticationService>();
        authService
            .Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), IdentityConstants.ApplicationScheme))
            .ReturnsAsync(AuthenticateResult.Success(
                new AuthenticationTicket(cookiePrincipal, IdentityConstants.ApplicationScheme)));

        _svc.Setup(s => s.CreatePrincipalForAuthCodeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(Result<ClaimsPrincipal>.Fail("Account disabled"));

        var services = new ServiceCollection();
        services.AddSingleton(authService.Object);

        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Features.Set(feature);

        _ctrl.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await _ctrl.Authorize();
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Authorize_CookieOk_ServiceOk_ReturnsSignIn()
    {
        var oidcRequest = new OpenIddictRequest();
        var transaction = new OpenIddictServerTransaction();
        transaction.Request = oidcRequest;
        var feature = new OpenIddictServerAspNetCoreFeature { Transaction = transaction };

        var cookiePrincipal = new ClaimsPrincipal(new ClaimsIdentity("cookie"));
        var issuedPrincipal = new ClaimsPrincipal(new ClaimsIdentity("oidc"));

        var authService = new Mock<IAuthenticationService>();
        authService
            .Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), IdentityConstants.ApplicationScheme))
            .ReturnsAsync(AuthenticateResult.Success(
                new AuthenticationTicket(cookiePrincipal, IdentityConstants.ApplicationScheme)));

        _svc.Setup(s => s.CreatePrincipalForAuthCodeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(Result<ClaimsPrincipal>.Ok(issuedPrincipal));

        var services = new ServiceCollection();
        services.AddSingleton(authService.Object);

        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Features.Set(feature);

        _ctrl.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await _ctrl.Authorize();
        var signIn = Assert.IsType<MvcSignInResult>(result);
        Assert.Equal(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, signIn.AuthenticationScheme);
    }

    // ── OIDC: Exchange ────────────────────────────────────────────────────────

    [Fact]
    public async Task Exchange_AuthCodeGrant_ReturnsSignIn()
    {
        var oidcRequest = new OpenIddictRequest { GrantType = GrantTypes.AuthorizationCode };
        var transaction = new OpenIddictServerTransaction();
        transaction.Request = oidcRequest;
        var feature = new OpenIddictServerAspNetCoreFeature { Transaction = transaction };

        var principal = new ClaimsPrincipal(new ClaimsIdentity("oidc"));
        var authService = new Mock<IAuthenticationService>();
        authService
            .Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme))
            .ReturnsAsync(AuthenticateResult.Success(
                new AuthenticationTicket(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)));

        var services = new ServiceCollection();
        services.AddSingleton(authService.Object);

        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Features.Set(feature);

        _ctrl.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await _ctrl.Exchange();
        Assert.IsType<MvcSignInResult>(result);
    }

    [Fact]
    public async Task Exchange_ClientCredentialsGrant_ReturnsSignIn()
    {
        var oidcRequest = new OpenIddictRequest
        {
            GrantType = GrantTypes.ClientCredentials,
            ClientId  = "rcd-service"
        };
        var transaction = new OpenIddictServerTransaction();
        transaction.Request = oidcRequest;
        var feature = new OpenIddictServerAspNetCoreFeature { Transaction = transaction };

        var ctx = new DefaultHttpContext();
        ctx.Features.Set(feature);

        _ctrl.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await _ctrl.Exchange();
        Assert.IsType<MvcSignInResult>(result);
    }

    [Fact]
    public async Task Exchange_RefreshTokenGrant_ServiceFails_ReturnsUnauthorized()
    {
        var oidcRequest = new OpenIddictRequest { GrantType = GrantTypes.RefreshToken };
        var transaction = new OpenIddictServerTransaction();
        transaction.Request = oidcRequest;
        var feature = new OpenIddictServerAspNetCoreFeature { Transaction = transaction };

        var principal = new ClaimsPrincipal(new ClaimsIdentity("oidc"));
        var authService = new Mock<IAuthenticationService>();
        authService
            .Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme))
            .ReturnsAsync(AuthenticateResult.Success(
                new AuthenticationTicket(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)));

        _svc.Setup(s => s.CreatePrincipalForRefreshTokenGrantAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(Result<ClaimsPrincipal>.Fail("User disabled"));

        var services = new ServiceCollection();
        services.AddSingleton(authService.Object);

        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Features.Set(feature);

        _ctrl.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await _ctrl.Exchange();
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Exchange_RefreshTokenGrant_ServiceOk_ReturnsSignIn()
    {
        var oidcRequest = new OpenIddictRequest { GrantType = GrantTypes.RefreshToken };
        var transaction = new OpenIddictServerTransaction();
        transaction.Request = oidcRequest;
        var feature = new OpenIddictServerAspNetCoreFeature { Transaction = transaction };

        var principal    = new ClaimsPrincipal(new ClaimsIdentity("oidc"));
        var newPrincipal = new ClaimsPrincipal(new ClaimsIdentity("oidc"));
        var authService = new Mock<IAuthenticationService>();
        authService
            .Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme))
            .ReturnsAsync(AuthenticateResult.Success(
                new AuthenticationTicket(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)));

        _svc.Setup(s => s.CreatePrincipalForRefreshTokenGrantAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(Result<ClaimsPrincipal>.Ok(newPrincipal));

        var services = new ServiceCollection();
        services.AddSingleton(authService.Object);

        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Features.Set(feature);

        _ctrl.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await _ctrl.Exchange();
        Assert.IsType<MvcSignInResult>(result);
    }

    [Fact]
    public async Task Exchange_UnsupportedGrant_ReturnsBadRequest()
    {
        var oidcRequest = new OpenIddictRequest { GrantType = "urn:custom:grant" };
        var transaction = new OpenIddictServerTransaction();
        transaction.Request = oidcRequest;
        var feature = new OpenIddictServerAspNetCoreFeature { Transaction = transaction };

        var ctx = new DefaultHttpContext();
        ctx.Features.Set(feature);

        _ctrl.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await _ctrl.Exchange();
        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── OIDC: Logout ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_CallsLogoutServiceAndReturnsSignOut()
    {
        _svc.Setup(s => s.LogoutAsync()).Returns(Task.CompletedTask);

        var authService = new Mock<IAuthenticationService>();
        authService
            .Setup(a => a.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string?>(), It.IsAny<AuthenticationProperties?>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(authService.Object);

        _ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() }
        };

        var result = await _ctrl.Logout();

        _svc.Verify(s => s.LogoutAsync(), Times.Once);
        Assert.IsType<MvcSignOutResult>(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<object> OneItemAsync(object item)
    {
        yield return item;
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<object> EmptyAsync()
    {
        await Task.CompletedTask;
        yield break;
    }
}
