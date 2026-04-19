namespace RCD.AuthAPI.Services;

// Stub implementation — replace with a real provider (SendGrid, SMTP, etc.) when ready.
// All links are logged so you can test flows without a mail server during development.
public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly string _baseUrl;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _baseUrl = configuration["AppBaseUrl"] ?? "https://localhost:7001";
    }

    public Task SendPasswordResetEmailAsync(string email, string userId, string token)
    {
        var link = $"{_baseUrl}/Account/ResetPassword?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";
        _logger.LogInformation("Password reset link for {Email}: {Link}", email, link);
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(string email, string userId, string token)
    {
        var link = $"{_baseUrl}/Account/VerifyEmail?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(token)}";
        _logger.LogInformation("Email verification link for {Email}: {Link}", email, link);
        return Task.CompletedTask;
    }
}
