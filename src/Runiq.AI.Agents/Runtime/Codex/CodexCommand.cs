using System.Diagnostics;
using System.Text;
using Runiq.AI.Agents.Configuration;

namespace Runiq.AI.Agents.Runtime.Codex;

internal static class CodexCommand
{
    internal static ProcessStartInfo Create(CodexExecutorOptions options, CodexAgentConfiguration agent, string? sessionId)
    {
        if (!Path.IsPathFullyQualified(options.WorkingDirectory) || !Directory.Exists(options.WorkingDirectory) ||
            options.Timeout <= TimeSpan.Zero || options.Timeout.TotalMilliseconds > uint.MaxValue - 1 ||
            !Enum.IsDefined(options.Sandbox) || options.MaxEventCharacters < 128 ||
            options.MaxOutputCharacters < options.MaxEventCharacters)
            throw new CodexException("CodexConfigurationInvalid");
        if (sessionId is not null && (!Guid.TryParseExact(sessionId, "D", out _) || sessionId.Length != 36))
            throw new CodexException("CodexSessionInvalid");

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
        // Never turn a host application's API key into a separate Codex API-key session.
        start.Environment.Remove("CODEX_API_KEY");
        start.Environment.Remove("OPENAI_API_KEY");
        string[] arguments = ["exec", "--json", "--color", "never", "-c", "approval_policy=\"never\"",
            "-c", $"sandbox_mode=\"{(options.Sandbox == CodexSandboxMode.ReadOnly ? "read-only" : "workspace-write")}\""];
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (options.SkipGitRepositoryCheck) start.ArgumentList.Add("--skip-git-repo-check");
        if (sessionId is not null)
        {
            start.ArgumentList.Add("resume");
        }
        // Resume accepts its own model flag; explicit overrides preserve the agent's settings across turns.
        start.ArgumentList.Add("--model");
        start.ArgumentList.Add(agent.Model);
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add($"model_reasoning_effort=\"{agent.ReasoningEffort.ToString().ToLowerInvariant()}\"");
        if (agent.ServiceTier == CodexServiceTier.Fast)
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("service_tier=\"fast\"");
        }
        if (sessionId is not null) start.ArgumentList.Add(sessionId);
        // Prompts are written to stdin, never interpreted by a shell or exposed in process arguments.
        start.ArgumentList.Add("-");
        return start;
    }

    internal static string ResolveExecutable(string? configured, string? searchPath = null)
    {
        if (configured is not null)
        {
            if (!Path.IsPathFullyQualified(configured) ||
                (OperatingSystem.IsWindows() && !configured.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)))
                throw new CodexException("CodexConfigurationInvalid");
            if (!File.Exists(configured)) throw new CodexException("CodexExecutableNotFound");
            return configured;
        }

        var name = OperatingSystem.IsWindows() ? "codex.exe" : "codex";
        foreach (var directory in (searchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            // Ignore relative entries and the current directory to avoid workspace executable hijacking.
            var root = directory.Trim('"');
            if (!Path.IsPathFullyQualified(root)) continue;
            var candidate = Path.Combine(root, name);
            if (File.Exists(candidate)) return candidate;
            if (!OperatingSystem.IsWindows()) continue;
            var architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.X64 => "x64",
                System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
                _ => null
            };
            if (architecture is null) continue;
            var target = architecture == "x64" ? "x86_64-pc-windows-msvc" : "aarch64-pc-windows-msvc";
            foreach (var binaryDirectory in new[] { "bin", "codex" })
            {
                candidate = Path.Combine(root, "node_modules", "@openai", "codex", "node_modules", "@openai",
                    $"codex-win32-{architecture}", "vendor", target, binaryDirectory, "codex.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }
        throw new CodexException("CodexNotInstalled");
    }
}

internal sealed class CodexException(string code, int? exitCode = null, string? diagnostic = null) : Exception(code)
{
    internal string Code { get; } = code;
    internal int? ExitCode { get; } = exitCode;
    internal string? Diagnostic { get; } = diagnostic;
}
