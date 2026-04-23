using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Moq;
using RCD.AuthAPI.Pages.Account;
using RCD.AuthAPI.Services;
using RCD.Core.Models;

namespace RCD.AuthAPI.Tests.Pages;

public class ResetPasswordModelTests
{
    private readonly Mock<IUserService>  _svc   = new();
    private readonly ResetPasswordModel  _model;

    public ResetPasswordModelTests()
    {
        _model = new ResetPasswordModel(_svc.Object);

        _model.PageContext = PageTestHelper.Create();
    }

    [Fact]
    public void OnGet_NullUserId_RedirectsToLogin()
    {
        var result = _model.OnGet(null, "token");
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/Login", redirect.PageName);
    }

    [Fact]
    public void OnGet_NullToken_RedirectsToLogin()
    {
        var result = _model.OnGet("uid", null);
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/Login", redirect.PageName);
    }

    [Fact]
    public void OnGet_BothPresent_SetsInputAndReturnsPage()
    {
        var result = _model.OnGet("uid1", "tok1");
        Assert.IsType<PageResult>(result);
        Assert.Equal("uid1", _model.Input.UserId);
        Assert.Equal("tok1", _model.Input.Token);
    }

    [Fact]
    public async Task OnPostAsync_InvalidModelState_ReturnsPage()
    {
        _model.ModelState.AddModelError("Email", "Required");
        var result = await _model.OnPostAsync();
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_ServiceFails_SetsErrorAndReturnsPage()
    {
        _model.Input = new ResetPasswordModel.InputModel
        {
            Email = "u@x.com", Token = "tok",
            NewPassword = "P@ss1", ConfirmPassword = "P@ss1"
        };
        _svc.Setup(s => s.ResetPasswordAsync(It.IsAny<RCD.AuthAPI.Models.Requests.ResetPasswordRequest>()))
            .ReturnsAsync(Result.Fail("Invalid token"));

        var result = await _model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Invalid token", _model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_Success_SetsSucceededAndReturnsPage()
    {
        _model.Input = new ResetPasswordModel.InputModel
        {
            Email = "u@x.com", Token = "tok",
            NewPassword = "P@ss1", ConfirmPassword = "P@ss1"
        };
        _svc.Setup(s => s.ResetPasswordAsync(It.IsAny<RCD.AuthAPI.Models.Requests.ResetPasswordRequest>()))
            .ReturnsAsync(Result.Ok());

        var result = await _model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(_model.Succeeded);
    }
}
