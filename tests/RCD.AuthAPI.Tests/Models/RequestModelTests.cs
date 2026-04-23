using RCD.AuthAPI.Models.Requests;

namespace RCD.AuthAPI.Tests.Models;

public class RequestModelTests
{
    [Fact]
    public void ChangePasswordRequest_Properties_DefaultToEmpty()
    {
        var r = new ChangePasswordRequest();
        Assert.Equal(string.Empty, r.CurrentPassword);
        Assert.Equal(string.Empty, r.NewPassword);
        Assert.Equal(string.Empty, r.ConfirmPassword);
    }

    [Fact]
    public void ChangePasswordRequest_Properties_CanBeSet()
    {
        var r = new ChangePasswordRequest
        {
            CurrentPassword = "old",
            NewPassword     = "new",
            ConfirmPassword = "new"
        };
        Assert.Equal("old", r.CurrentPassword);
        Assert.Equal("new", r.NewPassword);
        Assert.Equal("new", r.ConfirmPassword);
    }

    [Fact]
    public void RefreshTokenRequest_Property_DefaultsToEmpty()
    {
        var r = new RefreshTokenRequest();
        Assert.Equal(string.Empty, r.RefreshToken);
    }

    [Fact]
    public void RefreshTokenRequest_Property_CanBeSet()
    {
        var r = new RefreshTokenRequest { RefreshToken = "tok" };
        Assert.Equal("tok", r.RefreshToken);
    }
}
