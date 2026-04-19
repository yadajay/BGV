namespace RCD.AuthAPI.Models.Requests;

public class VerifyMfaRequest
{
    public string Code { get; set; } = string.Empty;
}