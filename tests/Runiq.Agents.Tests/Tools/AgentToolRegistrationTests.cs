using Runiq.Agents.Tools;

namespace Runiq.Agents.Tests.Tools;

public sealed class AgentToolRegistrationTests
{
    [Fact]
    public void FromToolType_ShouldCreateRegistration_WhenToolIsValid()
    {
        // Geçerli bir typed tool sınıfından agent tool kaydının doğru üretildiğini doğrular.
        var registration = AgentToolRegistration.FromToolType(typeof(TestWeatherTool));

        Assert.Equal(typeof(TestWeatherTool), registration.ToolType);
        Assert.Equal(typeof(TestWeatherInput), registration.InputType);
        Assert.Equal(typeof(TestWeatherOutput), registration.OutputType);
        Assert.Equal("weather", registration.Name);
        Assert.Equal("Gets current weather information for a city.", registration.Description);
    }

    [Fact]
    public void FromToolType_ShouldThrowInvalidOperationException_WhenToolTypeIsAbstract()
    {
        // Abstract tool sınıflarının runtime'da çalıştırılabilir tool olarak kabul edilmediğini doğrular.
        var exception = Assert.Throws<InvalidOperationException>(
            () => AgentToolRegistration.FromToolType(typeof(AbstractWeatherTool)));

        Assert.Contains("must be a concrete class", exception.Message);
    }

    [Fact]
    public void FromToolType_ShouldThrowInvalidOperationException_WhenTypeDoesNotImplementIRuniqTool()
    {
        // IRuniqTool<TInput,TOutput> uygulamayan sınıfların tool olarak kaydedilemediğini doğrular.
        var exception = Assert.Throws<InvalidOperationException>(
            () => AgentToolRegistration.FromToolType(typeof(NotATool)));

        Assert.Contains("must implement IRuniqTool<TInput, TOutput>", exception.Message);
    }

    [Fact]
    public void FromToolType_ShouldThrowInvalidOperationException_WhenToolDoesNotHaveAttribute()
    {
        // RuniqToolAttribute olmayan tool sınıflarının model tarafına metadata olmadan gönderilmediğini doğrular.
        var exception = Assert.Throws<InvalidOperationException>(
            () => AgentToolRegistration.FromToolType(typeof(ToolWithoutAttribute)));

        Assert.Contains("must be decorated with RuniqToolAttribute", exception.Message);
    }

    [Fact]
    public void FromToolType_ShouldThrowInvalidOperationException_WhenToolImplementsMultipleToolInterfaces()
    {
        // Bir tool sınıfının birden fazla IRuniqTool<TInput,TOutput> sözleşmesiyle belirsiz hale gelmediğini doğrular.
        var exception = Assert.Throws<InvalidOperationException>(
            () => AgentToolRegistration.FromToolType(typeof(MultiInterfaceTool)));

        Assert.Contains("must implement only one IRuniqTool<TInput, TOutput> interface", exception.Message);
    }

    [Fact]
    public void FromToolType_ShouldThrowArgumentNullException_WhenToolTypeIsNull()
    {
        // Null tool type değerinin sessizce kabul edilmediğini doğrular.
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