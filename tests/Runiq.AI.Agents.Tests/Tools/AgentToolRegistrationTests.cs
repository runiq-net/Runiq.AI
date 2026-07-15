using Runiq.AI.Agents.Tools;

namespace Runiq.AI.Agents.Tests.Tools;

public sealed class AgentToolRegistrationTests
{
    // Verifies that a valid typed tool class produces the expected agent tool registration.
    [Fact]
    public void FromToolType_ShouldCreateRegistration_WhenToolIsValid()
    {
        var registration = AgentToolRegistration.FromToolType(typeof(TestWeatherTool));

        Assert.Equal(typeof(TestWeatherTool), registration.ToolType);
        Assert.Equal(typeof(TestWeatherInput), registration.InputType);
        Assert.Equal(typeof(TestWeatherOutput), registration.OutputType);
        Assert.Equal("weather", registration.Name);
        Assert.Equal("Gets current weather information for a city.", registration.Description);
    }

    // Verifies that abstract tool types are rejected because they cannot be executed.
    [Fact]
    public void FromToolType_ShouldThrowInvalidOperationException_WhenToolTypeIsAbstract()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => AgentToolRegistration.FromToolType(typeof(AbstractWeatherTool)));

        Assert.Contains("must be a concrete class", exception.Message);
    }

    // Verifies that types without IRuniqTool<TInput, TOutput> cannot be registered as tools.
    [Fact]
    public void FromToolType_ShouldThrowInvalidOperationException_WhenTypeDoesNotImplementIRuniqTool()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => AgentToolRegistration.FromToolType(typeof(NotATool)));

        Assert.Contains("must implement IRuniqTool<TInput, TOutput>", exception.Message);
    }

    // Verifies that tool classes must declare RuniqToolAttribute metadata.
    [Fact]
    public void FromToolType_ShouldThrowInvalidOperationException_WhenToolDoesNotHaveAttribute()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => AgentToolRegistration.FromToolType(typeof(ToolWithoutAttribute)));

        Assert.Contains("must be decorated with RuniqToolAttribute", exception.Message);
    }

    // Verifies that tool classes implementing multiple tool contracts are rejected as ambiguous.
    [Fact]
    public void FromToolType_ShouldThrowInvalidOperationException_WhenToolImplementsMultipleToolInterfaces()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => AgentToolRegistration.FromToolType(typeof(MultiInterfaceTool)));

        Assert.Contains("must implement only one IRuniqTool<TInput, TOutput> interface", exception.Message);
    }

    // Verifies that null tool type arguments are rejected explicitly.
    [Fact]
    public void FromToolType_ShouldThrowArgumentNullException_WhenToolTypeIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => AgentToolRegistration.FromToolType(null!));
    }

    [RuniqTool(
        name: "weather",
        description: "Gets current weather information for a city.")]
    private sealed class TestWeatherTool : IRuniqTool<TestWeatherInput, TestWeatherOutput>
    {
        public Task<TestWeatherOutput> ExecuteAsync(
            TestWeatherInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestWeatherOutput(
                City: input.City,
                TemperatureCelsius: 23));
        }
    }

    private abstract class AbstractWeatherTool : IRuniqTool<TestWeatherInput, TestWeatherOutput>
    {
        public abstract Task<TestWeatherOutput> ExecuteAsync(
            TestWeatherInput input,
            CancellationToken cancellationToken = default);
    }

    private sealed class NotATool
    {
    }

    private sealed class ToolWithoutAttribute : IRuniqTool<TestWeatherInput, TestWeatherOutput>
    {
        public Task<TestWeatherOutput> ExecuteAsync(
            TestWeatherInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestWeatherOutput(
                City: input.City,
                TemperatureCelsius: 23));
        }
    }

    [RuniqTool("multi")]
    private sealed class MultiInterfaceTool :
        IRuniqTool<TestWeatherInput, TestWeatherOutput>,
        IRuniqTool<OtherInput, OtherOutput>
    {
        public Task<TestWeatherOutput> ExecuteAsync(
            TestWeatherInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TestWeatherOutput(
                City: input.City,
                TemperatureCelsius: 23));
        }

        public Task<OtherOutput> ExecuteAsync(
            OtherInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new OtherOutput(input.Value));
        }
    }

    private sealed record TestWeatherInput(string City);

    private sealed record TestWeatherOutput(
        string City,
        int TemperatureCelsius);

    private sealed record OtherInput(string Value);

    private sealed record OtherOutput(string Value);
}

