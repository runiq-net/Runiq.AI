using Microsoft.Extensions.DependencyInjection;
using Runiq.Agents.Tools;

namespace Runiq.Agents.Tests.Tools;

public sealed class AgentToolInvokerTests
{
    [Fact]
    public async Task InvokeAsync_ShouldExecuteRegisteredTool_WhenInputJsonIsValid()
    {
        // Agent üzerinde kayıtlı tool'un geçerli JSON input ile çalıştırıldığını ve output JSON döndüğünü doğrular.
        var agent = CreateAgent()
            .AddTool<TestWeatherTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "weather",
            """{"city":"Istanbul"}""");

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.OutputJson);
        Assert.Contains("Istanbul", result.OutputJson);
        Assert.Contains("23", result.OutputJson);
    }

    [Fact]
    public async Task InvokeAsync_ShouldResolveToolNameCaseInsensitive_WhenToolNameUsesDifferentCasing()
    {
        // Tool adının model tarafından farklı casing ile çağrılması durumunda da eşleştiğini doğrular.
        var agent = CreateAgent()
            .AddTool<TestWeatherTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "WEATHER",
            """{"city":"Istanbul"}""");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputJson);
        Assert.Contains("Istanbul", result.OutputJson);
    }

    [Fact]
    public async Task InvokeAsync_ShouldUseEmptyObject_WhenArgumentsJsonIsEmpty()
    {
        // Boş argumentsJson geldiğinde invoker'ın bunu boş JSON object olarak ele aldığını doğrular.
        var agent = CreateAgent()
            .AddTool<DefaultInputTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "default_input",
            "");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputJson);
        Assert.Contains("default-city", result.OutputJson);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnFailure_WhenToolNameIsEmpty()
    {
        // Boş tool adının çalıştırma denemesi yapılmadan anlamlı failure sonucu ürettiğini doğrular.
        var agent = CreateAgent()
            .AddTool<TestWeatherTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "",
            """{"city":"Istanbul"}""");

        Assert.False(result.IsSuccess);
        Assert.Equal("ToolNameRequired", result.ErrorCode);
        Assert.Equal("Tool name cannot be empty.", result.ErrorMessage);
        Assert.Null(result.OutputJson);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnFailure_WhenToolIsNotRegisteredOnAgent()
    {
        // Agent üzerinde kayıtlı olmayan tool çağrılarının ToolNotFound olarak döndüğünü doğrular.
        var agent = CreateAgent();
        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "weather",
            """{"city":"Istanbul"}""");

        Assert.False(result.IsSuccess);
        Assert.Equal("ToolNotFound", result.ErrorCode);
        Assert.Contains("does not have a tool named 'weather'", result.ErrorMessage);
        Assert.Null(result.OutputJson);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnFailure_WhenArgumentsJsonIsInvalid()
    {
        // Geçersiz JSON input değerinin ToolInputInvalid failure sonucuna çevrildiğini doğrular.
        var agent = CreateAgent()
            .AddTool<TestWeatherTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "weather",
            "{ invalid-json");

        Assert.False(result.IsSuccess);
        Assert.Equal("ToolInputInvalid", result.ErrorCode);
        Assert.Contains("input could not be deserialized", result.ErrorMessage);
        Assert.Null(result.OutputJson);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReturnFailure_WhenToolThrowsException()
    {
        // Tool çalıştırma sırasında oluşan exception'ın ToolExecutionFailed failure sonucuna çevrildiğini doğrular.
        var agent = CreateAgent()
            .AddTool<FailingTool>();

        var invoker = CreateInvoker();

        var result = await invoker.InvokeAsync(
            agent,
            "failing",
            """{"value":"test"}""");

        Assert.False(result.IsSuccess);
        Assert.Equal("ToolExecutionFailed", result.ErrorCode);
        Assert.Equal("Tool failed intentionally.", result.ErrorMessage);
        Assert.Null(result.OutputJson);
    }

    private static Agent CreateAgent()
    {
        return new Agent(
            id: "test-agent",
            name: "Test Agent",
            instructions: "Test instructions.",
            model: "openai/gpt-5",
            apiKey: "test-key");
    }

    private static AgentToolInvoker CreateInvoker()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        return new AgentToolInvoker(serviceProvider);
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

    [RuniqTool(
        name: "default_input",
        description: "Uses default input values.")]
    private sealed class DefaultInputTool : IRuniqTool<DefaultInput, DefaultInputOutput>
    {
        public Task<DefaultInputOutput> ExecuteAsync(
            DefaultInput input,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DefaultInputOutput(input.City));
        }
    }

    [RuniqTool(
        name: "failing",
        description: "Throws an exception for test purposes.")]
    private sealed class FailingTool : IRuniqTool<FailingInput, FailingOutput>
    {
        public Task<FailingOutput> ExecuteAsync(
            FailingInput input,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Tool failed intentionally.");
        }
    }

    private sealed record TestWeatherInput(string City);

    private sealed record TestWeatherOutput(
        string City,
        int TemperatureCelsius);

    private sealed record DefaultInput(string City = "default-city");

    private sealed record DefaultInputOutput(string City);

    private sealed record FailingInput(string Value);

    private sealed record FailingOutput(string Value);
}