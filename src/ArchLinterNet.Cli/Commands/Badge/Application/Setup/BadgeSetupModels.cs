using System.Text.Json.Serialization;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal enum BadgeSetupMode
{
    None,
    GithubRaw,
    Relay,
}

internal enum BadgeDisclosureProfile
{
    HeadlineOnlyV1,
    HeadlinePlusFreshnessV1,
}

internal enum BadgeRepositoryVisibility
{
    Public,
    Private,
}

internal enum BadgeSetupDiagnosticSeverity
{
    Error,
    Warning,
}

internal static class BadgeSetupContract
{
    internal const string SchemaId = "badge-relay-config/v1";
    internal const string ContractVersion = "v1";
    internal const string Bundle = "badge-relay/v1";
    internal const string CompatibilityPlan = "architecture-health-badge-relay/v1";
    internal const string HeadlineOnlyProfile = "headline-only/v1";
    internal const string HeadlinePlusFreshnessProfile = "headline-plus-freshness/v1";
    internal const int MaximumRenewalJobsPerDay = 48;
    internal const int MaximumLeaseMinutes = 60;
    internal const int MinimumRenewalCadenceMinutes = 30;
    internal const int DefaultRenewalCadenceMinutes = 1440;
    internal const int DefaultLeaseMinutes = MaximumLeaseMinutes;
    internal const int PrivateMinutesPerRenewalJob = 1;

    internal static string ToWireValue(this BadgeSetupMode mode) => mode switch
    {
        BadgeSetupMode.None => "none",
        BadgeSetupMode.GithubRaw => "github-raw",
        BadgeSetupMode.Relay => "relay",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    internal static string ToWireValue(this BadgeDisclosureProfile profile) => profile switch
    {
        BadgeDisclosureProfile.HeadlineOnlyV1 => HeadlineOnlyProfile,
        BadgeDisclosureProfile.HeadlinePlusFreshnessV1 => HeadlinePlusFreshnessProfile,
        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    };

    internal static bool TryParseMode(string? value, out BadgeSetupMode mode) => value switch
    {
        "none" => Set(BadgeSetupMode.None, out mode),
        "github-raw" => Set(BadgeSetupMode.GithubRaw, out mode),
        "relay" => Set(BadgeSetupMode.Relay, out mode),
        _ => Set(default, out mode, false),
    };

    internal static bool TryParseProfile(string? value, out BadgeDisclosureProfile profile) => value switch
    {
        HeadlineOnlyProfile => Set(BadgeDisclosureProfile.HeadlineOnlyV1, out profile),
        HeadlinePlusFreshnessProfile => Set(BadgeDisclosureProfile.HeadlinePlusFreshnessV1, out profile),
        _ => Set(default, out profile, false),
    };

    private static bool Set<T>(T value, out T destination, bool result = true)
    {
        destination = value;
        return result;
    }
}

internal sealed record BadgeSetupRepositoryContext(
    string Owner,
    string Name,
    string Visibility,
    BadgeSetupCapabilities Capabilities);

internal sealed record BadgeSetupCapabilities(
    bool HasRequiredCheck = false,
    bool HasRulesApi = false,
    bool CanUseOidc = false,
    bool CanUseGithubRaw = false,
    bool CanUseRelay = false,
    string? ProviderPlan = null);

internal sealed record BadgeSetupExistingState(
    bool HasDeployment,
    string? Mode = null,
    string? DisclosureProfile = null,
    string? Alias = null);

internal sealed record BadgeSetupRequest(
    BadgeSetupRepositoryContext Repository,
    string? Mode = null,
    string? DisclosureProfile = null,
    string? DestinationAccount = null,
    string? DestinationAlias = null,
    bool RenewalEnabled = false,
    int? RenewalCadenceMinutes = null,
    int? MaxLeaseMinutes = null,
    string SchemaId = BadgeSetupContract.SchemaId,
    string ContractVersion = BadgeSetupContract.ContractVersion,
    string Bundle = BadgeSetupContract.Bundle,
    string CompatibilityPlan = BadgeSetupContract.CompatibilityPlan,
    BadgeSetupExistingState? Existing = null);

internal sealed record BadgeSetupConfiguration(
    [property: JsonPropertyName("schema_id")] string SchemaId,
    [property: JsonPropertyName("contract_version")] string ContractVersion,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("disclosure_profile")] string DisclosureProfile,
    [property: JsonPropertyName("bundle")] string Bundle,
    [property: JsonPropertyName("compatibility_plan")] string CompatibilityPlan,
    [property: JsonPropertyName("repository")] BadgeSetupConfigurationRepository Repository,
    [property: JsonPropertyName("destination")] BadgeSetupConfigurationDestination Destination,
    [property: JsonPropertyName("renewal")] BadgeSetupConfigurationRenewal Renewal,
    [property: JsonPropertyName("pins")] BadgeSetupPins? Pins = null,
    [property: JsonPropertyName("managed_files")] IReadOnlyList<string>? ManagedFiles = null);

