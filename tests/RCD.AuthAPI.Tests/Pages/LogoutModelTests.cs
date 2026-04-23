using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Moq;
using RCD.AuthAPI.Pages.Account;
using RCD.AuthAPI.Services;

namespace RCD.AuthAPI.Tests.Pages;

public class LogoutModelTests
{
    private readonly Mock<IUserService> _svc   = new();
    private readonly LogoutModel        _model;

    public LogoutModelTests()
    {
        _model = new LogoutModel(_svc.Object);

        _model.PageContext = PageTestHelper.Create();
    }

    [Fact]
    public void OnGet_ReturnsPage()
    {
        var result = _model.OnGet();
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_CallsLogoutAndRedirectsToLogin()
    {
        _svc.Setup(s => s.LogoutAsync()).Returns(Task.CompletedTask);

        var result = await _model.OnPostAsync();

        _svc.Verify(s => s.LogoutAsync(), Times.Once);
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/Login", redirect.PageName);
    }
}
