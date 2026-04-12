using BGV.AuthAPI.Models.Requests;
using BGV.AuthAPI.Models.Responses;
using BGV.AuthAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BGV.AuthAPI.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public class UserManagementController : ControllerBase
{
    private readonly IUserService _userService;

    public UserManagementController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var result = await _userService.UpdateUserAsync(id, request);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "User updated successfully" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var result = await _userService.DeleteUserAsync(id);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "User deleted successfully" });
    }

    [HttpPost("{id}/roles")]
    public async Task<IActionResult> AddRole(string id, [FromBody] AddRoleRequest request)
    {
        var result = await _userService.AddRoleToUserAsync(id, request.Role);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Role added successfully" });
    }

    [HttpDelete("{id}/roles/{role}")]
    public async Task<IActionResult> RemoveRole(string id, string role)
    {
        var result = await _userService.RemoveRoleFromUserAsync(id, role);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        return Ok(new { message = "Role removed successfully" });
    }
}