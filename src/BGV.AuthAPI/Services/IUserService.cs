using BGV.AuthAPI.Models.Requests;
using BGV.AuthAPI.Models.Responses;
using BGV.Core.Models;
using System.Security.Claims;

namespace BGV.AuthAPI.Services;

public interface IUserService
{
    Task<Result<string>> RegisterAsync(RegisterRequest request);
    Task<Result<string>> LoginAsync(LoginRequest request);
    Task<Result> ForgotPasswordAsync(string email);
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request);
    Task<Result> VerifyEmailAsync(string token);
    Task<Result> UpdateUserAsync(string userId, UpdateUserRequest request);
    Task<Result> DeleteUserAsync(string userId);
    Task<Result> AddRoleToUserAsync(string userId, string role);
    Task<Result> RemoveRoleFromUserAsync(string userId, string role);
    Task<List<UserResponse>> GetAllUsersAsync();
    Task<UserResponse?> GetUserByIdAsync(string userId);
    Task<Result<ClaimsPrincipal>> CreatePrincipalForPasswordGrantAsync(string username, string password, IEnumerable<string> scopes);
    Task<Result<ClaimsPrincipal>> CreatePrincipalForRefreshTokenGrantAsync(ClaimsPrincipal currentPrincipal, IEnumerable<string> scopes);
    Task LogoutAsync();
}