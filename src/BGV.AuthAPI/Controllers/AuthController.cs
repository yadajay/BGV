using BGV.AuthAPI.Models.Requests;
using BGV.AuthAPI.Models.Responses;
using BGV.AuthAPI.Services;
using BGV.Infrastructure.Db;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BGV.AuthAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo"), HttpPost("~/connect/userinfo"), IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> Userinfo()
    {
        // The 'sub' claim is used to identify the user.
        var userId = User.GetClaim(OpenIddictConstants.Claims.Subject);
        var user = await _userService.GetUserByIdAsync(userId!);
        
        if (user == null) return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [OpenIddictConstants.Claims.Subject] = user.Id,
            [OpenIddictConstants.Claims.Email] = user.Email!
        };

        return Ok(claims);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _userService.RegisterAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.Error });
        return Ok(new { message = "User registered successfully" });
    }

    // Use the '~' to bypass the [Route("api/v1/auth")] for standard OIDC endpoints
    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    [AllowAnonymous]
    [Produces("application/json")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Exchange(
        [FromForm] string grant_type,
        [FromForm] string? username,
        [FromForm] string? password,
        [FromForm] string? refresh_token,
        [FromForm] string? scope,
        [FromForm] string? client_id,
        [FromForm] string? client_secret)
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

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _userService.LogoutAsync();
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _userService.ForgotPasswordAsync(request.Email);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Password reset email sent" });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _userService.ResetPasswordAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Password reset successfully" });
    }

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