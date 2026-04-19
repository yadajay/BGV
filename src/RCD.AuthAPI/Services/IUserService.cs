using RCD.AuthAPI.Models.Requests;
using RCD.AuthAPI.Models.Responses;
using RCD.Core.Models;
using RCD.Infrastructure.Db;
using System.Security.Claims;

namespace RCD.AuthAPI.Services;

public interface IUserService
{
    /// <summary>
    /// Creates a new user account. Sends an email verification link via <see cref="IEmailService"/>.
    /// Returns the new user's ID on success.
    /// </summary>
    Task<Result<string>> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Validates credentials and signs the user in with an Identity cookie (the auth server's own
    /// session). Called by the Login Razor Page before redirecting back to <c>/connect/authorize</c>.
    /// </summary>
    Task<Result> LoginAsync(string email, string password, bool rememberMe);

    /// <summary>
    /// Generates a password-reset token and dispatches it via <see cref="IEmailService"/>.
    /// Always returns success to avoid revealing whether the email address is registered.
    /// </summary>
    Task<Result> ForgotPasswordAsync(string email);

    /// <summary>
    /// Resets the user's password using the token from a password-reset email link.
    /// <see cref="ResetPasswordRequest.Email"/> identifies the account;
    /// <see cref="ResetPasswordRequest.Token"/> is the base64url-encoded reset token.
    /// </summary>
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>
    /// Confirms the user's email address using the token from an email-verification link.
    /// Both <paramref name="userId"/> and <paramref name="token"/> are embedded in the link
    /// sent by <see cref="IEmailService.SendEmailVerificationAsync"/>.
    /// </summary>
    Task<Result> VerifyEmailAsync(string userId, string token);

    /// <summary>
    /// Changes the signed-in user's password. Refreshes the Identity cookie after a successful
    /// change so the user stays logged in.
    /// </summary>
    Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword);

    /// <summary>
    /// Updates the user's email address, username, and active status.
    /// Called from the admin user-management API.
    /// </summary>
    Task<Result> UpdateUserAsync(string userId, UpdateUserRequest request);

    /// <summary>
    /// Permanently deletes the user account. This action is irreversible.
    /// </summary>
    Task<Result> DeleteUserAsync(string userId);

    /// <summary>
    /// Assigns an Identity role to the user. The role must already exist in the role store.
    /// </summary>
    Task<Result> AddRoleToUserAsync(string userId, string role);

    /// <summary>
    /// Removes an Identity role from the user.
    /// </summary>
    Task<Result> RemoveRoleFromUserAsync(string userId, string role);

    /// <summary>
    /// Returns all registered users with their roles. Backed by <see cref="IUserRepository"/>
    /// to keep query logic out of the service layer.
    /// </summary>
    Task<List<UserResponse>> GetAllUsersAsync();

    /// <summary>
    /// Returns a single user by ID, or <c>null</c> if not found.
    /// </summary>
    Task<UserResponse?> GetUserByIdAsync(string userId);

    /// <summary>
    /// Builds an OpenIddict <see cref="ClaimsPrincipal"/> for the Authorization Code flow.
    /// <paramref name="cookiePrincipal"/> is the Identity cookie principal from the user's
    /// existing auth server session — it identifies which user approved the authorization request.
    /// Scopes are taken from the original <c>/connect/authorize</c> request.
    /// </summary>
    Task<Result<ClaimsPrincipal>> CreatePrincipalForAuthCodeAsync(
        ClaimsPrincipal cookiePrincipal, IEnumerable<string> scopes);

    /// <summary>
    /// Revalidates the user and issues a refreshed <see cref="ClaimsPrincipal"/> for the
    /// Refresh Token grant. Re-checks IsActive and CanSignIn so a disabled account cannot
    /// silently obtain new tokens via an old refresh token.
    /// </summary>
    Task<Result<ClaimsPrincipal>> CreatePrincipalForRefreshTokenGrantAsync(
        ClaimsPrincipal currentPrincipal, IEnumerable<string> scopes);

    /// <summary>
    /// Signs the user out of the Identity cookie session on this auth server.
    /// For full OIDC logout (clearing the OpenIddict token + redirecting the client),
    /// use <c>GET /connect/logout</c> instead.
    /// </summary>
    Task LogoutAsync();
}
