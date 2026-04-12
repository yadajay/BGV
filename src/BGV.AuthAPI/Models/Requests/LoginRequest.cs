namespace BGV.AuthAPI.Models.Requests;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    // public string? MfaCode { get; set; }
}