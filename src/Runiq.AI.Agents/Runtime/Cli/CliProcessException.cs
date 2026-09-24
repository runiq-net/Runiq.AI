namespace Runiq.AI.Agents.Runtime.Cli;

// Provider adapters add their own error prefix at the execution boundary.
internal sealed class CliProcessException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}
