using BlazorFluent.Web.Infrastructure.Observability;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BlazorFluent.Web.Tests.Observability;

public class CircuitLifecycleHandlerTests
{
    // Circuit is sealed with an internal constructor, so lifecycle callbacks cannot be invoked in isolation.
    [Fact]
    public void Handler_IsCircuitHandler_AndConstructsWithLogger()
    {
        var handler = new BlazorCircuitObservabilityHandler(new Mock<ILogger<BlazorCircuitObservabilityHandler>>().Object);

        handler.Should().BeAssignableTo<CircuitHandler>();
    }
}