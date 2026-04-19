using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RCD.AuthAPI.Services;

namespace RCD.AuthAPI.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly IUserService _userService;

    public ForgotPasswordModel(IUserService userService) => _userService = userService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool EmailSent { get; set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        await _userService.ForgotPasswordAsync(Input.Email);

        // Always show the success message — never reveal whether the email exists.
        EmailSent = true;
        return Page();
    }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
