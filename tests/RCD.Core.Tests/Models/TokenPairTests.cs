using RCD.Core.Models;

namespace RCD.Core.Tests.Models;

public class TokenPairTests
{
    [Fact]
    public void DefaultProperties_AreEmptyStrings()
    {
        var pair = new TokenPair();
        Assert.Equal(string.Empty, pair.AccessToken);
        Assert.Equal(string.Empty, pair.RefreshToken);
    }

    [Fact]
    public void Properties_CanBeSetAndRead()
    {
        var pair = new TokenPair
        {
            AccessToken  = "access.jwt.token",
            RefreshToken = "refresh.jwt.token"
        };
        Assert.Equal("access.jwt.token",  pair.AccessToken);
        Assert.Equal("refresh.jwt.token", pair.RefreshToken);
    }
}
