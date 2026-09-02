using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

/// <summary>
/// Maps the reader's closed SARIF trust decision into the smaller report presentation vocabulary
/// without recomputing any evidence trust or producer context.
/// </summary>
internal static class ArchitecturePrReportExternalEvidenceTrustStateMapper
{
    internal static ArchitecturePrReportExternalEvidenceTrustState Map(SarifEvidenceTrustStatus status) => status switch
    {
        SarifEvidenceTrustStatus.Valid => ArchitecturePrReportExternalEvidenceTrustState.Current,
        SarifEvidenceTrustStatus.MissingRevision or SarifEvidenceTrustStatus.WrongRevision =>
            ArchitecturePrReportExternalEvidenceTrustState.Stale,
        SarifEvidenceTrustStatus.MissingLogicalId
            or SarifEvidenceTrustStatus.WrongLogicalId
            or SarifEvidenceTrustStatus.MissingRepository
            or SarifEvidenceTrustStatus.WrongRepository
            or SarifEvidenceTrustStatus.MissingScope
            or SarifEvidenceTrustStatus.WrongScope
            or SarifEvidenceTrustStatus.ConflictingContext =>
            ArchitecturePrReportExternalEvidenceTrustState.WrongContext,
        SarifEvidenceTrustStatus.MissingRequiredInput => ArchitecturePrReportExternalEvidenceTrustState.Missing,
        SarifEvidenceTrustStatus.OptionalNotConfigured
            or SarifEvidenceTrustStatus.MissingOptionalInput =>
            ArchitecturePrReportExternalEvidenceTrustState.NotConfigured,
        _ => ArchitecturePrReportExternalEvidenceTrustState.Invalid,
    };
}
