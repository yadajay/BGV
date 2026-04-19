using RCD.AuthAPI.Models.Responses;

namespace RCD.AuthAPI.Repositories;

public interface IUserRepository
{
    Task<List<UserResponse>> GetAllUsersAsync();
    Task<UserResponse?> GetUserByIdAsync(string userId);
}