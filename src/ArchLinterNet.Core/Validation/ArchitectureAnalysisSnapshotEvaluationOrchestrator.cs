using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Validation;

public sealed partial class ArchitectureAnalysisSnapshot
{
    // Keeps snapshot state ownership private while isolating per-mode execution and repository
    // inventory completion from the snapshot's lifecycle and runner-management responsibilities.
    private static class EvaluationOrchestrator
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

            return snapshot._evaluatedModes[mode];
        }

        private static ValidationOutcome EvaluateMode(
            ArchitectureAnalysisSnapshot snapshot,
            string mode,
            ValidationTiming? timing)
        {
            // A snapshot meant to serve any/all requested modes validates a --contract-id filter
            // against the union of strict and audit IDs at construction time (see
            // ArchitectureValidationApplicationService.ResolveSelectedContractIds) — that only rejects
            // an ID unknown to every mode. An ID valid in one mode but not this one would otherwise
            // silently match nothing when this mode's contracts execute, instead of failing the same
            // way an independent single-mode Validate call for this mode would. Re-validating here,
            // per mode, keeps combined execution semantically equivalent to separate runs.
            snapshot.EnsureRequestedContractIdsAreKnownForMode(mode);

            snapshot._cancellationToken.ThrowIfCancellationRequested();
            ValidationOutcome? cachedOutcome = snapshot._preflight.Blocked
                ? null
                : snapshot.TryEvaluateFromCache(mode, timing);
            WorkSnapshot? workBefore = cachedOutcome is null && !snapshot._preflight.Blocked
                ? snapshot.CaptureWorkSnapshot()
                : null;
            ValidationOutcome outcome = cachedOutcome
                ?? (snapshot._preflight.Blocked ? snapshot.BuildBlockedOutcome() : snapshot.EvaluateCore(mode, timing));
            if (snapshot._preparedPostBuildRunner is not null)
            {
                outcome = outcome with { PreparedPostBuildRunner = snapshot._preparedPostBuildRunner };
            }

            if (cachedOutcome is null
                && !outcome.PreflightBlocked
                && snapshot._cacheAuthorizations.Remove(
                    mode,
                    out AnalysisCachePopulation.PreparedAuthorization? authorization))
            {
                // This opaque plan was captured before contract execution. It is associated by object
                // identity rather than stored on ValidationOutcome itself, so transient cache state
                // cannot change that public record's equality contract.
                CacheArtifactEvidence artifacts = snapshot.GetCacheArtifactEvidence();
                AnalysisCachePopulation.AttachAuthorization(
                    outcome,
                    authorization,
                    artifacts.Paths,
                    artifacts.CapturedIdentities,
                    snapshot.CreateWorkProvenance(workBefore!.Value));
            }

            snapshot._evaluatedModes[mode] = outcome;
            snapshot._counters = snapshot._counters with { ModesEvaluated = snapshot._evaluatedModes.Count };
            return outcome;
        }

        private static void CompleteRepositoryPolicyInventory(
            ArchitectureAnalysisSnapshot snapshot,
            string requestedMode,
            ValidationTiming? timing)
        {
            IReadOnlyList<string> waiverModes = ArchitectureWaiverLifecycleEvaluator
                .GetModesWithSelectedManualWaivers(snapshot._document, snapshot._requestedContractIds);
            foreach (string waiverMode in waiverModes)
            {
                if (!snapshot._evaluatedModes.ContainsKey(waiverMode))
                {
                    EvaluateMode(snapshot, waiverMode, timing);
                }
            }

            ArchitectureWaiverLifecycleRecord[] repositoryWaivers = waiverModes
                .SelectMany(waiverMode => snapshot._evaluatedModes[waiverMode].Waivers)
                .ToArray();
            ArchitecturePolicyInventory inventory = ArchitecturePolicyInventoryProjector.Project(
                snapshot._document,
                requestedMode,
                repositoryWaivers,
                snapshot._requestedContractIds,
                snapshot._includeAsmdefContracts,
                snapshot._coverageConfig != "off");

            foreach (string evaluatedMode in snapshot._evaluatedModes.Keys.ToArray())
            {
                ValidationOutcome previous = snapshot._evaluatedModes[evaluatedMode];
                ValidationOutcome completed = previous with { PolicyInventory = inventory };
                AnalysisCachePopulation.TransferAuthorization(previous, completed);
                snapshot._evaluatedModes[evaluatedMode] = completed;
            }
        }
    }
}
