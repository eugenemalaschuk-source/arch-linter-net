using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Reporting;

namespace ArchLinterNet.Core.Tests;

public sealed partial class ArchitectureAnalysisSnapshotTests
{
    private sealed class EnsureBuiltMetadataRunnerSetupService : IArchitectureRunnerSetupService
    {
        public int BuildRunnerCallCount { get; private set; }

        public int LoadDocumentCallCount { get; private set; }

        public int PrepareRunnerCallCount { get; private set; }

        public ArchitectureContractDocument DocumentToReturn { get; set; } = new() { Version = 1, Name = "Fake" };

        public IArchitectureContractRunner RunnerToReturn { get; set; } = null!;

        public ArchitectureContractDocument LoadDocument(
            string policyPath, string? baselinePath = null, ValidationTiming? timing = null)
        {
            LoadDocumentCallCount++;
            return DocumentToReturn;
        }

        public ArchitectureRunnerSetup BuildRunner(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null)
        {
            BuildRunnerCallCount++;
            return new ArchitectureRunnerSetup("/fake/repository/root", RunnerToReturn);
        }

        public ArchitectureRunnerSetup BuildRunnerForPostBuild(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null)
        {
            return BuildRunner(document, policyPath, conditionSetName, preprocessorSymbols, selectedContractIds,
                enableUnmatchedIgnoreTracking, timing, mode, cancellationToken, maxParallelism);
        }

        public ArchitectureRunnerPreparation PrepareRunner(
            ArchitectureContractDocument document,
            string policyPath,
            string? conditionSetName = null,
            IReadOnlyList<string>? preprocessorSymbols = null,
            HashSet<string>? selectedContractIds = null,
            string? mode = null,
            CancellationToken cancellationToken = default)
        {
            PrepareRunnerCallCount++;
            ArchitectureAnalysisContext context = RunnerToReturn.Session.Context;
            return new ArchitectureRunnerPreparation(
                context.RepositoryRoot,
                preprocessorSymbols,
                context.ProjectDiscovery ?? new ProjectDiscoveryResult(
                    Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                    Array.Empty<ArchitectureProjectDiscoveryDiagnostic>()),
                ResolveAssemblyOutputs: true,
                context.SelectedAssemblyArtifactPaths,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                context.MissingAssemblyNames.ToArray(),
                IsMetadataReferenceClosureComplete: false);
        }

        public ArchitectureRunnerSetup MaterializePreparedRunner(
            ArchitectureContractDocument document,
            ArchitectureRunnerPreparation preparation,
            HashSet<string>? selectedContractIds = null,
            bool enableUnmatchedIgnoreTracking = true,
            ValidationTiming? timing = null,
            string? mode = null,
            CancellationToken cancellationToken = default,
            int? maxParallelism = null)
        {
            return BuildRunner(document, "prepared-by-fake", selectedContractIds: selectedContractIds,
                enableUnmatchedIgnoreTracking: enableUnmatchedIgnoreTracking, timing: timing, mode: mode,
                cancellationToken: cancellationToken, maxParallelism: maxParallelism);
        }
    }
}
