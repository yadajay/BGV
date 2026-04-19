using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RCD.AuthAPI.Services;

namespace RCD.AuthAPI.Pages.Account;

public class LoginModel : PageModel
{
    private readonly IUserService _userService;

    public LoginModel(IUserService userService) => _userService = userService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await _userService.LoginAsync(Input.Email, Input.Password, Input.RememberMe);

        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return Page();
        }

        // LocalRedirect guards against open redirect attacks.
        // ReturnUrl is typically /connect/authorize?... which continues the OIDC flow.
        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            return LocalRedirect(ReturnUrl);

        return RedirectToPage("/Manage/Index");
    }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
