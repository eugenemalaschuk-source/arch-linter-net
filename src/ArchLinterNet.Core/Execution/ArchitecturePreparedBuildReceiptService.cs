using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

// The producer owns the one authoritative preparation. Receipt publication is performed inside
// that build-capable path, immediately after its successful graph build, so an existing artifact
// can never be promoted by a separate publisher without the build that authorizes it.
internal sealed class ArchitecturePreparedBuildReceiptService(
    IArchitectureRunnerSetupService runnerSetupService)
{
    internal BuildStatePreflightResult Publish(BuildStatePreparedCandidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.CancellationToken.ThrowIfCancellationRequested();
        ArchitectureContractDocument document = runnerSetupService.LoadDocument(
            request.PolicyPath, null, null, request.CancellationToken);
        ArchitectureRunnerPreparation preparation = runnerSetupService.PrepareRunner(
            document,
            request.PolicyPath,
            request.ConditionSetName,
            mode: null,
            cancellationToken: request.CancellationToken);
        BuildStateResolvedAssemblies? resolution = BuildStatePreflightRunner.CreatePreparationResolution(
            preparation, BuildPreparationMode.EnsureBuilt);

        if (resolution is null
            || (resolution.ResolvedAssemblyPaths.Count == 0 && resolution.MissingAssemblyNames.Count == 0))
        {
            throw new InvalidOperationException(
                "The prepared candidate does not contain a project graph selected for architecture analysis.");
        }

        string requestedConfiguration = request.RequestedConfiguration
            ?? (string.IsNullOrWhiteSpace(document.Analysis.Configuration) ? "Debug" : document.Analysis.Configuration);
        string? requestedTargetFramework = request.RequestedTargetFramework
            ?? (string.IsNullOrWhiteSpace(document.Analysis.TargetFramework) ? null : document.Analysis.TargetFramework);

        return BuildStateRuntimeBuildPreparation.EnsureBuilt(new BuildStatePreflightRequest(
            preparation.RepositoryRoot,
            preparation.ProjectDiscovery,
            resolution,
            BuildPreparationMode.EnsureBuilt,
            NoRestore: request.NoRestore,
            RequestedConfiguration: requestedConfiguration,
            RequestedTargetFramework: requestedTargetFramework,
            RequestedPlatform: request.RequestedPlatform,
            RequestedRuntimeIdentifier: request.RequestedRuntimeIdentifier,
            CancellationToken: request.CancellationToken));
    }
}
