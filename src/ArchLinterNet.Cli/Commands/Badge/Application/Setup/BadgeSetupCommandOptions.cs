namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal sealed record BadgeSetupCommandOptions(
    string? InputPath,
    string? OutputDirectory,
    string? Repository,
    string? Visibility,
    string? Mode,
    string? DisclosureProfile,
    string? Account,
    string? Alias,
    string? Endpoint,
    string? ProviderPlan,
    int? CadenceMinutes,
    int? MaxLeaseMinutes,
    bool RenewalEnabled,
    bool DryRun,
    bool PublicDiagnostics,
    string Format,
    bool ShowHelp);