internal sealed record BadgeSetupConfigurationRepository(
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("visibility")] string Visibility);

internal sealed record BadgeSetupConfigurationDestination(
    [property: JsonPropertyName("alias")] string? Alias,
    [property: JsonPropertyName("account")] string? Account = null,
    [property: JsonPropertyName("endpoint")] string? Endpoint = null);

internal sealed record BadgeSetupConfigurationRenewal(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("cadence_minutes")] int CadenceMinutes,
    [property: JsonPropertyName("max_lease_minutes")] int MaxLeaseMinutes);

internal sealed record BadgeSetupPins(
    [property: JsonPropertyName("workflow_ref")] string? WorkflowRef = null,
    [property: JsonPropertyName("workflow_sha")] string? WorkflowSha = null,
    [property: JsonPropertyName("action_ref")] string? ActionRef = null,
    [property: JsonPropertyName("bundle_digest")] string? BundleDigest = null);

internal sealed record BadgeSetupPrerequisite(
    string Code,
    string Description,
    bool Required,
    bool Satisfied);

internal sealed record BadgeSetupCostEstimate(
    bool RenewalEnabled,
    int CadenceMinutes,
    int MaxLeaseMinutes,
    int JobsPerDay,
    int JobsPerThirtyDayMonth,
    int GithubPrivateMinutesPerDay,
    int GithubPrivateMinutesPerThirtyDayMonth,
    string ProviderQuotaAssumption)
{
    internal static BadgeSetupCostEstimate None { get; } = new(
        RenewalEnabled: false,
        CadenceMinutes: 0,
        MaxLeaseMinutes: 0,
        JobsPerDay: 0,
        JobsPerThirtyDayMonth: 0,
        GithubPrivateMinutesPerDay: 0,
        GithubPrivateMinutesPerThirtyDayMonth: 0,
        ProviderQuotaAssumption: "No external renewal or hosting quota is required.");
}

internal sealed record BadgeSetupPlan(
    bool IsValid,
    string Mode,
    string DisclosureProfile,
    bool ExternalCallsExpected,
    bool PublicEndpointExpected,
    IReadOnlyList<BadgeSetupPrerequisite> Prerequisites,
    BadgeSetupCostEstimate Cost,
    IReadOnlyList<string> PlannedChanges,
    IReadOnlyList<BadgeSetupDiagnostic> Diagnostics);

internal sealed record BadgeSetupPlanResult(
    BadgeSetupPlan Plan,
    IReadOnlyList<BadgeSetupDiagnostic> Diagnostics)
{
    internal bool IsValid => Plan.IsValid;
}

internal sealed record BadgeSetupConfigurationParseResult(
    bool IsValid,
    BadgeSetupConfiguration? Configuration,
    IReadOnlyList<BadgeSetupDiagnostic> Diagnostics);

internal sealed record BadgeDoctorObservations(
    bool DestinationReachable = true,
    bool FirstEvidenceAvailable = true,
    bool ArtifactValid = true,
    bool ValidityCurrent = true,
    bool DestinationRevoked = false,
    bool ProviderQuotaAvailable = true,
    string? PrivateContext = null,
    string? Token = null,
    string? RawProviderResponse = null);

internal sealed record BadgeSetupPublicDiagnostic(
    string Code,
    string Message,
    string Remediation,
    string Severity);

internal sealed record BadgeDoctorReport(
    bool Available,
    IReadOnlyList<BadgeSetupDiagnostic> Diagnostics)
{
    internal BadgeDoctorPublicReport ToPublic() => new(
        Available,
        Diagnostics.Select(static diagnostic => diagnostic.ToPublic()).ToArray());
}

internal sealed record BadgeDoctorPublicReport(
    bool Available,
    IReadOnlyList<BadgeSetupPublicDiagnostic> Diagnostics);
