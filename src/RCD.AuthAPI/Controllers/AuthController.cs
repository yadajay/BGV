using RCD.AuthAPI.Models.Requests;
using RCD.AuthAPI.Models.Responses;
using RCD.AuthAPI.Services;
using RCD.Infrastructure.Db;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace RCD.AuthAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    /// <param name="userService">The user service.</param>
    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Returns claims about the authenticated user (OIDC UserInfo endpoint).
    /// </summary>
    /// <returns>A JSON object containing user claims.</returns>
    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo"), HttpPost("~/connect/userinfo"), IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Userinfo()
    {
        // The 'sub' claim is used to identify the user.
        var userId = User.GetClaim(OpenIddictConstants.Claims.Subject);
        var user = await _userService.GetUserByIdAsync(userId!);
        
        if (user == null) return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        // The OpenIddict validation middleware has already validated the token.
        // We can return the claims directly from the User object or re-fetch for fresh data.
        return Ok(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = user.Id,
            [Claims.Email] = user.Email ?? string.Empty,
            [Claims.Name] = user.Email ?? string.Empty
        });
    }

    /// <summary>
    /// Registers a new user in the system.
    /// </summary>
    /// <param name="request">The registration details.</param>
    /// <returns>An OK result if successful; otherwise, a BadRequest.</returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _userService.RegisterAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.Error });
        return Ok(new { message = "User registered successfully" });
    }

    /// <summary>
    /// Handles OIDC token exchange requests (Password and Refresh Token grants).
    /// This is the standard OIDC token endpoint.
    /// </summary>
    /// <returns>A sign-in result with tokens if successful; otherwise, an error response.</returns>
    // Use the '~' to bypass the [Route("api/v1/auth")] for standard OIDC endpoints
    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [AllowAnonymous]
    [Produces("application/json")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ?? 
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        ClaimsPrincipal principal;

        if (request.IsPasswordGrantType())
        {
            var result = await _userService.CreatePrincipalForPasswordGrantAsync(request.Username!, request.Password!, request.GetScopes());
            if (!result.Success) 
            {
                return Unauthorized(new OpenIddictResponse { 
                    Error = Errors.InvalidGrant, 
                    ErrorDescription = result.Error 
                });
            }
            principal = result.Data;
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsRefreshTokenGrantType())
        {
            // Use the authentication result from the refresh token
            var authResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var result = await _userService.CreatePrincipalForRefreshTokenGrantAsync(authResult.Principal!, request.GetScopes());
            if (!result.Success) return Unauthorized(new { error = result.Error });
            principal = result.Data;
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest(new OpenIddictResponse
        {
            Error = Errors.UnsupportedGrantType,
            ErrorDescription = "The specified grant type is not supported."
        });
    }

    /// <summary>
    /// Handles OIDC end-session/logout requests.
    /// </summary>
    /// <returns>A sign-out result triggering redirection to the post-logout URI.</returns>
    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    [AllowAnonymous] // Logout usually needs to be accessible even if the local cookie expired
    public async Task<IActionResult> Logout()
    {
        await _userService.LogoutAsync();
        // Ask OpenIddict to clear the user's tokens and redirect to the post_logout_redirect_uri.
        // OpenIddict validates the redirect URI against the client's registered URIs.
        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties
            {
                RedirectUri = "/"
            });
    }

    /// <summary>
    /// Performs a programmatic API logout by revoking the current session.
    /// </summary>
    /// <returns>An OK result confirming logout.</returns>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> ApiLogout()
    {
        await _userService.LogoutAsync();
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Initiates the password reset process by generating a token.
    /// </summary>
    /// <param name="request">The request containing the user's email.</param>
    /// <returns>An OK result confirming the email was sent.</returns>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _userService.ForgotPasswordAsync(request.Email);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Password reset email sent" });
    }

    /// <summary>
    /// Resets a user's password using a valid reset token.
    /// </summary>
    /// <param name="request">The reset details including the token and new password.</param>
    /// <returns>An OK result if the password was reset.</returns>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _userService.ResetPasswordAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Password reset successfully" });
    }

    /// <summary>
    /// Verifies a user's email address using a verification token.
    /// </summary>
    /// <param name="request">The request containing the verification token.</param>
    /// <returns>An OK result if the email was verified.</returns>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await _userService.VerifyEmailAsync(request.Token);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Email verified successfully" });
    }

    // [HttpPost("mfa/enable")]
    // [Authorize]
    // public async Task<IActionResult> EnableMfa()
    // {
    //     var userId = User.FindFirst("sub")?.Value;
    //     if (string.IsNullOrEmpty(userId))
    //         return Unauthorized();

    //     var result = await _mfaService.EnableMfaAsync(userId);
    //     if (!result.Success)
    //         return BadRequest(new { message = result.Error });

    //     return Ok(new EnableMfaResponse { Secret = result.Data.Secret, QrCodeUrl = result.Data.QrCodeUrl });
    // }

    // [HttpPost("mfa/verify")]
    // [Authorize]
    // public async Task<IActionResult> VerifyMfa([FromBody] VerifyMfaRequest request)
    // {
    //     var userId = User.FindFirst("sub")?.Value;
    //     if (string.IsNullOrEmpty(userId))
    //         return Unauthorized();

    //     var result = await _mfaService.VerifyMfaAsync(userId, request.Code);
    //     if (!result.Success)
    //         return BadRequest(new { message = result.Error });

    //     return Ok(new { message = "MFA verified successfully" });
    // }
}