using System.Diagnostics;
using System.Text;
using Runiq.AI.Agents.Configuration;

namespace Runiq.AI.Agents.Runtime.Claude;

internal static class ClaudeCommand
{
    internal static ProcessStartInfo Create(ClaudeExecutorOptions options, ClaudeAgentConfiguration agent, string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(options.WorkingDirectory) ||
            !Path.IsPathFullyQualified(options.WorkingDirectory) || !Directory.Exists(options.WorkingDirectory) ||
            options.Timeout <= TimeSpan.Zero || options.Timeout.TotalMilliseconds > uint.MaxValue - 1 ||
            options.MaxEventCharacters < 128 ||
            options.MaxOutputCharacters < options.MaxEventCharacters)
            throw new ClaudeException("ClaudeConfigurationInvalid");
        if (sessionId is not null && (!Guid.TryParseExact(sessionId, "D", out _) || sessionId.Length != 36))
            throw new ClaudeException("ClaudeSessionInvalid");

        var start = new ProcessStartInfo
        {
            FileName = ResolveExecutable(options.ExecutablePath),
            WorkingDirectory = options.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        // Keep CLI authentication intact, but prevent inherited effort from overriding this agent's explicit choice.
        start.Environment.Remove("CLAUDE_CODE_EFFORT_LEVEL");
        foreach (var argument in new[] { "--print", "--output-format", "stream-json", "--verbose",
                     "--include-partial-messages", "--permission-mode", "dontAsk" })
            start.ArgumentList.Add(argument);
        start.ArgumentList.Add("--model");
        start.ArgumentList.Add(agent.Model);
        start.ArgumentList.Add("--effort");
        start.ArgumentList.Add(agent.ReasoningEffort.ToString().ToLowerInvariant());
        if (sessionId is not null)
        {
            start.ArgumentList.Add("--resume");
            start.ArgumentList.Add(sessionId);
        }
        return start;
    }

    internal static string ResolveExecutable(string? configured, string? searchPath = null)
    {
        if (configured is not null)
        {
            if (!Path.IsPathFullyQualified(configured) ||
                (OperatingSystem.IsWindows() && !configured.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                throw new ClaudeException("ClaudeConfigurationInvalid");
            if (!File.Exists(configured)) throw new ClaudeException("ClaudeExecutableNotFound");
            return configured;
        }

        var name = OperatingSystem.IsWindows() ? "claude.exe" : "claude";
        foreach (var directory in (searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            // Ignore relative entries and the current directory to avoid workspace executable hijacking.
            var root = directory.Trim('"');
            if (!Path.IsPathFullyQualified(root)) continue;
            var candidate = Path.Combine(root, name);
            if (File.Exists(candidate)) return candidate;
            if (!OperatingSystem.IsWindows()) continue;
            candidate = Path.Combine(root, "node_modules", "@anthropic-ai", "claude-code", "bin", "claude.exe");
            if (File.Exists(candidate)) return candidate;
        }
        throw new ClaudeException("ClaudeNotInstalled");
    }
}

internal sealed class ClaudeException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}
