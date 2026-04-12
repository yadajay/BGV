using BGV.AuthAPI.Models.Responses;
using BGV.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BGV.AuthAPI.Repositories;

public class UserRepository : IUserRepository
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRepository(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<List<UserResponse>> GetAllUsersAsync()
    {
        var users = await _userManager.Users.ToListAsync();
        var result = new List<UserResponse>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserResponse
            {
                Id = user.Id,
                Email = user.Email!,
                IsActive = user.IsActive,
                Roles = roles.ToList()
            });
        }

        return result;
    }

    public async Task<UserResponse?> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return null;

        var roles = await _userManager.GetRolesAsync(user);
        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email!,
            IsActive = user.IsActive,
            Roles = roles.ToList()
        };
    }
}