namespace BGV.AuthAPI.Models.Responses;

public class EnableMfaResponse
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
}