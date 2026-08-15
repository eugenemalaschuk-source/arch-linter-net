namespace ArchLinterNet.Cli.Commands.Badge.Application;

internal sealed record BadgeCommandOptions(string PolicyPath, bool EnsureBuilt, bool NoRestore, string? Configuration, bool ShowHelp);
