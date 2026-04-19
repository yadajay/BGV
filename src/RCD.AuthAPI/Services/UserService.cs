using RCD.AuthAPI.Models.Requests;
using RCD.AuthAPI.Models.Responses;
using RCD.AuthAPI.Repositories;
using RCD.Core.Models;
using RCD.Infrastructure.Db;
using OpenIddict.Abstractions;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace RCD.AuthAPI.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IUserRepository _userRepository;

    public UserService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IUserRepository userRepository)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userRepository = userRepository;
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

        return Result<string>.Ok(user.Id);
    }

    /// <inheritdoc />
    public async Task<Result> ForgotPasswordAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            return Result.Ok(); // Don't reveal if email exists

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        // TODO: Send email with token

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmPassword)
            return Result.Fail("Passwords do not match");

        var user = await _userManager.FindByEmailAsync(request.Token); // Assuming token contains email, but actually need to verify token
        if (user == null)
            return Result.Fail("Invalid token");

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return Result.Fail(string.Join(", ", result.Errors.Select(e => e.Description)));

        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> VerifyEmailAsync(string token)
    {
        // TODO: Implement email verification
        return Result.Ok();
    }

    /// <inheritdoc />
    public async Task<Result> UpdateUserAsync(string userId, UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Result.Fail("User not found");

        user.Email = request.Email;
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
    public async Task<List<UserResponse>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllUsersAsync();
    }

    /// <inheritdoc />
    public async Task<UserResponse?> GetUserByIdAsync(string userId)
    {
        return await _userRepository.GetUserByIdAsync(userId);
    }

    /// <inheritdoc />
    public async Task<Result<ClaimsPrincipal>> CreatePrincipalForPasswordGrantAsync(string username, string password, IEnumerable<string> scopes)
    {
        var user = await _userManager.FindByNameAsync(username);
        if (user == null) return Result<ClaimsPrincipal>.Fail("Invalid credentials");

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded) return Result<ClaimsPrincipal>.Fail("Invalid credentials");

        var principal = await _signInManager.CreateUserPrincipalAsync(user);

        // Explicitly set the subject claim to the user's unique identifier.
        principal.SetClaim(OpenIddictConstants.Claims.Subject, await _userManager.GetUserIdAsync(user));
        principal.SetClaim(OpenIddictConstants.Claims.Email, await _userManager.GetEmailAsync(user));
        principal.SetClaim(OpenIddictConstants.Claims.Name, user.UserName); // In the future, map this to a 'FullName' property

        principal.SetScopes(scopes);

        foreach (var claim in principal.Claims)
        {
            var destinations = new List<string> { OpenIddictConstants.Destinations.AccessToken };

            // Decide if the claim should also go to the Identity Token (ID Token)
            if (claim.Type == OpenIddictConstants.Claims.Subject ||
               (claim.Type == OpenIddictConstants.Claims.Name && principal.HasScope(OpenIddictConstants.Scopes.Profile)) ||
               (claim.Type == OpenIddictConstants.Claims.Email && principal.HasScope(OpenIddictConstants.Scopes.Email)) ||
               (claim.Type == OpenIddictConstants.Claims.Role && principal.HasScope("roles")))
            {
                destinations.Add(OpenIddictConstants.Destinations.IdentityToken);
            }

            claim.SetDestinations(destinations);
        }

        return Result<ClaimsPrincipal>.Ok(principal);
    }

    /// <inheritdoc />
    public async Task<Result<ClaimsPrincipal>> CreatePrincipalForRefreshTokenGrantAsync(ClaimsPrincipal currentPrincipal, IEnumerable<string> scopes)
    {
        var user = await _userManager.GetUserAsync(currentPrincipal);
        if (user == null) return Result<ClaimsPrincipal>.Fail("User no longer exists");

        if (!await _signInManager.CanSignInAsync(user)) return Result<ClaimsPrincipal>.Fail("User cannot sign in");

        var principal = await _signInManager.CreateUserPrincipalAsync(user);

        // Explicitly set the subject claim.
        principal.SetClaim(OpenIddictConstants.Claims.Subject, await _userManager.GetUserIdAsync(user));
        principal.SetClaim(OpenIddictConstants.Claims.Email, await _userManager.GetEmailAsync(user));
        principal.SetClaim(OpenIddictConstants.Claims.Name, user.UserName);

        principal.SetScopes(scopes);

        foreach (var claim in principal.Claims)
        {
            var destinations = new List<string> { OpenIddictConstants.Destinations.AccessToken };

            if (claim.Type == OpenIddictConstants.Claims.Subject ||
               (claim.Type == OpenIddictConstants.Claims.Name && principal.HasScope(OpenIddictConstants.Scopes.Profile)) ||
               (claim.Type == OpenIddictConstants.Claims.Email && principal.HasScope(OpenIddictConstants.Scopes.Email)) ||
               (claim.Type == OpenIddictConstants.Claims.Role && principal.HasScope("roles")))
            {
                destinations.Add(OpenIddictConstants.Destinations.IdentityToken);
            }

            claim.SetDestinations(destinations);
        }

        return Result<ClaimsPrincipal>.Ok(principal);
    }

    /// <inheritdoc />
    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }
}