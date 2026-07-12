using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

// Human-readable and CI-JSON rendering for the contextual dependency/allow-only diagnostics
// (ContextDependencyDiagnostic/ContextAllowOnlyDiagnostic). Split into its own partial file to
// keep ArchitectureDiagnosticFormatter.cs under the repository's 800-line decomposition limit,
// mirroring ArchitectureDiagnosticFormatter.Classification.cs.
public sealed partial class ArchitectureDiagnosticFormatter
{
    // Contextual diagnostics are structurally distinct from DependencyDiagnostic (a separate record
    // type, not a subclass), so they need their own human-readable context — this is also what makes
    // a contextual violation visibly distinguishable from a namespace/layer dependency violation in
    // human-readable output, per the contextual-dependency-contracts/contextual-allow-only-contracts specs.
    private static string FormatContextDependencyContextForHumans(ContextDependencyDiagnostic diagnostic)
    {
        return $" (kind: context_dependency, source_role: {diagnostic.SourceRole ?? "?"}, " +
               $"source_metadata: {FormatMetadataForHumans(diagnostic.SourceMetadata)}, " +
               $"target_role: {diagnostic.TargetRole ?? "?"}, " +
               $"target_metadata: {FormatMetadataForHumans(diagnostic.TargetMetadata)}, " +
               $"matched_selector: {diagnostic.MatchedSelector ?? "?"})";
    }

    private static string FormatContextAllowOnlyContextForHumans(ContextAllowOnlyDiagnostic diagnostic)
    {
        return $" (kind: context_allow_only, source_role: {diagnostic.SourceRole ?? "?"}, " +
               $"source_metadata: {FormatMetadataForHumans(diagnostic.SourceMetadata)}, " +
               $"target_role: {diagnostic.TargetRole ?? "?"}, " +
               $"target_metadata: {FormatMetadataForHumans(diagnostic.TargetMetadata)}, " +
               $"matched_selector: {diagnostic.MatchedSelector ?? "?"})";
    }

    private static string FormatMetadataForHumans(IReadOnlyDictionary<string, object>? metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return "{}";
        }

        return "{" + string.Join(", ", metadata
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => $"{entry.Key}={entry.Value}")) + "}";
    }

    private static void ApplyContextDependencyCiFields(ContextDependencyDiagnostic diagnostic, Dictionary<string, object?> obj)
    {
        if (diagnostic.SourceRole != null)
            obj["source_role"] = diagnostic.SourceRole;

        if (diagnostic.SourceMetadata != null)
            obj["source_metadata"] = diagnostic.SourceMetadata;

        if (diagnostic.TargetRole != null)
            obj["target_role"] = diagnostic.TargetRole;

        if (diagnostic.TargetMetadata != null)
            obj["target_metadata"] = diagnostic.TargetMetadata;

        if (diagnostic.MatchedSelector != null)
            obj["matched_selector"] = diagnostic.MatchedSelector;
    }

    private static void ApplyContextAllowOnlyCiFields(ContextAllowOnlyDiagnostic diagnostic, Dictionary<string, object?> obj)
    {
        if (diagnostic.SourceRole != null)
            obj["source_role"] = diagnostic.SourceRole;

        if (diagnostic.SourceMetadata != null)
            obj["source_metadata"] = diagnostic.SourceMetadata;

        if (diagnostic.TargetRole != null)
            obj["target_role"] = diagnostic.TargetRole;

        if (diagnostic.TargetMetadata != null)
            obj["target_metadata"] = diagnostic.TargetMetadata;

        if (diagnostic.MatchedSelector != null)
            obj["matched_selector"] = diagnostic.MatchedSelector;
    }

    private static string FormatPortBoundaryContextForHumans(PortBoundaryDiagnostic diagnostic) =>
        $" (kind: port_boundary, evidence_kind: {diagnostic.EvidenceKind ?? "?"}, " +
        $"expected_seam: {diagnostic.ExpectedSeam ?? "?"}, source_role: {diagnostic.SourceRole ?? "?"}, " +
        $"source_metadata: {FormatMetadataForHumans(diagnostic.SourceMetadata)}, target_role: {diagnostic.TargetRole ?? "?"}, " +
        $"target_metadata: {FormatMetadataForHumans(diagnostic.TargetMetadata)}, remediation: {diagnostic.RemediationHint ?? "?"})";

    private static void ApplyPortBoundaryCiFields(PortBoundaryDiagnostic diagnostic, Dictionary<string, object?> obj)
    {
        obj["evidence_kind"] = diagnostic.EvidenceKind;
        obj["expected_seam"] = diagnostic.ExpectedSeam;
        obj["source_role"] = diagnostic.SourceRole;
        obj["source_metadata"] = diagnostic.SourceMetadata;
        obj["target_role"] = diagnostic.TargetRole;
        obj["target_metadata"] = diagnostic.TargetMetadata;
        obj["remediation_hint"] = diagnostic.RemediationHint;
    }
}
