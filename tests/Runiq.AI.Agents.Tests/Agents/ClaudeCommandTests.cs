using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime.Claude;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class ClaudeCommandTests
{
    [Theory]
    [InlineData("workspace")]
    [InlineData("timeout")]
    [InlineData("line-limit")]
    [InlineData("total-limit")]
    [InlineData("executable")]
    // Verifies invalid process configuration fails before any executable or network operation.
    public void Configuration_RejectsInvalidValues(string field)
    {
        var options = Options();
        switch (field)
        {
            case "workspace": options.WorkingDirectory = "."; break;
            case "timeout": options.Timeout = TimeSpan.Zero; break;
            case "line-limit": options.MaxEventCharacters = 0; break;
            case "total-limit": options.MaxOutputCharacters = 1; break;
            case "executable": options.ExecutablePath = "claude.cmd"; break;
        }
        Assert.Equal("ClaudeConfigurationInvalid", Assert.Throws<ClaudeException>(() => ClaudeCommand.Create(options, AgentSettings(), null)).Code);
    }

    [Fact]
    // Verifies default installation discovery and an explicitly missing executable have separate error codes.
    public void Discovery_DistinguishesMissingInstallAndMissingConfiguredExecutable()
    {
        Assert.Equal("ClaudeNotInstalled", Assert.Throws<ClaudeException>(() => ClaudeCommand.ResolveExecutable(null, ".")).Code);
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe");
        Assert.Equal("ClaudeExecutableNotFound", Assert.Throws<ClaudeException>(() => ClaudeCommand.ResolveExecutable(missing)).Code);
    }

    [Fact]
    // Verifies Claude's native print mode, explicit workspace and permission-denial policy.
    public void Command_UsesNativePrintMode()
    {
        var options = Options();
        var command = ClaudeCommand.Create(options, AgentSettings(), null);
        Assert.Equal(options.WorkingDirectory, command.WorkingDirectory);
        Assert.Equal(new[] { "--print", "--output-format", "stream-json", "--verbose",
            "--include-partial-messages", "--permission-mode", "dontAsk", "--model", "sonnet", "--effort", "high" }, command.ArgumentList);
        Assert.True(command.RedirectStandardInput);
        Assert.False(command.UseShellExecute);
    }

    [Fact]
    // Verifies discovery uses an absolute PATH directory without interpreting shell wrappers.
    public void Discovery_UsesNativeExecutable()
    {
        var directory = Directory.CreateTempSubdirectory("runiq-claude-path-");
        try
        {
            var executable = Path.Combine(directory.FullName, OperatingSystem.IsWindows() ? "claude.exe" : "claude");
            File.WriteAllText(executable, "test fixture; never executed");
            Assert.Equal(executable, ClaudeCommand.ResolveExecutable(null, directory.FullName));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static ClaudeAgentConfiguration AgentSettings() =>
        new Agent("claude", "Claude", "").UseClaude(options => options.Model = "sonnet").Executor!.Claude!;

    private static ClaudeExecutorOptions Options() => new()
    {
        WorkingDirectory = Path.GetTempPath(), ExecutablePath = Environment.ProcessPath
    };
}
