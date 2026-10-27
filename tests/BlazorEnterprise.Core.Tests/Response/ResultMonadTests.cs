using BlazorEnterprise.Shared.Common;
using FluentAssertions;
using Xunit;

namespace BlazorEnterprise.Core.Tests.Response;

public class ResultMonadTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = Result.Success("Operation completed");
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Operation completed");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_CreatesFailedResultWithErrors()
    {
        var result = Result.Failure("Invalid parameter");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid parameter");
        result.Errors.Should().ContainSingle().Which.Should().Be("Invalid parameter");
    }

    [Fact]
    public void GenericResult_PreservesTypedPayload()
    {
        var result = Result<int>.Success(42, "Calculated");
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Data.Should().Be(42);
    }
}
