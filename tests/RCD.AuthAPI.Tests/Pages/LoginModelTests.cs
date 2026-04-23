using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Moq;
using RCD.AuthAPI.Pages.Account;
using RCD.AuthAPI.Services;
using RCD.Core.Models;

namespace RCD.AuthAPI.Tests.Pages;

public class LoginModelTests
{
    private readonly Mock<IUserService> _svc     = new();
    private readonly Mock<IUrlHelper>   _url     = new();
    private readonly LoginModel         _model;

    public LoginModelTests()
    {
        _model = new LoginModel(_svc.Object);

        _model.PageContext = PageTestHelper.Create();
        _model.Url = _url.Object;
    }

    [Fact]
    public void OnGet_DoesNotThrow()
    {
        _model.OnGet(); // no assertion needed — just verifying no exception
        Assert.True(true);
    }

    [Fact]
    public async Task OnPostAsync_InvalidModelState_ReturnsPage()
    {
        _model.ModelState.AddModelError("Email", "Required");
        var result = await _model.OnPostAsync();
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_LoginFails_SetsErrorAndReturnsPage()
    {
        _model.Input = new LoginModel.InputModel { Email = "a@b.com", Password = "bad" };
        _svc.Setup(s => s.LoginAsync("a@b.com", "bad", false))
            .ReturnsAsync(Result.Fail("Invalid email or password"));

        var result = await _model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Invalid email or password", _model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_LoginSucceeds_WithLocalReturnUrl_RedirectsToReturnUrl()
    {
        _model.Input     = new LoginModel.InputModel { Email = "a@b.com", Password = "pwd" };
        _model.ReturnUrl = "/connect/authorize?client_id=x";
        _svc.Setup(s => s.LoginAsync("a@b.com", "pwd", false)).ReturnsAsync(Result.Ok());
        _url.Setup(u => u.IsLocalUrl("/connect/authorize?client_id=x")).Returns(true);

        var result = await _model.OnPostAsync();

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/connect/authorize?client_id=x", redirect.Url);
    }

    [Fact]
    public async Task OnPostAsync_LoginSucceeds_NoReturnUrl_RedirectsToManage()
    {
        _model.Input     = new LoginModel.InputModel { Email = "a@b.com", Password = "pwd" };
        _model.ReturnUrl = null;
        _svc.Setup(s => s.LoginAsync("a@b.com", "pwd", false)).ReturnsAsync(Result.Ok());

        var result = await _model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Manage/Index", redirect.PageName);
    }

    [Fact]
    public async Task OnPostAsync_LoginSucceeds_NonLocalReturnUrl_RedirectsToManage()
    {
        _model.Input     = new LoginModel.InputModel { Email = "a@b.com", Password = "pwd" };
        _model.ReturnUrl = "https://evil.com";
        _svc.Setup(s => s.LoginAsync("a@b.com", "pwd", false)).ReturnsAsync(Result.Ok());
        _url.Setup(u => u.IsLocalUrl("https://evil.com")).Returns(false);

        var result = await _model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
    }
}
