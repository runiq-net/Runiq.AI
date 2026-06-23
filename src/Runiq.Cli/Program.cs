using Runiq.Cli;
using Runiq.Cli.Commands;

var app = new CliApplication(
    new InitCommand().Execute);

Environment.ExitCode = app.Run(args);
