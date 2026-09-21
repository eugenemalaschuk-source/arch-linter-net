using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

// The producer calls this after its one explicit solution build. It only discovers output paths,
// fingerprints the files already on disk, writes receipts, and verifies them in Ordinary mode;
// it never enters BuildStatePreparationService.Prepare(EnsureBuilt) or invokes MSBuild.
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

        return BuildStateRuntimeBuildPreparation.PublishPreparedReceipts(new BuildStatePreflightRequest(
            preparation.RepositoryRoot,
            preparation.ProjectDiscovery,
            resolution,
            BuildPreparationMode.Ordinary,
            RequestedConfiguration: requestedConfiguration,
            RequestedTargetFramework: requestedTargetFramework,
            RequestedPlatform: request.RequestedPlatform,
            RequestedRuntimeIdentifier: request.RequestedRuntimeIdentifier,
            CancellationToken: request.CancellationToken));
    }
}
