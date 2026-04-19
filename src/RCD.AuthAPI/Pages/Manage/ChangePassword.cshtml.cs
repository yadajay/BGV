using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RCD.AuthAPI.Services;
using RCD.Infrastructure.Db;

namespace RCD.AuthAPI.Pages.Manage;

[Authorize]
public class ChangePasswordModel : PageModel
{
    private readonly IUserService _userService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChangePasswordModel(IUserService userService, UserManager<ApplicationUser> userManager)
    {
        _userService = userService;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var userId = _userManager.GetUserId(User);
        if (userId == null) return RedirectToPage("/Account/Login");

        var result = await _userService.ChangePasswordAsync(userId, Input.CurrentPassword, Input.NewPassword);

        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return Page();
        }

        TempData["StatusMessage"] = "Password changed successfully.";
        return RedirectToPage("/Manage/Index");
    }

    public class InputModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
