using RCD.Core.Models;

namespace RCD.Core.Tests.Models;

public class ResultTests
{
    [Fact]
    public void Ok_ReturnsSuccess()
    {
        var result = Result.Ok();
        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_ReturnsFailureWithError()
    {
        var result = Result.Fail("something went wrong");
        Assert.False(result.Success);
        Assert.Equal("something went wrong", result.Error);
    }

    [Fact]
    public void Generic_Ok_ReturnsSuccessWithData()
    {
        var result = Result<int>.Ok(42);
        Assert.True(result.Success);
        Assert.Equal(42, result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Generic_Fail_ReturnsFailureWithError()
    {
        var result = Result<int>.Fail("bad input");
        Assert.False(result.Success);
        Assert.Equal("bad input", result.Error);
        Assert.Equal(default, result.Data);
    }

    [Fact]
    public void Generic_Ok_WithReferenceType_ReturnsData()
    {
        var result = Result<string>.Ok("hello");
        Assert.True(result.Success);
        Assert.Equal("hello", result.Data);
    }

    [Fact]
    public void Generic_Fail_InheritsFromResult()
    {
        Result result = Result<string>.Fail("err");
        Assert.False(result.Success);
        Assert.Equal("err", result.Error);
    }
}
