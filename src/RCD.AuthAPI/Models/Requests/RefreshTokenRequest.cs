namespace RCD.AuthAPI.Models.Requests;

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}