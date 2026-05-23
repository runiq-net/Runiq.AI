using Runiq.Agents.Tools;

namespace SampleApp.MyTools;

/// <summary>
/// Input parametresi almadan sunucu saatini dönen örnek tool'dur.
/// </summary>
[RuniqTool(
    name: "server_time",
    description: "Gets the current server time without requiring input.")]
public sealed class ServerTimeTool : IRuniqTool<EmptyToolInput, ServerTimeOutput>
{
    /// <inheritdoc />
    public Task<ServerTimeOutput> ExecuteAsync(
        EmptyToolInput input,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return Task.FromResult(new ServerTimeOutput(
            UtcTime: now,
            UnixTimeSeconds: now.ToUnixTimeSeconds(),
            Summary: $"Current server UTC time is {now:yyyy-MM-dd HH:mm:ss}."
        ));
    }
}

/// <summary>
/// ServerTimeTool sonucunu temsil eder.
/// </summary>
public sealed record ServerTimeOutput(
    DateTimeOffset UtcTime,
    long UnixTimeSeconds,
    string Summary);