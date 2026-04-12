namespace BGV.AuthAPI.Models.Requests;

public class VerifyEmailRequest
{
    public string Token { get; set; } = string.Empty;
}