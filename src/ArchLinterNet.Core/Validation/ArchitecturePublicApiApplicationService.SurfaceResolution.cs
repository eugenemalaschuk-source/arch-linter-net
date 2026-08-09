using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Validation;

// Contract lookup, build-state preflight, and surface capture — the part every public-api operation
// shares. Split from the operation bodies so neither file grows past the repository's file-size gate.
public sealed partial class ArchitecturePublicApiApplicationService
{
    private SurfaceResolution ResolveSurface(
        string policyPath,
        string contractId,
        string? conditionSetName,
        BuildPreparationMode preparationMode,
        bool noRestore,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArchitectureContractDocument document = runnerSetupService.LoadDocument(policyPath, null, null, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        ArchitecturePublicApiSurfaceContract? contract = document.Contracts.StrictPublicApiSurface
            .Concat(document.Contracts.AuditPublicApiSurface)
            .FirstOrDefault(candidate => string.Equals(candidate.Id, contractId, StringComparison.OrdinalIgnoreCase));

        if (contract == null)
        {
            IEnumerable<string> available = document.Contracts.StrictPublicApiSurface
                .Concat(document.Contracts.AuditPublicApiSurface)
                .Select(candidate => candidate.Id)
                .Where(id => id != null)
                .OrderBy(id => id, StringComparer.Ordinal)!;

            string availableText = available.Any() ? string.Join(", ", available) : "(none)";
            return SurfaceResolution.Failed(
                $"Unknown public API surface contract '{contractId}'. Available contract ids: {availableText}.");
        }

        ArchitectureRunnerSetup setup = runnerSetupService.BuildRunner(document, policyPath, conditionSetName, cancellationToken: cancellationToken);
        try
        {
            BuildStatePreflightResult preflight = RunBuildStatePreflight(
                setup.Runner, preparationMode, noRestore, cancellationToken);
            if (preflight.Blocked)
            {
                return SurfaceResolution.Failed(
                    "Build state preflight is blocked; the exported surface cannot be captured from artifacts " +
                    "that are missing, stale, or built for a different target framework.",
                    preflight.Diagnostics);
            }

            if (preparationMode == BuildPreparationMode.EnsureBuilt
                && setup.Runner.Session.Context.ProjectDiscovery is { DiscoveredProjects.Count: > 0 })
            {
                // The initial runner only identifies the graph to prepare. Recreate it after the
                // build so the scanner consumes post-build bytes, then prove those bytes against
                // the receipt in ordinary mode before continuing.
                ArchitectureRunnerSetup postBuildSetup = runnerSetupService.BuildRunner(
                    document, policyPath, conditionSetName, cancellationToken: cancellationToken);
                setup.Runner.Session.Context.Dispose();
                setup = postBuildSetup;
                preflight = RunBuildStatePreflight(
                    setup.Runner, BuildPreparationMode.Ordinary, noRestore, cancellationToken);

                if (preflight.Blocked)
                {
                    return SurfaceResolution.Failed(
                        "Build state preflight is blocked; the exported surface cannot be captured from artifacts " +
                        "that are missing, stale, or built for a different target framework.",
                        preflight.Diagnostics);
                }
            }

            IReadOnlyList<PublicApiSnapshotEntry> entries = setup.Runner.Session.CapturePublicApiSurface(
                contract, out IReadOnlyList<string> missingAssemblies);
            cancellationToken.ThrowIfCancellationRequested();

            if (missingAssemblies.Count > 0)
            {
                return SurfaceResolution.Failed(
                    $"Contract '{contractId}' targets assemblies that could not be resolved: " +
                    $"{string.Join(", ", missingAssemblies)}. Build the solution before capturing its public API.",
                    preflight.Diagnostics);
            }

            return new SurfaceResolution(contract, entries, preflight.Diagnostics, null);
        }
        finally
        {
            // Unlike validation, this seam owns its runner for the length of one operation. The
            // captured entries are plain strings, so releasing the isolated assembly load scope
            // here costs nothing and avoids holding target assemblies loaded after the CLI returns.
            setup.Runner.Session.Context.Dispose();
        }
    }

    // Mirrors ArchitectureValidationApplicationService.RunBuildStatePreflight: preflight only has
    // the fingerprint/receipt inputs it needs when project discovery produced a project graph, and
    // "resolution never ran" (neither resolved nor missing names) must not be read as "artifact
    // missing".
    private BuildStatePreflightResult RunBuildStatePreflight(
        IArchitectureContractRunner runner,
        BuildPreparationMode preparationMode,
        bool noRestore,
        CancellationToken cancellationToken)
    {
        Discovery.ProjectDiscoveryResult? discovery = runner.Session.Context.ProjectDiscovery;
        if (discovery == null || discovery.DiscoveredProjects.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        BuildStateResolvedAssemblies resolution = new(
            runner.Session.Context.TargetAssemblies,
            runner.Session.Context.MissingAssemblyNames);

        if (resolution.ResolvedAssemblies.Count == 0 && resolution.MissingAssemblyNames.Count == 0)
        {
            return new BuildStatePreflightResult(Array.Empty<BuildStatePreflightDiagnostic>());
        }

        return buildStatePreparationService.Prepare(new BuildStatePreflightRequest(
            runner.Session.Context.RepositoryRoot,
            discovery,
            resolution,
            preparationMode,
            noRestore,
            CancellationToken: cancellationToken));
    }

    private sealed record SurfaceResolution(
        ArchitecturePublicApiSurfaceContract? Contract,
        IReadOnlyList<PublicApiSnapshotEntry> Entries,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
        string? Error,
        PublicApiFailureKind FailureKind = PublicApiFailureKind.InvalidInput)
    {
        public static SurfaceResolution Failed(
            string error, IReadOnlyCollection<BuildStatePreflightDiagnostic>? diagnostics = null)
        {
            return new SurfaceResolution(
                null,
                Array.Empty<PublicApiSnapshotEntry>(),
                diagnostics ?? Array.Empty<BuildStatePreflightDiagnostic>(),
                error);
        }
    }
}
