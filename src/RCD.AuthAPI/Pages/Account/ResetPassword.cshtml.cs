using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RCD.AuthAPI.Models.Requests;
using RCD.AuthAPI.Services;

namespace RCD.AuthAPI.Pages.Account;

public class ResetPasswordModel : PageModel
{
    private readonly IUserService _userService;

    public ResetPasswordModel(IUserService userService) => _userService = userService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(string? userId, string? token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            return RedirectToPage("/Account/Login");

        Input.UserId = userId;
        Input.Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await _userService.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = Input.Email,
            Token = Input.Token,
            NewPassword = Input.NewPassword,
            ConfirmPassword = Input.ConfirmPassword
        });

        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return Page();
        }

        Succeeded = true;
        return Page();
    }

    public class InputModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
