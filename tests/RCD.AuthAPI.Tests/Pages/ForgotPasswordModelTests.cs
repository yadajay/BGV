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

public class ForgotPasswordModelTests
{
    private readonly Mock<IUserService>  _svc   = new();
    private readonly ForgotPasswordModel _model;

    public ForgotPasswordModelTests()
    {
        _model = new ForgotPasswordModel(_svc.Object);

        _model.PageContext = PageTestHelper.Create();
    }

    [Fact]
    public void OnGet_ReturnsPage()
    {
        var result = _model.OnGet();
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_InvalidModelState_ReturnsPage()
    {
        _model.ModelState.AddModelError("Email", "Required");
        var result = await _model.OnPostAsync();
        Assert.IsType<PageResult>(result);
    }

    [Fact]
    public async Task OnPostAsync_ValidEmail_SetsEmailSentAndReturnsPage()
    {
        _model.Input = new ForgotPasswordModel.InputModel { Email = "u@x.com" };
        _svc.Setup(s => s.ForgotPasswordAsync("u@x.com")).ReturnsAsync(Result.Ok());

        var result = await _model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(_model.EmailSent);
    }
}
