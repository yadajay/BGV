using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RCD.AuthAPI.Services;

namespace RCD.AuthAPI.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly IUserService _userService;

    public LogoutModel(IUserService userService) => _userService = userService;

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        await _userService.LogoutAsync();
        return RedirectToPage("/Account/Login");
    }
}
