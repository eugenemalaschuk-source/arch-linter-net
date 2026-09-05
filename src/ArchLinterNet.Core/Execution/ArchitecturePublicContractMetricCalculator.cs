using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Resolves a public API metric's contract and assembly identities, then reuses the session's
// existing public-surface capture. No scanner, snapshot, or public contract state is created here.
internal static class ArchitecturePublicContractMetricCalculator
{
    internal static ArchitectureMetricRawEvidence Calculate(
        ArchitectureAnalysisSession session,
        ArchitectureMetricDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definition);

        string scope = definition.PublicApiSurface ?? string.Empty;
        ArchitecturePublicApiSurfaceContract[] candidates = session.Document.Contracts.StrictPublicApiSurface
            .Concat(session.Document.Contracts.AuditPublicApiSurface)
            .Where(candidate => string.Equals(candidate.Id, scope, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (candidates.Length != 1 || candidates[0].ApiSnapshotError is not null)
        {
            return Missing(scope, definition.Unit);
        }

        ArchitecturePublicApiSurfaceContract contract = candidates[0];
        if (!TryResolvePublicSurfaceAssemblyIdentities(session, contract, out IReadOnlyDictionary<string, string>? identities))
        {
            // Public-surface configuration names assemblies by simple name. A metric cannot turn
            // that into a canonical contributor when the target set has zero or multiple matches.
            return Missing(scope, definition.Unit);
        }

        IReadOnlyList<PublicApiSnapshotEntry> entries;
        IReadOnlyList<ArchitectureViolation> selectorSafety;
        IReadOnlyList<string> missing;
        bool isComplete;
        try
        {
            entries = session.CapturePublicApiSurface(contract, out missing, out selectorSafety, out isComplete);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Missing(scope, definition.Unit);
        }

        List<string> reasons = new();
        if (missing.Count > 0 || selectorSafety.Count > 0 || !isComplete)
        {
            reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
        }

        List<string> contributors = new();
        foreach (PublicApiSnapshotEntry entry in entries)
        {
            if (!identities.TryGetValue(entry.AssemblyName, out string? identity))
            {
                reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
                continue;
            }

            contributors.Add($"{identity}|{entry.Signature}");
        }

        return new ArchitectureMetricRawEvidence(scope, null, reasons, contributors);
    }

    private static ArchitectureMetricRawEvidence Missing(string scope, string? unit) =>
        new(scope, unit, [ArchitectureApplicabilityReasonCodes.MissingRequiredInput], Array.Empty<string>());

    private static bool TryResolvePublicSurfaceAssemblyIdentities(
        ArchitectureAnalysisSession session,
        ArchitecturePublicApiSurfaceContract contract,
        out IReadOnlyDictionary<string, string> identities)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string assemblyName in contract.Assemblies.Distinct(StringComparer.Ordinal))
        {
            Assembly[] candidates = session.Context.TargetAssemblies
                .Where(candidate => string.Equals(candidate.GetName().Name, assemblyName, StringComparison.Ordinal))
                .ToArray();
            if (candidates.Length != 1)
            {
                identities = new Dictionary<string, string>(StringComparer.Ordinal);
                return false;
            }

            resolved.Add(assemblyName, ArchitectureTopologyMetricObserver.ResolveCanonicalAssemblyIdentity(candidates[0]));
        }

        identities = resolved;
        return true;
    }
}
