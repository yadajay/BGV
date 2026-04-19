namespace RCD.AuthAPI.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string email, string userId, string token);
    Task SendEmailVerificationAsync(string email, string userId, string token);
}
