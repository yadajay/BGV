using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using OpenIddict.Abstractions;
using RCD.AuthAPI.Models.Requests;
using RCD.AuthAPI.Models.Responses;
using RCD.AuthAPI.Repositories;
using RCD.AuthAPI.Services;
using RCD.Infrastructure.Db;
using System.Security.Claims;

namespace RCD.AuthAPI.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>>   _userManager;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManager;
    private readonly Mock<IUserRepository>   _repo    = new();
    private readonly Mock<IEmailService>     _email   = new();
    private readonly UserService             _svc;

    public UserServiceTests()
    {
        var store          = new Mock<IUserStore<ApplicationUser>>();
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory  = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();

        _userManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);

        _signInManager = new Mock<SignInManager<ApplicationUser>>(
            _userManager.Object, contextAccessor.Object, claimsFactory.Object,
            null, null, null, null);

        _svc = new UserService(
            _userManager.Object, _signInManager.Object, _repo.Object, _email.Object);
    }

    // ── RegisterAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_PasswordMismatch_ReturnsFail()
    {
        var req = new RegisterRequest
        {
            Email = "a@b.com", Password = "P@ss1", ConfirmPassword = "Other"
        };
        var result = await _svc.RegisterAsync(req);
        Assert.False(result.Success);
        Assert.Contains("do not match", result.Error);
    }

    [Fact]
    public async Task Register_CreateFails_ReturnsFail()
    {
        var req = new RegisterRequest
        {
            Email = "a@b.com", Password = "P@ss1", ConfirmPassword = "P@ss1"
        };
        _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), req.Password))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Description = "Duplicate email" }));

        var result = await _svc.RegisterAsync(req);
        Assert.False(result.Success);
        Assert.Contains("Duplicate email", result.Error);
    }

    [Fact]
    public async Task Register_Success_ReturnsUserId_AndSendsEmail()
    {
        var req = new RegisterRequest
        {
            Email = "a@b.com", Password = "P@ss1", ConfirmPassword = "P@ss1"
        };
        var user = new ApplicationUser { Id = "uid1", Email = req.Email };

        _userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), req.Password))
            .Callback<ApplicationUser, string>((u, _) => u.Id = "uid1")
            .ReturnsAsync(IdentityResult.Success);

        _userManager.Setup(m => m.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("rawtoken");

        _email.Setup(e => e.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await _svc.RegisterAsync(req);
        Assert.True(result.Success);
        _email.Verify(e => e.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ── LoginAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByEmailAsync("x@y.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _svc.LoginAsync("x@y.com", "pwd", false);
        Assert.False(result.Success);
        Assert.Contains("Invalid", result.Error);
    }

    [Fact]
    public async Task Login_InactiveUser_ReturnsFail()
    {
        var user = new ApplicationUser { IsActive = false };
        _userManager.Setup(m => m.FindByEmailAsync("x@y.com")).ReturnsAsync(user);

        var result = await _svc.LoginAsync("x@y.com", "pwd", false);
        Assert.False(result.Success);
        Assert.Contains("disabled", result.Error);
    }

    [Fact]
    public async Task Login_LockedOut_ReturnsFail()
    {
        var user = new ApplicationUser { IsActive = true };
        _userManager.Setup(m => m.FindByEmailAsync("x@y.com")).ReturnsAsync(user);
        _signInManager
            .Setup(m => m.PasswordSignInAsync(user, "pwd", false, true))
            .ReturnsAsync(SignInResult.LockedOut);

        var result = await _svc.LoginAsync("x@y.com", "pwd", false);
        Assert.False(result.Success);
        Assert.Contains("locked", result.Error);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsFail()
    {
        var user = new ApplicationUser { IsActive = true };
        _userManager.Setup(m => m.FindByEmailAsync("x@y.com")).ReturnsAsync(user);
        _signInManager
            .Setup(m => m.PasswordSignInAsync(user, "bad", false, true))
            .ReturnsAsync(SignInResult.Failed);

        var result = await _svc.LoginAsync("x@y.com", "bad", false);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Login_Success_ReturnsOk()
    {
        var user = new ApplicationUser { IsActive = true };
        _userManager.Setup(m => m.FindByEmailAsync("x@y.com")).ReturnsAsync(user);
        _signInManager
            .Setup(m => m.PasswordSignInAsync(user, "pwd", true, true))
            .ReturnsAsync(SignInResult.Success);

        var result = await _svc.LoginAsync("x@y.com", "pwd", true);
        Assert.True(result.Success);
    }

    // ── ForgotPasswordAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsOkSilently()
    {
        _userManager.Setup(m => m.FindByEmailAsync("ghost@x.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _svc.ForgotPasswordAsync("ghost@x.com");
        Assert.True(result.Success);
        _email.Verify(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_SendsResetEmail()
    {
        var user = new ApplicationUser { Email = "u@x.com" };
        _userManager.Setup(m => m.FindByEmailAsync("u@x.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("token");
        _email.Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await _svc.ForgotPasswordAsync("u@x.com");
        Assert.True(result.Success);
        _email.Verify(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ── ResetPasswordAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ResetPassword_PasswordMismatch_ReturnsFail()
    {
        var req = new ResetPasswordRequest
        {
            Email = "u@x.com", Token = "t", NewPassword = "A", ConfirmPassword = "B"
        };
        var result = await _svc.ResetPasswordAsync(req);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ResetPassword_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByEmailAsync("ghost@x.com"))
            .ReturnsAsync((ApplicationUser?)null);

        // Build a valid base64url token so the decode doesn't throw
        var encoded = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Text.Encoding.UTF8.GetBytes("rawtoken"));

        var req = new ResetPasswordRequest
        {
            Email = "ghost@x.com", Token = encoded,
            NewPassword = "P@ss1", ConfirmPassword = "P@ss1"
        };
        var result = await _svc.ResetPasswordAsync(req);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ResetPassword_IdentityFails_ReturnsFail()
    {
        var user = new ApplicationUser { Email = "u@x.com" };
        _userManager.Setup(m => m.FindByEmailAsync("u@x.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.ResetPasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Invalid token" }));

        var encoded = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Text.Encoding.UTF8.GetBytes("rawtoken"));

        var req = new ResetPasswordRequest
        {
            Email = "u@x.com", Token = encoded,
            NewPassword = "P@ss1", ConfirmPassword = "P@ss1"
        };
        var result = await _svc.ResetPasswordAsync(req);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ResetPassword_Success_ReturnsOk()
    {
        var user = new ApplicationUser { Email = "u@x.com" };
        _userManager.Setup(m => m.FindByEmailAsync("u@x.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.ResetPasswordAsync(user, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var encoded = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Text.Encoding.UTF8.GetBytes("rawtoken"));

        var req = new ResetPasswordRequest
        {
            Email = "u@x.com", Token = encoded,
            NewPassword = "P@ss1", ConfirmPassword = "P@ss1"
        };
        var result = await _svc.ResetPasswordAsync(req);
        Assert.True(result.Success);
    }

    // ── VerifyEmailAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByIdAsync("bad")).ReturnsAsync((ApplicationUser?)null);
        var encoded = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Text.Encoding.UTF8.GetBytes("tok"));

        var result = await _svc.VerifyEmailAsync("bad", encoded);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task VerifyEmail_IdentityFails_ReturnsFail()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.ConfirmEmailAsync(user, It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Bad token" }));

        var encoded = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Text.Encoding.UTF8.GetBytes("rawtoken"));

        var result = await _svc.VerifyEmailAsync("uid", encoded);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task VerifyEmail_Success_ReturnsOk()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.ConfirmEmailAsync(user, It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var encoded = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Text.Encoding.UTF8.GetBytes("rawtoken"));

        var result = await _svc.VerifyEmailAsync("uid", encoded);
        Assert.True(result.Success);
    }

    // ── ChangePasswordAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync((ApplicationUser?)null);
        var result = await _svc.ChangePasswordAsync("uid", "old", "new");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ChangePassword_IdentityFails_ReturnsFail()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.ChangePasswordAsync(user, "old", "new"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Wrong password" }));

        var result = await _svc.ChangePasswordAsync("uid", "old", "new");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ChangePassword_Success_RefreshesSignIn()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.ChangePasswordAsync(user, "old", "new"))
            .ReturnsAsync(IdentityResult.Success);
        _signInManager.Setup(m => m.RefreshSignInAsync(user)).Returns(Task.CompletedTask);

        var result = await _svc.ChangePasswordAsync("uid", "old", "new");
        Assert.True(result.Success);
        _signInManager.Verify(m => m.RefreshSignInAsync(user), Times.Once);
    }

    // ── UpdateUserAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUser_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync((ApplicationUser?)null);
        var result = await _svc.UpdateUserAsync("uid", new UpdateUserRequest());
        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateUser_IdentityFails_ReturnsFail()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Conflict" }));

        var result = await _svc.UpdateUserAsync("uid", new UpdateUserRequest { Email = "new@x.com", IsActive = true });
        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateUser_Success_ReturnsOk()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _svc.UpdateUserAsync("uid", new UpdateUserRequest { Email = "new@x.com", IsActive = false });
        Assert.True(result.Success);
        Assert.Equal("new@x.com", user.Email);
        Assert.False(user.IsActive);
    }

    // ── DeleteUserAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync((ApplicationUser?)null);
        var result = await _svc.DeleteUserAsync("uid");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteUser_IdentityFails_ReturnsFail()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.DeleteAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Cannot delete" }));

        var result = await _svc.DeleteUserAsync("uid");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteUser_Success_ReturnsOk()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _svc.DeleteUserAsync("uid");
        Assert.True(result.Success);
    }

    // ── AddRoleToUserAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task AddRole_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync((ApplicationUser?)null);
        var result = await _svc.AddRoleToUserAsync("uid", "Admin");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task AddRole_IdentityFails_ReturnsFail()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.AddToRoleAsync(user, "Admin"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role not found" }));

        var result = await _svc.AddRoleToUserAsync("uid", "Admin");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task AddRole_Success_ReturnsOk()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.AddToRoleAsync(user, "Admin")).ReturnsAsync(IdentityResult.Success);

        var result = await _svc.AddRoleToUserAsync("uid", "Admin");
        Assert.True(result.Success);
    }

    // ── RemoveRoleFromUserAsync ───────────────────────────────────────────────

    [Fact]
    public async Task RemoveRole_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync((ApplicationUser?)null);
        var result = await _svc.RemoveRoleFromUserAsync("uid", "Admin");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RemoveRole_IdentityFails_ReturnsFail()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.RemoveFromRoleAsync(user, "Admin"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Not in role" }));

        var result = await _svc.RemoveRoleFromUserAsync("uid", "Admin");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RemoveRole_Success_ReturnsOk()
    {
        var user = new ApplicationUser { Id = "uid" };
        _userManager.Setup(m => m.FindByIdAsync("uid")).ReturnsAsync(user);
        _userManager.Setup(m => m.RemoveFromRoleAsync(user, "Admin")).ReturnsAsync(IdentityResult.Success);

        var result = await _svc.RemoveRoleFromUserAsync("uid", "Admin");
        Assert.True(result.Success);
    }

    // ── GetAllUsersAsync / GetUserByIdAsync ───────────────────────────────────

    [Fact]
    public async Task GetAllUsers_DelegatesToRepository()
    {
        var expected = new List<UserResponse> { new() { Id = "1", Email = "a@b.com" } };
        _repo.Setup(r => r.GetAllUsersAsync()).ReturnsAsync(expected);

        var result = await _svc.GetAllUsersAsync();
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetUserById_DelegatesToRepository()
    {
        var expected = new UserResponse { Id = "1", Email = "a@b.com" };
        _repo.Setup(r => r.GetUserByIdAsync("1")).ReturnsAsync(expected);

        var result = await _svc.GetUserByIdAsync("1");
        Assert.Same(expected, result);
    }

    // ── LogoutAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_CallsSignOut()
    {
        _signInManager.Setup(m => m.SignOutAsync()).Returns(Task.CompletedTask);
        await _svc.LogoutAsync();
        _signInManager.Verify(m => m.SignOutAsync(), Times.Once);
    }

    // ── CreatePrincipalForAuthCodeAsync ───────────────────────────────────────

    [Fact]
    public async Task CreatePrincipalForAuthCode_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _svc.CreatePrincipalForAuthCodeAsync(new ClaimsPrincipal(), []);
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task CreatePrincipalForAuthCode_InactiveUser_ReturnsFail()
    {
        var user = new ApplicationUser { IsActive = false };
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        var result = await _svc.CreatePrincipalForAuthCodeAsync(new ClaimsPrincipal(), []);
        Assert.False(result.Success);
        Assert.Contains("disabled", result.Error);
    }

    [Fact]
    public async Task CreatePrincipalForAuthCode_CannotSignIn_ReturnsFail()
    {
        var user = new ApplicationUser { IsActive = true };
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        _signInManager.Setup(m => m.CanSignInAsync(user)).ReturnsAsync(false);

        var result = await _svc.CreatePrincipalForAuthCodeAsync(new ClaimsPrincipal(), []);
        Assert.False(result.Success);
        Assert.Contains("cannot sign in", result.Error);
    }

    [Fact]
    public async Task CreatePrincipalForAuthCode_Success_ReturnsPrincipal_WithAllScopeDestinations()
    {
        var user = new ApplicationUser { Id = "uid", UserName = "u@x.com", Email = "u@x.com", IsActive = true };
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        _userManager.Setup(m => m.GetUserIdAsync(user)).ReturnsAsync("uid");
        _userManager.Setup(m => m.GetEmailAsync(user)).ReturnsAsync("u@x.com");
        _signInManager.Setup(m => m.CanSignInAsync(user)).ReturnsAsync(true);

        // Return a principal that has Subject, Name, Email, and Role claims so every
        // branch inside BuildPrincipalAsync is executed.
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, "uid"),
            new Claim(OpenIddictConstants.Claims.Name,    "u@x.com"),
            new Claim(OpenIddictConstants.Claims.Email,   "u@x.com"),
            new Claim(OpenIddictConstants.Claims.Role,    "Admin"),
            new Claim("custom_claim",                     "value"),
        }, "TestAuth");
        var cookiePrincipal = new ClaimsPrincipal(identity);
        _signInManager.Setup(m => m.CreateUserPrincipalAsync(user)).ReturnsAsync(cookiePrincipal);

        // Pass all scopes so every HasScope() condition in BuildPrincipalAsync is true.
        var scopes = new[]
        {
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            "roles"
        };

        var result = await _svc.CreatePrincipalForAuthCodeAsync(new ClaimsPrincipal(), scopes);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    // ── CreatePrincipalForRefreshTokenGrantAsync ──────────────────────────────

    [Fact]
    public async Task CreatePrincipalForRefreshToken_UserNotFound_ReturnsFail()
    {
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _svc.CreatePrincipalForRefreshTokenGrantAsync(new ClaimsPrincipal(), []);
        Assert.False(result.Success);
        Assert.Contains("no longer exists", result.Error);
    }

    [Fact]
    public async Task CreatePrincipalForRefreshToken_InactiveUser_ReturnsFail()
    {
        var user = new ApplicationUser { IsActive = false };
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        var result = await _svc.CreatePrincipalForRefreshTokenGrantAsync(new ClaimsPrincipal(), []);
        Assert.False(result.Success);
        Assert.Contains("disabled", result.Error);
    }

    [Fact]
    public async Task CreatePrincipalForRefreshToken_CannotSignIn_ReturnsFail()
    {
        var user = new ApplicationUser { IsActive = true };
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        _signInManager.Setup(m => m.CanSignInAsync(user)).ReturnsAsync(false);

        var result = await _svc.CreatePrincipalForRefreshTokenGrantAsync(new ClaimsPrincipal(), []);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreatePrincipalForRefreshToken_Success_ReturnsPrincipal()
    {
        var user = new ApplicationUser { Id = "uid", UserName = "u@x.com", Email = "u@x.com", IsActive = true };
        _userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        _userManager.Setup(m => m.GetUserIdAsync(user)).ReturnsAsync("uid");
        _userManager.Setup(m => m.GetEmailAsync(user)).ReturnsAsync("u@x.com");
        _signInManager.Setup(m => m.CanSignInAsync(user)).ReturnsAsync(true);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(OpenIddictConstants.Claims.Subject, "uid"),
            new Claim(OpenIddictConstants.Claims.Name,    "u@x.com"),
            new Claim(OpenIddictConstants.Claims.Email,   "u@x.com"),
        }, "TestAuth");
        _signInManager.Setup(m => m.CreateUserPrincipalAsync(user))
            .ReturnsAsync(new ClaimsPrincipal(identity));

        var result = await _svc.CreatePrincipalForRefreshTokenGrantAsync(new ClaimsPrincipal(), []);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }
}
