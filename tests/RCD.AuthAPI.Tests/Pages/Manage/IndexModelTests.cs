using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Moq;
using RCD.AuthAPI.Models.Responses;
using RCD.AuthAPI.Pages.Manage;
using RCD.AuthAPI.Services;
using RCD.Infrastructure.Db;
using System.Security.Claims;

namespace RCD.AuthAPI.Tests.Pages.Manage;

public class IndexModelTests
{
    private readonly Mock<IUserService>                   _svc         = new();
    private readonly Mock<UserManager<ApplicationUser>>   _userManager;
    private readonly IndexModel                           _model;

    public IndexModelTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);

        _model = new IndexModel(_svc.Object, _userManager.Object);
    }

    private void SetupPageContext(ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext();
        if (user != null) httpContext.User = user;

        _model.PageContext = PageTestHelper.Create(httpContext);
    }

    [Fact]
    public async Task OnGetAsync_NullUserId_RedirectsToLogin()
    {
        SetupPageContext();
        _userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns((string?)null);

        var result = await _model.OnGetAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/Login", redirect.PageName);
    }

    [Fact]
    public async Task OnGetAsync_UserNotFound_RedirectsToLogin()
    {
        SetupPageContext();
        _userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("uid");
        _svc.Setup(s => s.GetUserByIdAsync("uid")).ReturnsAsync((UserResponse?)null);

        var result = await _model.OnGetAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/Login", redirect.PageName);
    }

    [Fact]
    public async Task OnGetAsync_UserFound_PopulatesAndReturnsPage()
    {
        SetupPageContext();
        _userManager.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns("uid");
        _svc.Setup(s => s.GetUserByIdAsync("uid")).ReturnsAsync(new UserResponse
        {
            Id       = "uid",
            Email    = "u@x.com",
            Roles    = ["Admin"],
            IsActive = true
        });

        var result = await _model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("u@x.com", _model.Email);
        Assert.Contains("Admin", _model.Roles);
        Assert.True(_model.IsActive);
        Assert.Null(_model.StatusMessage);
    }
}
