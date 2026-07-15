using Runiq.AI.Cli;
using Runiq.AI.Cli.Commands;

var app = new CliApplication(
    new InitCommand().Execute);

Environment.ExitCode = app.Run(args);

