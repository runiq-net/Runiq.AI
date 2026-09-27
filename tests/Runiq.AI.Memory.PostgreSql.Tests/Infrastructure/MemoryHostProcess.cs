using System.Diagnostics;

namespace Runiq.AI.Memory.PostgreSql.Tests.Infrastructure;

internal sealed class MemoryHostProcess : IDisposable
{
    private readonly Process process;
    private readonly Task<string> errors;

    internal MemoryHostProcess(string mode, string schema, string? thread = null, string? key = null)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Runiq.AI.slnx"))) root = root.Parent;
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var assembly = Path.Combine(root!.FullName, "tests", "Hosts", "Runiq.AI.Memory.PostgreSql.TestHost", "bin", configuration,
            "net10.0", "Runiq.AI.Memory.PostgreSql.TestHost.dll");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
        };
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add(mode);
        start.ArgumentList.Add(schema);
        if (thread is not null) start.ArgumentList.Add(thread);
        if (key is not null) start.ArgumentList.Add(key);
        start.Environment["RUNIQ_MEMORY_TEST_CONNECTION"] = PostgreSqlTestDatabase.ConnectionString;
        process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start the test host.");
        errors = process.StandardError.ReadToEndAsync();
    }

    internal async Task ReadyAsync() => Assert.Equal("READY", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)));
    internal Task ReleaseAsync() => process.StandardInput.WriteLineAsync("GO");

    internal async Task<string> FinishAsync()
    {
        var output = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(process.ExitCode == 0, await errors);
        return (await output).Trim();
    }

    public void Dispose()
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        process.Dispose();
    }
}
