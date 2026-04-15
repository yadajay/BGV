using BGV.AuthAPI.Models.Requests;
using BGV.AuthAPI.Models.Responses;
using BGV.Core.Models;
using System.Security.Claims;

namespace BGV.AuthAPI.Services;

public interface IUserService
{
    /// <summary>
    /// Registers a new user in the system with the provided credentials.
    /// </summary>
    /// <param name="request">The registration details.</param>
    /// <returns>A result containing the new User ID if successful.</returns>
    Task<Result<string>> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Initiates the forgotten password flow by generating a reset token.
    /// </summary>
    /// <param name="email">The email of the user.</param>
    Task<Result> ForgotPasswordAsync(string email);

    /// <summary>
    /// Resets a user's password using a valid reset token.
    /// </summary>
    /// <param name="request">The reset details including the token and new password.</param>
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request);

    /// <summary>
    /// Verifies a user's email address using a verification token.
    /// </summary>
    /// <param name="token">The verification token.</param>
    Task<Result> VerifyEmailAsync(string token);

    /// <summary>
    /// Updates the profile information for an existing user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="request">The updated user details.</param>
    Task<Result> UpdateUserAsync(string userId, UpdateUserRequest request);

    /// <summary>
    /// Permanently deletes a user from the system.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    Task<Result> DeleteUserAsync(string userId);

    /// <summary>
    /// Assigns a specific identity role to a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="role">The name of the role to add.</param>
    Task<Result> AddRoleToUserAsync(string userId, string role);

    /// <summary>
    /// Removes a specific identity role from a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="role">The name of the role to remove.</param>
    Task<Result> RemoveRoleFromUserAsync(string userId, string role);

    /// <summary>
    /// Retrieves a list of all registered users.
    /// </summary>
    Task<List<UserResponse>> GetAllUsersAsync();

    /// <summary>
    /// Retrieves the details of a specific user by their ID.
    /// </summary>
    Task<UserResponse?> GetUserByIdAsync(string userId);

    /// <summary>
    /// Validates credentials and creates a ClaimsPrincipal for the OIDC Password Grant flow.
    /// </summary>
    Task<Result<ClaimsPrincipal>> CreatePrincipalForPasswordGrantAsync(string username, string password, IEnumerable<string> scopes);

    /// <summary>
    /// Revalidates the user and creates a new ClaimsPrincipal for the OIDC Refresh Token Grant flow.
    /// </summary>
    Task<Result<ClaimsPrincipal>> CreatePrincipalForRefreshTokenGrantAsync(ClaimsPrincipal currentPrincipal, IEnumerable<string> scopes);

    /// <summary>
    /// Signs the user out of the current session.
    /// </summary>
    Task LogoutAsync();
}