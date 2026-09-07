using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

// Coordinates mode memoization and repository-inventory completion for one snapshot. The
// snapshot supplies all mutable state operations; this coordinator has no runner, session, or
// lifecycle state of its own.
internal static class ArchitectureAnalysisSnapshotEvaluationOrchestrator
{
    internal static ValidationOutcome Evaluate(
        ArchitectureAnalysisSnapshot snapshot,
        string mode,
        ValidationTiming? timing)
    {
        ValidationOutcome outcome = EvaluateMode(snapshot, mode, timing);
        if (!outcome.PreflightBlocked)
        {
            CompleteRepositoryPolicyInventory(snapshot, mode, timing);
        }

        return snapshot.GetEvaluatedMode(mode);
    }

    private static ValidationOutcome EvaluateMode(
        ArchitectureAnalysisSnapshot snapshot,
        string mode,
        ValidationTiming? timing)
    {
        // A snapshot meant to serve any/all requested modes validates a --contract-id filter
        // against the union of strict and audit IDs at construction time. Re-validating here,
        // per mode, keeps combined execution semantically equivalent to separate runs.
        snapshot.EnsureRequestedContractIdsAreKnownForMode(mode);

        snapshot.CancellationToken.ThrowIfCancellationRequested();
        ValidationOutcome? cachedOutcome = snapshot.IsPreflightBlocked
            ? null
            : snapshot.TryEvaluateFromCache(mode, timing);
        ArchitectureAnalysisSnapshotWorkSnapshot? workBefore = cachedOutcome is null && !snapshot.IsPreflightBlocked
            ? snapshot.CaptureWorkSnapshot()
            : null;
        ValidationOutcome outcome = cachedOutcome
            ?? (snapshot.IsPreflightBlocked
                ? snapshot.BuildBlockedOutcome()
                : snapshot.EvaluateCoreForMode(mode, timing));
        if (snapshot.PreparedPostBuildRunner is not null)
        {
            outcome = outcome with { PreparedPostBuildRunner = snapshot.PreparedPostBuildRunner };
        }

        if (cachedOutcome is null
            && !outcome.PreflightBlocked
            && snapshot.TryTakeCacheAuthorization(
                mode,
                out AnalysisCachePopulation.PreparedAuthorization authorization))
        {
            // This opaque plan was captured before contract execution. It is associated by object
            // identity rather than stored on ValidationOutcome itself, so transient cache state
            // cannot change that public record's equality contract.
            (IReadOnlyList<string> paths,
                IReadOnlyList<AnalysisCacheCapturedFileIdentity> capturedIdentities) =
                snapshot.GetCacheArtifactEvidenceForAuthorization();
            AnalysisCachePopulation.AttachAuthorization(
                outcome,
                authorization,
                paths,
                capturedIdentities,
                snapshot.CreateWorkProvenance(workBefore!.Value));
        }

        snapshot.SetEvaluatedMode(mode, outcome);
        return outcome;
    }

    private static void CompleteRepositoryPolicyInventory(
        ArchitectureAnalysisSnapshot snapshot,
        string requestedMode,
        ValidationTiming? timing)
    {
        IReadOnlyList<string> waiverModes = ArchitectureWaiverLifecycleEvaluator
            .GetModesWithSelectedManualWaivers(snapshot.Document, snapshot.RequestedContractIds);
        foreach (string waiverMode in waiverModes)
        {
            if (!snapshot.HasEvaluatedMode(waiverMode))
            {
                EvaluateMode(snapshot, waiverMode, timing);
            }
        }

        ArchitectureWaiverLifecycleRecord[] repositoryWaivers = waiverModes
            .SelectMany(waiverMode => snapshot.GetEvaluatedMode(waiverMode).Waivers)
            .ToArray();
        ArchitecturePolicyInventory inventory = ArchitecturePolicyInventoryProjector.Project(
            snapshot.Document,
            requestedMode,
            repositoryWaivers,
            snapshot.RequestedContractIds,
            snapshot.IncludeAsmdefContracts,
            snapshot.CoverageConfig != "off");

        snapshot.ApplyPolicyInventory(inventory);
    }
}
