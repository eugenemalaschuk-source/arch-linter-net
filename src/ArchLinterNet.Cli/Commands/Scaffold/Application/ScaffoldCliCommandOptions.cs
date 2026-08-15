namespace ArchLinterNet.Cli.Commands.Scaffold.Application;

internal sealed record ScaffoldCliCommandOptions(
    string Profile,
    string? ModuleName,
    string? CommandToken,
    bool DryRun,
    bool Force,
    string? ModelName,
    string? AbstractionName,
    string? ExceptionName);
