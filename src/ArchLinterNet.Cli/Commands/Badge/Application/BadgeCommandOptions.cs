namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed record BadgeCommandOptions(string InputPath, bool ShowHelp);

internal sealed record ArchitectureHealthBadgeCommandOptions(
    string InputPath,
    string? OutputPath,
    bool ShowHelp);
