using BGV.AuthAPI.Models.Responses;

namespace BGV.AuthAPI.Repositories;

public interface IUserRepository
{
    Task<List<UserResponse>> GetAllUsersAsync();
    Task<UserResponse?> GetUserByIdAsync(string userId);
}