using BGV.AuthAPI.Models.Requests;
using BGV.AuthAPI.Models.Responses;
using BGV.AuthAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace BGV.AuthAPI.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ITokenService _tokenService;
    // private readonly IMfaService _mfaService;

    public AuthController(IUserService userService, ITokenService tokenService/*, IMfaService mfaService*/)
    {
        _userService = userService;
        _tokenService = tokenService;
        // _mfaService = mfaService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _userService.RegisterAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "User registered successfully" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _userService.LoginAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        var tokens = await _tokenService.GenerateTokensAsync(result.Data);
        return Ok(new LoginResponse { AccessToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken });
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _tokenService.RefreshTokenAsync(request.RefreshToken);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new RefreshTokenResponse { AccessToken = result.Data.AccessToken, RefreshToken = result.Data.RefreshToken });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await _tokenService.RevokeTokenAsync(request.RefreshToken);
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