using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Moq;
using RCD.AuthAPI.Pages.Manage;
using RCD.AuthAPI.Services;
using RCD.Core.Models;
using RCD.Infrastructure.Db;
using System.Security.Claims;

namespace RCD.AuthAPI.Tests.Pages.Manage;

public class ChangePasswordModelTests
{
    private readonly Mock<IUserService>                  _svc        = new();
    private readonly Mock<UserManager<ApplicationUser>>  _userManager;
    private readonly ChangePasswordModel                 _model;

    public ChangePasswordModelTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);

        _model = new ChangePasswordModel(_svc.Object, _userManager.Object);
    }

    private void SetupPageContext()
    {
        var httpContext = new DefaultHttpContext();
        var tempData    = new Mock<ITempDataDictionary>();
        _model.TempData = tempData.Object;

        _model.PageContext = PageTestHelper.Create(httpContext);
    }

    [Fact]
    public void OnGet_ReturnsPage()
    {
        SetupPageContext();
        var result = _model.OnGet();
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_InvalidModelState_ReturnsPage()
    {
        SetupPageContext();
        _model.ModelState.AddModelError("CurrentPassword", "Required");

        var result = await _model.OnPostAsync();
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_NullUserId_RedirectsToLogin()
    {
        SetupPageContext();
        _model.Input = new ChangePasswordModel.InputModel
        {
            CurrentPassword = "old", NewPassword = "new", ConfirmPassword = "new"
        };
        _userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns((string?)null);

        var result = await _model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/Login", redirect.PageName);
    }

    [Fact]
    public async Task OnPostAsync_ServiceFails_SetsErrorAndReturnsPage()
    {
        SetupPageContext();
        _model.Input = new ChangePasswordModel.InputModel
        {
            CurrentPassword = "old", NewPassword = "new", ConfirmPassword = "new"
        };
        _userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("uid");
        _svc.Setup(s => s.ChangePasswordAsync("uid", "old", "new"))
            .ReturnsAsync(Result.Fail("Wrong password"));

        var result = await _model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("Wrong password", _model.ErrorMessage);
    }

    [Fact]
    public async Task OnPostAsync_Success_RedirectsToManage()
    {
        SetupPageContext();
        _model.Input = new ChangePasswordModel.InputModel
        {
            CurrentPassword = "old", NewPassword = "new", ConfirmPassword = "new"
        };
        _userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("uid");
        _svc.Setup(s => s.ChangePasswordAsync("uid", "old", "new"))
            .ReturnsAsync(Result.Ok());

        var result = await _model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Manage/Index", redirect.PageName);
    }
}
