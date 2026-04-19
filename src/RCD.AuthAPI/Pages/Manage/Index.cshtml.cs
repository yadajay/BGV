using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RCD.AuthAPI.Services;
using RCD.Infrastructure.Db;

namespace RCD.AuthAPI.Pages.Manage;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IUserService _userService;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(IUserService userService, UserManager<ApplicationUser> userManager)
    {
        _userService = userService;
        _userManager = userManager;
    }

    public string Email { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = [];
    public bool IsActive { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null) return RedirectToPage("/Account/Login");

        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null) return RedirectToPage("/Account/Login");

        Email = user.Email;
        Roles = user.Roles;
        IsActive = user.IsActive;

        return Page();
    }
}
