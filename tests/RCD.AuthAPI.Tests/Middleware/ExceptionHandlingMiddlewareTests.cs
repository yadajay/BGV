using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using RCD.AuthAPI.Middleware;
using System.Text.Json;

namespace RCD.AuthAPI.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _logger = new();

    [Fact]
    public async Task InvokeAsync_NoException_CallsNext()
    {
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new ExceptionHandlingMiddleware(next, _logger.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ExceptionThrown_Returns500WithProblemDetails()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("boom");
        var middleware = new ExceptionHandlingMiddleware(next, _logger.Object);

        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(body).ReadToEndAsync();
        var doc  = JsonDocument.Parse(json);
        Assert.Equal("boom", doc.RootElement.GetProperty("detail").GetString());
        Assert.Equal(500,    doc.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_ExceptionThrown_LogsError()
    {
        RequestDelegate next = _ => throw new Exception("err");
        var middleware = new ExceptionHandlingMiddleware(next, _logger.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
