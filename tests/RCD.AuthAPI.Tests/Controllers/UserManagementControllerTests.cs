using Microsoft.AspNetCore.Mvc;
using Moq;
using RCD.AuthAPI.Controllers;
using RCD.AuthAPI.Models.Requests;
using RCD.AuthAPI.Models.Responses;
using RCD.AuthAPI.Services;
using RCD.Core.Models;

namespace RCD.AuthAPI.Tests.Controllers;

public class UserManagementControllerTests
{
    private readonly Mock<IUserService>       _svc = new();
    private readonly UserManagementController _ctrl;

    public UserManagementControllerTests()
    {
        _ctrl = new UserManagementController(_svc.Object);
    }

    // ── GetUsers ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsers_ReturnsOkWithList()
    {
        var list = new List<UserResponse> { new() { Id = "1" } };
        _svc.Setup(s => s.GetAllUsersAsync()).ReturnsAsync(list);

        var result = await _ctrl.GetUsers();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(list, ok.Value);
    }

    // ── GetUser ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUser_NotFound_ReturnsNotFound()
    {
        _svc.Setup(s => s.GetUserByIdAsync("uid")).ReturnsAsync((UserResponse?)null);

        var result = await _ctrl.GetUser("uid");
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetUser_Found_ReturnsOk()
    {
        var user = new UserResponse { Id = "uid" };
        _svc.Setup(s => s.GetUserByIdAsync("uid")).ReturnsAsync(user);

        var result = await _ctrl.GetUser("uid");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(user, ok.Value);
    }

    // ── UpdateUser ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUser_ServiceFails_ReturnsBadRequest()
    {
        _svc.Setup(s => s.UpdateUserAsync("uid", It.IsAny<UpdateUserRequest>()))
            .ReturnsAsync(Result.Fail("conflict"));

        var result = await _ctrl.UpdateUser("uid", new UpdateUserRequest());
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(bad.Value);
    }

    [Fact]
    public async Task UpdateUser_Success_ReturnsOk()
    {
        _svc.Setup(s => s.UpdateUserAsync("uid", It.IsAny<UpdateUserRequest>()))
            .ReturnsAsync(Result.Ok());

        var result = await _ctrl.UpdateUser("uid", new UpdateUserRequest());
        Assert.IsType<OkObjectResult>(result);
    }

    // ── DeleteUser ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_ServiceFails_ReturnsBadRequest()
    {
        _svc.Setup(s => s.DeleteUserAsync("uid")).ReturnsAsync(Result.Fail("err"));

        var result = await _ctrl.DeleteUser("uid");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteUser_Success_ReturnsOk()
    {
        _svc.Setup(s => s.DeleteUserAsync("uid")).ReturnsAsync(Result.Ok());

        var result = await _ctrl.DeleteUser("uid");
        Assert.IsType<OkObjectResult>(result);
    }

    // ── AddRole ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddRole_ServiceFails_ReturnsBadRequest()
    {
        _svc.Setup(s => s.AddRoleToUserAsync("uid", "Admin")).ReturnsAsync(Result.Fail("err"));

        var result = await _ctrl.AddRole("uid", new AddRoleRequest { Role = "Admin" });
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddRole_Success_ReturnsOk()
    {
        _svc.Setup(s => s.AddRoleToUserAsync("uid", "Admin")).ReturnsAsync(Result.Ok());

        var result = await _ctrl.AddRole("uid", new AddRoleRequest { Role = "Admin" });
        Assert.IsType<OkObjectResult>(result);
    }

    // ── RemoveRole ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveRole_ServiceFails_ReturnsBadRequest()
    {
        _svc.Setup(s => s.RemoveRoleFromUserAsync("uid", "Admin")).ReturnsAsync(Result.Fail("err"));

        var result = await _ctrl.RemoveRole("uid", "Admin");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RemoveRole_Success_ReturnsOk()
    {
        _svc.Setup(s => s.RemoveRoleFromUserAsync("uid", "Admin")).ReturnsAsync(Result.Ok());

        var result = await _ctrl.RemoveRole("uid", "Admin");
        Assert.IsType<OkObjectResult>(result);
    }
}
