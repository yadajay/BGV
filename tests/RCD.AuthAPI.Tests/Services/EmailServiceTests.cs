using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using RCD.AuthAPI.Services;

namespace RCD.AuthAPI.Tests.Services;

public class EmailServiceTests
{
    private readonly Mock<ILogger<EmailService>> _logger = new();

    private EmailService Build(string? baseUrl)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(baseUrl != null
                ? new Dictionary<string, string?> { ["AppBaseUrl"] = baseUrl }
                : new Dictionary<string, string?>())
            .Build();
        return new EmailService(_logger.Object, config);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_LogsLink()
    {
        var svc = Build("https://example.com");
        await svc.SendPasswordResetEmailAsync("u@test.com", "uid1", "tok1");
        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("uid1")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_LogsLink()
    {
        var svc = Build("https://example.com");
        await svc.SendEmailVerificationAsync("u@test.com", "uid2", "tok2");
        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("uid2")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Constructor_UsesDefaultBaseUrl_WhenConfigMissing()
    {
        var svc = Build(null);
        // Should not throw even without AppBaseUrl configured
        await svc.SendPasswordResetEmailAsync("u@test.com", "uid3", "tok3");
        _logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("localhost")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
