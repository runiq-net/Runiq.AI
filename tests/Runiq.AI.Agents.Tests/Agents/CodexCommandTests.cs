using Runiq.AI.Agents.Configuration;
using Runiq.AI.Agents.Runtime.Codex;

namespace Runiq.AI.Agents.Tests.Agents;

public sealed class CodexCommandTests
{
    [Theory]
    [InlineData("workspace")]
    [InlineData("timeout")]
    [InlineData("sandbox")]
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
            case "sandbox": options.Sandbox = (CodexSandboxMode)100; break;
            case "line-limit": options.MaxEventCharacters = 0; break;
            case "total-limit": options.MaxOutputCharacters = 1; break;
            case "executable": options.ExecutablePath = "codex.cmd"; break;
        }
        Assert.Equal("CodexConfigurationInvalid", Assert.Throws<CodexException>(() => CodexCommand.Create(options, null)).Code);
    }

    [Fact]
    // Verifies default installation discovery and an explicitly missing executable have separate error codes.
    public void Discovery_DistinguishesMissingInstallAndMissingConfiguredExecutable()
    {
        Assert.Equal("CodexNotInstalled", Assert.Throws<CodexException>(() => CodexCommand.ResolveExecutable(null, ".")).Code);
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe");
        Assert.Equal("CodexExecutableNotFound", Assert.Throws<CodexException>(() => CodexCommand.ResolveExecutable(missing)).Code);
    }

    [Theory]
    [InlineData(CodexSandboxMode.ReadOnly, "read-only")]
    [InlineData(CodexSandboxMode.WorkspaceWrite, "workspace-write")]
    // Verifies sandbox and workspace are explicit, arguments are separated, and stdin is used for prompts.
    public void Command_PreservesSandboxAndWorkspace(CodexSandboxMode sandbox, string expected)
    {
        var options = Options();
        options.Sandbox = sandbox;
        options.SkipGitRepositoryCheck = true;
        var command = CodexCommand.Create(options, null);
        Assert.Equal(options.WorkingDirectory, command.WorkingDirectory);
        Assert.Contains($"sandbox_mode=\"{expected}\"", command.ArgumentList);
        Assert.Contains("approval_policy=\"never\"", command.ArgumentList);
        Assert.Contains("--skip-git-repo-check", command.ArgumentList);
        Assert.DoesNotContain("--dangerously-bypass-approvals-and-sandbox", command.ArgumentList);
        Assert.Equal("-", command.ArgumentList[^1]);
    }

    [Fact]
    // Verifies discovery uses an absolute PATH directory without interpreting shell wrappers.
    public void Discovery_UsesNativeExecutable()
    {
        var directory = Directory.CreateTempSubdirectory("runiq-codex-path-");
        try
        {
            var executable = Path.Combine(directory.FullName, OperatingSystem.IsWindows() ? "codex.exe" : "codex");
            File.WriteAllText(executable, "test fixture; never executed");
            Assert.Equal(executable, CodexCommand.ResolveExecutable(null, directory.FullName));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static CodexExecutorOptions Options() => new()
    {
        WorkingDirectory = Path.GetTempPath(), ExecutablePath = Environment.ProcessPath
    };
}
