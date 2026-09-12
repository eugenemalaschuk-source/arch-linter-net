namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal static class BadgeSetupDiagnosticCodes
{
    internal const string InvalidConfiguration = "invalid-configuration";
    internal const string UnsupportedSchema = "unsupported-schema";
    internal const string UnsupportedContractVersion = "unsupported-contract-version";
    internal const string UnsupportedMode = "unsupported-mode";
    internal const string UnsupportedDisclosureProfile = "unsupported-disclosure-profile";
    internal const string UnsupportedBundle = "unsupported-bundle";
    internal const string UnsupportedCompatibilityPlan = "unsupported-compatibility-plan";
    internal const string MalformedIdentity = "malformed-identity";
    internal const string UnsupportedVisibility = "unsupported-visibility";
    internal const string VisibilityConflict = "visibility-conflict";
    internal const string MissingCapability = "missing-capability";
    internal const string UnsupportedPlan = "unsupported-plan";
    internal const string MissingDestination = "missing-destination";
    internal const string InvalidRenewal = "invalid-renewal";
    internal const string RenewalCostExceeded = "renewal-cost-exceeded";
    internal const string LeaseExceeded = "lease-exceeded";
    internal const string ConfigurationConflict = "configuration-conflict";
    internal const string DestinationUnavailable = "destination-unavailable";
    internal const string FirstEvidenceMissing = "first-evidence-missing";
    internal const string ArtifactInvalid = "artifact-invalid";
    internal const string ValidityExpired = "validity-expired";
    internal const string DestinationRevoked = "destination-revoked";
    internal const string ProviderQuotaFailure = "provider-quota-failure";
}

internal sealed record BadgeSetupDiagnostic(
    string Code,
    string Message,
    string Remediation,
    BadgeSetupDiagnosticSeverity Severity = BadgeSetupDiagnosticSeverity.Error,
    string? PrivateDetail = null)
{
    internal BadgeSetupPublicDiagnostic ToPublic() => new(
        Code,
        Message,
        Remediation,
        Severity.ToString().ToLowerInvariant());
}

internal static class BadgeSetupDiagnosticCatalog
{
    internal static BadgeSetupDiagnostic Create(
        string code,
        BadgeSetupDiagnosticSeverity severity = BadgeSetupDiagnosticSeverity.Error,
        string? privateDetail = null) => code switch
        {
            BadgeSetupDiagnosticCodes.InvalidConfiguration => Diagnostic(
                code,
                "The setup configuration is malformed.",
                "Provide a valid badge-relay-config/v1 configuration with the required fields.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.UnsupportedSchema => Diagnostic(
                code,
                "The setup schema is not supported by this product.",
                "Use the schema version shipped with this CLI.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.UnsupportedContractVersion => Diagnostic(
                code,
                "The setup contract version is not supported by this product.",
                "Use the contract version shipped with this CLI.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.UnsupportedMode => Diagnostic(
                code,
                "The selected publication mode is not supported.",
                "Choose none, public github-raw, or adopter-owned relay.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.UnsupportedDisclosureProfile => Diagnostic(
                code,
                "The selected disclosure profile is not supported.",
                "Choose headline-only/v1 or headline-plus-freshness/v1.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.UnsupportedBundle => Diagnostic(
                code,
                "The requested setup bundle is not supported.",
                "Use the Relay bundle identifier shipped with this CLI.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.UnsupportedCompatibilityPlan => Diagnostic(
                code,
                "The requested compatibility plan is not supported.",
                "Use the compatibility plan shipped with this CLI.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.MalformedIdentity => Diagnostic(
                code,
                "A repository or destination identity is malformed.",
                "Use an approved opaque identity containing only the documented characters and length.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.UnsupportedVisibility => Diagnostic(
                code,
                "The repository visibility value is not supported.",
                "Declare the repository as public or private before selecting a transport.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.VisibilityConflict => Diagnostic(
                code,
                "The selected transport conflicts with repository visibility.",
                "Use none or adopter-owned relay for a private repository; github-raw requires public visibility.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.MissingCapability => Diagnostic(
                code,
                "A required repository or provider capability could not be proven.",
                "Enable the named capability and run setup again; setup will not downgrade verification.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.UnsupportedPlan => Diagnostic(
                code,
                "The provider plan is unsupported or was not proven.",
                "Use a supported adopter-owned Relay plan with sufficient quota.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.MissingDestination => Diagnostic(
                code,
                "The selected transport is missing a required destination identity.",
                "Provide the adopter account and an approved opaque Relay alias.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.InvalidRenewal => Diagnostic(
                code,
                "The renewal cadence is invalid.",
                "Use a cadence from 30 through 1440 minutes; renewal never runs more than 48 times per day.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.RenewalCostExceeded => Diagnostic(
                code,
                "The requested renewal cadence exceeds the supported daily job bound.",
                "Choose a cadence that runs at most 48 renewal jobs per day.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.LeaseExceeded => Diagnostic(
                code,
                "The requested Relay lease exceeds the supported validity bound.",
                "Choose a maximum lease of 60 minutes or less.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.ConfigurationConflict => Diagnostic(
                code,
                "The requested setup conflicts with an existing approved deployment.",
                "Reuse the existing identity or review the deployment before retrying.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.DestinationUnavailable => Diagnostic(
                code,
                "The badge destination is unavailable.",
                "Check the approved destination and recover it before publishing new evidence.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.FirstEvidenceMissing => Diagnostic(
                code,
                "The badge is unavailable because no qualifying first evidence exists.",
                "Merge the first qualifying pull request through the approved workflow, then run doctor again.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.ArtifactInvalid => Diagnostic(
                code,
                "The badge artifact is missing or invalid.",
                "Regenerate the canonical artifact with the pinned workflow and verify its digest.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.ValidityExpired => Diagnostic(
                code,
                "The badge evidence has expired.",
                "Publish fresh qualifying evidence; renewal cannot extend semantic validity by itself.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.DestinationRevoked => Diagnostic(
                code,
                "The badge destination has been revoked.",
                "Revoke and recreate the approved destination through the owner flow.",
                severity,
                privateDetail),
            BadgeSetupDiagnosticCodes.ProviderQuotaFailure => Diagnostic(
                code,
                "The provider quota or storage capability is unavailable.",
                "Increase available quota or disable optional renewal, then run doctor again.",
                severity,
                privateDetail),
            _ => Diagnostic(
                BadgeSetupDiagnosticCodes.InvalidConfiguration,
                "The setup configuration is invalid.",
                "Use a supported versioned setup configuration.",
                severity,
                privateDetail),
        };

    private static BadgeSetupDiagnostic Diagnostic(
        string code,
        string message,
        string remediation,
        BadgeSetupDiagnosticSeverity severity,
        string? privateDetail) => new(code, message, remediation, severity, privateDetail);
}
