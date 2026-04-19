using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;
using RCD.AuthAPI.Models.Requests;
using RCD.AuthAPI.Models.Responses;
using RCD.AuthAPI.Repositories;
using RCD.Core.Models;
using RCD.Infrastructure.Db;
using System.Security.Claims;
using System.Text;

namespace RCD.AuthAPI.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;

    public UserService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IUserRepository userRepository,
        IEmailService emailService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userRepository = userRepository;
        _emailService = emailService;
    }

    /// <inheritdoc />
    public async Task<Result<string>> RegisterAsync(RegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword)
            return Result<string>.Fail("Passwords do not match");

        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return Result<string>.Fail(string.Join(", ", result.Errors.Select(e => e.Description)));

        // Generate an email-confirmation token, base64url-encode it for safe URL embedding,
        // and dispatch the verification link. Email confirmation is not enforced on sign-in
        // (RequireConfirmedEmail = false) but the infrastructure is in place for future use.
        var rawToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
        await _emailService.SendEmailVerificationAsync(user.Email!, user.Id, encodedToken);

        return Result<string>.Ok(user.Id);
    }

    /// <inheritdoc />
    public async Task<Result> LoginAsync(string email, string password, bool rememberMe)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            return Result.Fail("Invalid email or password");

        if (!user.IsActive)
            return Result.Fail("Account is disabled. Please contact support.");

        // lockoutOnFailure: true increments the failed-attempt counter and eventually
        // locks the account, protecting against brute-force attacks.
        var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return Result.Fail("Account is temporarily locked. Try again later.");

        if (!result.Succeeded)
            return Result.Fail("Invalid email or password");

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> ForgotPasswordAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            return Result.Ok(); // Don't reveal whether the email address is registered

        var rawToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
        await _emailService.SendPasswordResetEmailAsync(user.Email!, user.Id, encodedToken);

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
            return Result.Fail("Passwords do not match");

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Result.Fail("Invalid request"); // Vague response avoids account enumeration

        // The token arrives base64url-encoded from the email link; decode before passing to Identity.
        var rawToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        var result = await _userManager.ResetPasswordAsync(user, rawToken, request.NewPassword);
        if (!result.Succeeded)
            return Result.Fail(string.Join(", ", result.Errors.Select(e => e.Description)));

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> VerifyEmailAsync(string userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Fail("Invalid request");

        var rawToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        var result = await _userManager.ConfirmEmailAsync(user, rawToken);
        if (!result.Succeeded)
            return Result.Fail(string.Join(", ", result.Errors.Select(e => e.Description)));

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Fail("User not found");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            return Result.Fail(string.Join(", ", result.Errors.Select(e => e.Description)));

        // RefreshSignInAsync re-issues the security stamp cookie so the session stays valid
        // after the password change without requiring the user to log in again.
        await _signInManager.RefreshSignInAsync(user);
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> UpdateUserAsync(string userId, UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Fail("User not found");

        user.Email    = request.Email;
        user.UserName = request.Email;
        user.IsActive = request.IsActive;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return Result.Fail(string.Join(", ", result.Errors.Select(e => e.Description)));

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Fail("User not found");

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return Result.Fail(string.Join(", ", result.Errors.Select(e => e.Description)));

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> AddRoleToUserAsync(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Fail("User not found");

        var result = await _userManager.AddToRoleAsync(user, role);
        if (!result.Succeeded)
            return Result.Fail(string.Join(", ", result.Errors.Select(e => e.Description)));

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> RemoveRoleFromUserAsync(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Fail("User not found");

        var result = await _userManager.RemoveFromRoleAsync(user, role);
        if (!result.Succeeded)
            return Result.Fail(string.Join(", ", result.Errors.Select(e => e.Description)));

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<List<UserResponse>> GetAllUsersAsync() =>
        await _userRepository.GetAllUsersAsync();

    /// <inheritdoc />
    public async Task<UserResponse?> GetUserByIdAsync(string userId) =>
        await _userRepository.GetUserByIdAsync(userId);

    /// <inheritdoc />
    public async Task<Result<ClaimsPrincipal>> CreatePrincipalForAuthCodeAsync(
        ClaimsPrincipal cookiePrincipal, IEnumerable<string> scopes)
    {
        // cookiePrincipal comes from the Identity cookie session validated in /connect/authorize.
        var user = await _userManager.GetUserAsync(cookiePrincipal);
        if (user == null)
            return Result<ClaimsPrincipal>.Fail("User not found");

        if (!user.IsActive)
            return Result<ClaimsPrincipal>.Fail("Account is disabled");

        if (!await _signInManager.CanSignInAsync(user))
            return Result<ClaimsPrincipal>.Fail("User cannot sign in");

        var principal = await BuildPrincipalAsync(user, scopes);
        return Result<ClaimsPrincipal>.Ok(principal);
    }

    /// <inheritdoc />
    public async Task<Result<ClaimsPrincipal>> CreatePrincipalForRefreshTokenGrantAsync(
        ClaimsPrincipal currentPrincipal, IEnumerable<string> scopes)
    {
        // Re-fetch the user to pick up any account changes (e.g. IsActive toggled since the
        // original login). This prevents disabled accounts from silently renewing tokens.
        var user = await _userManager.GetUserAsync(currentPrincipal);
        if (user == null)
            return Result<ClaimsPrincipal>.Fail("User no longer exists");

        if (!user.IsActive)
            return Result<ClaimsPrincipal>.Fail("Account is disabled");

        if (!await _signInManager.CanSignInAsync(user))
            return Result<ClaimsPrincipal>.Fail("User cannot sign in");

        var principal = await BuildPrincipalAsync(user, scopes);
        return Result<ClaimsPrincipal>.Ok(principal);
    }

    /// <inheritdoc />
    public async Task LogoutAsync() => await _signInManager.SignOutAsync();

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Shared principal builder used by all grant types so claim structure is consistent
    /// regardless of how the user authenticated.
    /// <para>
    /// Claim destinations control which token a claim appears in:
    /// <list type="bullet">
    ///   <item><c>sub</c> — always in both access token and ID token.</item>
    ///   <item><c>name</c> — access token always; ID token only when <c>profile</c> scope is present.</item>
    ///   <item><c>email</c> — access token always; ID token only when <c>email</c> scope is present.</item>
    ///   <item><c>role</c> — access token always; ID token only when <c>roles</c> scope is present.</item>
    /// </list>
    /// </para>
    /// </summary>
    private async Task<ClaimsPrincipal> BuildPrincipalAsync(ApplicationUser user, IEnumerable<string> scopes)
    {
        var principal = await _signInManager.CreateUserPrincipalAsync(user);

        principal.SetClaim(OpenIddictConstants.Claims.Subject, await _userManager.GetUserIdAsync(user));
        principal.SetClaim(OpenIddictConstants.Claims.Email,   await _userManager.GetEmailAsync(user));
        principal.SetClaim(OpenIddictConstants.Claims.Name,    user.UserName);

        principal.SetScopes(scopes);

        foreach (var claim in principal.Claims)
        {
            var destinations = new List<string> { OpenIddictConstants.Destinations.AccessToken };

            if (claim.Type == OpenIddictConstants.Claims.Subject ||
               (claim.Type == OpenIddictConstants.Claims.Name  && principal.HasScope(OpenIddictConstants.Scopes.Profile)) ||
               (claim.Type == OpenIddictConstants.Claims.Email && principal.HasScope(OpenIddictConstants.Scopes.Email)) ||
               (claim.Type == OpenIddictConstants.Claims.Role  && principal.HasScope("roles")))
            {
                destinations.Add(OpenIddictConstants.Destinations.IdentityToken);
            }

            claim.SetDestinations(destinations);
        }

        return principal;
    }
}
