using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using RCD.AuthAPI.Repositories;
using RCD.AuthAPI.Tests.Helpers;
using RCD.Infrastructure.Db;

namespace RCD.AuthAPI.Tests.Repositories;

public class UserRepositoryTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly UserRepository _repo;

    public UserRepositoryTests()
    {
        var store          = new Mock<IUserStore<ApplicationUser>>();
        var contextAccessor = new Mock<IHttpContextAccessor>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
        _repo = new UserRepository(_userManager.Object);
    }

    [Fact]
    public async Task GetAllUsersAsync_ReturnsAllUsersWithRoles()
    {
        var u1 = new ApplicationUser { Id = "1", Email = "a@b.com", IsActive = true };
        var u2 = new ApplicationUser { Id = "2", Email = "c@d.com", IsActive = false };
        var users = new TestAsyncEnumerable<ApplicationUser>(new[] { u1, u2 });

        _userManager.Setup(m => m.Users).Returns(users);
        _userManager.Setup(m => m.GetRolesAsync(u1)).ReturnsAsync(new List<string> { "Admin" });
        _userManager.Setup(m => m.GetRolesAsync(u2)).ReturnsAsync(new List<string>());

        var result = await _repo.GetAllUsersAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("1", result[0].Id);
        Assert.Contains("Admin", result[0].Roles);
        Assert.Equal("2", result[1].Id);
        Assert.Empty(result[1].Roles);
    }

    [Fact]
    public async Task GetAllUsersAsync_EmptyStore_ReturnsEmptyList()
    {
        var users = new TestAsyncEnumerable<ApplicationUser>(Array.Empty<ApplicationUser>());
        _userManager.Setup(m => m.Users).Returns(users);

        var result = await _repo.GetAllUsersAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUserByIdAsync_UserNotFound_ReturnsNull()
    {
        _userManager.Setup(m => m.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);

        var result = await _repo.GetUserByIdAsync("missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserByIdAsync_UserFound_ReturnsResponse()
    {
        var user = new ApplicationUser { Id = "uid1", Email = "a@b.com", IsActive = true };
        _userManager.Setup(m => m.FindByIdAsync("uid1")).ReturnsAsync(user);
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Vendor" });

        var result = await _repo.GetUserByIdAsync("uid1");

        Assert.NotNull(result);
        Assert.Equal("uid1", result.Id);
        Assert.Equal("a@b.com", result.Email);
        Assert.True(result.IsActive);
        Assert.Contains("Vendor", result.Roles);
    }
}
