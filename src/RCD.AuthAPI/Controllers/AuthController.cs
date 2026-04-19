using RCD.AuthAPI.Models.Requests;
using RCD.AuthAPI.Services;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace RCD.AuthAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    // ── OIDC Standard Endpoints ───────────────────────────────────────────────
    // These routes use the '~/' prefix to escape the [Route("api/v1/auth")] prefix
    // and sit at the paths OpenIddict expects (/<endpoint> not /api/v1/auth/<endpoint>).

    /// <summary>
    /// OIDC Authorization endpoint (<c>GET/POST /connect/authorize</c>).
    /// <para>
    /// Flow:
    /// <list type="number">
    ///   <item>Check whether the user has a valid Identity cookie (the auth server session).</item>
    ///   <item>If no cookie: redirect to <c>/Account/Login</c> with the full authorize URL as
    ///         <c>ReturnUrl</c> so after login the browser returns here automatically.</item>
    ///   <item>If cookie present: build an OpenIddict <see cref="ClaimsPrincipal"/> and call
    ///         <c>SignIn</c> with the OpenIddict server scheme, causing OpenIddict to issue an
    ///         authorization code and redirect to the client's <c>redirect_uri</c>.</item>
    /// </list>
    /// </para>
    /// This is the SSO pivot point: every client redirects here. Users who already have a session
    /// skip the login form entirely — they are redirected back to the client with a fresh code.
    /// </summary>
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict server request not found.");

        // Attempt cookie authentication using the Identity application scheme.
        // This is the auth server's own session — separate from the Bearer tokens issued to clients.
        var cookieResult = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        if (!cookieResult.Succeeded)
        {
            // No session — redirect to the login page, preserving the full authorize URL
            // (including client_id, scope, code_challenge, state, etc.) as the return URL.
            var returnUrl = Request.PathBase + Request.Path + QueryString.Create(
                Request.HasFormContentType
                    ? Request.Form.ToList()
                    : Request.Query.ToList());

            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties { RedirectUri = returnUrl });
        }

        // User is authenticated — build the OpenIddict principal and issue the authorization code.
        var result = await _userService.CreatePrincipalForAuthCodeAsync(
            cookieResult.Principal!, request.GetScopes());

        if (!result.Success)
        {
            // Return an OIDC-compliant access_denied error to the client.
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.AccessDenied,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = result.Error
                }));
        }

        // SignIn with the OpenIddict server scheme causes OpenIddict to:
        //   1. Persist the authorization record.
        //   2. Generate a short-lived authorization code.
        //   3. Redirect to the client's redirect_uri with ?code=...&state=...
        return SignIn(result.Data!, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// OIDC Token endpoint (<c>POST /connect/token</c>).
    /// <para>
    /// Handles three grant types:
    /// <list type="bullet">
    ///   <item><b>Authorization Code</b> — client exchanges the code received from
    ///         <c>/connect/authorize</c> for tokens. OpenIddict has already validated the code
    ///         and PKCE verifier; we re-use the stored principal and return tokens.</item>
    ///   <item><b>Client Credentials</b> — machine-to-machine. No user; principal is built from
    ///         the client ID. Used by background services calling protected APIs.</item>
    ///   <item><b>Refresh Token</b> — client exchanges a refresh token for a new access token.
    ///         The user is re-validated (IsActive, CanSignIn) before a new principal is issued
    ///         so that disabling an account revokes future token renewals.</item>
    /// </list>
    /// </para>
    /// </summary>
    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [AllowAnonymous]
    [Produces("application/json")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsAuthorizationCodeGrantType() || request.IsDeviceCodeGrantType())
        {
            // OpenIddict already validated the code and PKCE verifier.
            // Retrieve the principal it stored during /connect/authorize and re-apply scopes.
            var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = authResult.Principal!;
            principal.SetScopes(request.GetScopes());
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsClientCredentialsGrantType())
        {
            // No user involved — principal represents the client application itself.
            // The subject claim is set to the client_id; all other claims are omitted.
            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            identity.AddClaim(Claims.Subject, request.ClientId!,
                OpenIddictConstants.Destinations.AccessToken);

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(request.GetScopes());
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsRefreshTokenGrantType())
        {
            // Retrieve the principal stored in the refresh token by OpenIddict,
            // then re-validate the user to catch any account changes since original login.
            var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var result = await _userService.CreatePrincipalForRefreshTokenGrantAsync(
                authResult.Principal!, request.GetScopes());

            if (!result.Success)
                return Unauthorized(new OpenIddictResponse
                {
                    Error = Errors.InvalidGrant,
                    ErrorDescription = result.Error
                });

            return SignIn(result.Data!, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest(new OpenIddictResponse
        {
            Error = Errors.UnsupportedGrantType,
            ErrorDescription = "The specified grant type is not supported."
        });
    }

    /// <summary>
    /// OIDC UserInfo endpoint (<c>GET/POST /connect/userinfo</c>).
    /// <para>
    /// Returns claims about the currently authenticated user. Protected by the OpenIddict
    /// server scheme — the caller must present a valid access token in the Authorization header.
    /// Returns a minimal claim set (sub, email, name). Extend as needed per OIDC spec.
    /// </para>
    /// </summary>
    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo"), HttpPost("~/connect/userinfo"), IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Userinfo()
    {
        var userId = User.GetClaim(Claims.Subject);
        var user = await _userService.GetUserByIdAsync(userId!);

        // Challenge causes OpenIddict to return a 401 with WWW-Authenticate header.
        if (user == null)
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        return Ok(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = user.Id,
            [Claims.Email]   = user.Email ?? string.Empty,
            [Claims.Name]    = user.Email ?? string.Empty
        });
    }

    /// <summary>
    /// OIDC End-Session endpoint (<c>GET/POST /connect/logout</c>).
    /// <para>
    /// Called by client applications to initiate logout. Signs out the Identity cookie
    /// (ending the SSO session) then delegates to OpenIddict which validates the
    /// <c>id_token_hint</c> and <c>post_logout_redirect_uri</c> and redirects the user back
    /// to the client. All active SSO sessions for the same user are terminated.
    /// </para>
    /// </summary>
    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        // Clear the auth server cookie session first, then let OpenIddict handle the redirect.
        await _userService.LogoutAsync();

        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }

    // ── REST API Endpoints ────────────────────────────────────────────────────
    // These endpoints are called programmatically (not by browsers during OIDC flows).
    // They are protected by OpenIddict token validation where authentication is required.

    /// <summary>
    /// Registers a new user account.
    /// Sends an email verification link as a side-effect.
    /// No authentication required — used from registration forms or onboarding APIs.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _userService.RegisterAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "User registered successfully. Check your email to verify your account." });
    }

    /// <summary>
    /// API logout — signs the calling user out of the auth server cookie session.
    /// Requires a valid Bearer access token. Typically used by programmatic clients;
    /// browser-based clients should use <c>GET /connect/logout</c> instead for full OIDC logout.
    /// </summary>
    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<IActionResult> ApiLogout()
    {
        await _userService.LogoutAsync();
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Initiates the password-reset flow. Generates a token and dispatches it via email.
    /// Always returns 200 to prevent account enumeration — the response is identical whether
    /// the email address exists or not.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _userService.ForgotPasswordAsync(request.Email);
        return Ok(new { message = "If that email is registered, a reset link has been sent." });
    }

    /// <summary>
    /// Resets the user's password using the token from the email link.
    /// Requires <c>Email</c>, <c>Token</c> (base64url from link), <c>NewPassword</c>, and
    /// <c>ConfirmPassword</c> in the request body.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _userService.ResetPasswordAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Password reset successfully" });
    }

    /// <summary>
    /// Confirms the user's email address using the token embedded in the verification link.
    /// Requires both <c>UserId</c> and <c>Token</c> (base64url) in the request body.
    /// </summary>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await _userService.VerifyEmailAsync(request.UserId, request.Token);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Email verified successfully" });
    }
}
