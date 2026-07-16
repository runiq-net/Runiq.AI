using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Runiq.AI.Rag.Configuration;

namespace Runiq.AI.Rag.Runtime;

internal sealed class RagIngestionHostedService(IRagIndexRegistry registry, RagIngestionManager manager, ILogger<RagIngestionHostedService>? logger = null) : BackgroundService
{
    private readonly ILogger<RagIngestionHostedService> log = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RagIngestionHostedService>.Instance;
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var index in registry.Registrations.Where(r => r.IngestionStrategy.Kind == RagIngestionStrategyKind.OnStartup))
        {
            var operation = await manager.StartAsync(index.Name, RagIngestionOperationReason.Startup, cancellationToken).ConfigureAwait(false);
            if (operation.State == RagIngestionOperationState.Cancelled)
                throw new OperationCanceledException(cancellationToken);
            if (operation.State == RagIngestionOperationState.Failed)
                throw new InvalidOperationException($"Startup ingestion failed for RAG index '{index.Name}'.");
        }
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var operations = new List<Task>();
        foreach (var index in registry.Registrations.Where(r => r.IngestionStrategy.Kind == RagIngestionStrategyKind.BackgroundOnStartup))
            operations.Add(RunSafelyAsync(index.Name, RagIngestionOperationReason.BackgroundStartup, stoppingToken));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                var now = DateTimeOffset.Now;
                foreach (var index in registry.Registrations.Where(r => r.IngestionStrategy.Kind == RagIngestionStrategyKind.Scheduled && Matches(r.IngestionStrategy.ScheduleExpression!, now)))
                    operations.Add(RunSafelyAsync(index.Name, RagIngestionOperationReason.Scheduled, stoppingToken));
                operations.RemoveAll(task => task.IsCompleted);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            await Task.WhenAll(operations).ConfigureAwait(false);
        }
    }

    private async Task RunSafelyAsync(string indexName, RagIngestionOperationReason reason, CancellationToken token)
    {
        try { await manager.StartAsync(indexName, reason, token).ConfigureAwait(false); }
        catch (InvalidOperationException) { log.LogDebug("Skipped managed ingestion trigger for active index {IndexName}.", indexName); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private static bool Matches(string expression, DateTimeOffset time)
    {
        var fields = expression.Split(' ');
        int[] values = [time.Minute, time.Hour, time.Day, time.Month, (int)time.DayOfWeek];
        int[] minimums = [0, 0, 1, 1, 0];
        return fields.Select((field, index) => MatchField(field, values[index], minimums[index])).All(match => match);
    }

    private static bool MatchField(string field, int value, int minimum) => field.Split(',').Any(part =>
    {
        var stepParts = part.Split('/');
        var range = stepParts[0];
        var step = stepParts.Length == 2 && int.TryParse(stepParts[1], out var parsedStep) ? parsedStep : 1;
        var bounds = range.Split('-');
        var start = range == "*" ? minimum : int.Parse(bounds[0], System.Globalization.CultureInfo.InvariantCulture);
        var end = range == "*" ? int.MaxValue : bounds.Length == 2 ? int.Parse(bounds[1], System.Globalization.CultureInfo.InvariantCulture) : start;
        return value >= start && value <= end && (value - start) % step == 0;
    });
}
