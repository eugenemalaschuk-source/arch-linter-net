using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Abstractions;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation.Abstractions;

namespace ArchLinterNet.Core.Validation;

// Application seam for the reviewed public API snapshot workflow, mirroring
// ArchitectureBaselineApplicationService: Core resolves the policy, verifies build state, captures
// the surface, and returns content plus a structured delta; the host writes files.
public sealed class ArchitecturePublicApiApplicationService(
    IArchitectureRunnerSetupService runnerSetupService,
    IBuildStatePreparationService buildStatePreparationService,
    IPublicApiSnapshotStore snapshotStore)
    : IArchitecturePublicApiApplicationService
{
    public PublicApiCaptureOutcome Capture(PublicApiCaptureRequest request)
    {
        SurfaceResolution resolution = ResolveSurface(request.PolicyPath, request.ContractId, request.ConditionSetName);
        if (resolution.Error != null)
        {
            return new PublicApiCaptureOutcome(false, null, 0, resolution.PreflightDiagnostics, resolution.Error);
        }

        string snapshot = Serialize(request.ContractId, resolution.Entries);
        return new PublicApiCaptureOutcome(
            true, snapshot, DistinctCount(resolution.Entries), resolution.PreflightDiagnostics);
    }

    public PublicApiDiffOutcome Diff(PublicApiDiffRequest request)
    {
        SurfaceResolution resolution = ResolveSurface(request.PolicyPath, request.ContractId, request.ConditionSetName);
        if (resolution.Error != null)
        {
            return new PublicApiDiffOutcome(
                false, false, PublicApiDelta.Empty, resolution.PreflightDiagnostics, resolution.Error);
        }

        if (!TryReadSnapshot(request.PolicyPath, request.SnapshotPath, out IReadOnlyList<PublicApiSnapshotEntry> declared, out string? error))
        {
            return new PublicApiDiffOutcome(
                false, false, PublicApiDelta.Empty, resolution.PreflightDiagnostics, error);
        }

        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(declared, resolution.Entries);
        return new PublicApiDiffOutcome(true, !delta.HasChanges, delta, resolution.PreflightDiagnostics);
    }

    public PublicApiUpdateOutcome Update(PublicApiUpdateRequest request)
    {
        SurfaceResolution resolution = ResolveSurface(request.PolicyPath, request.ContractId, request.ConditionSetName);
        if (resolution.Error != null)
        {
            return new PublicApiUpdateOutcome(
                false, null, PublicApiDelta.Empty, request.DryRun, resolution.PreflightDiagnostics, resolution.Error);
        }

        // Updating an inline `declared_api` list in place would require rewriting the policy YAML,
        // which cannot preserve surrounding comments through a round-trip. Refusing is the honest
        // branch: the reviewed content is never silently reformatted or stripped.
        if (string.IsNullOrWhiteSpace(resolution.Contract!.ApiSnapshot))
        {
            return new PublicApiUpdateOutcome(
                false, null, PublicApiDelta.Empty, request.DryRun, resolution.PreflightDiagnostics,
                $"Contract '{request.ContractId}' declares its surface inline via 'declared_api' and has no " +
                "'api_snapshot'. Updating an inline declaration in place is refused because a YAML round-trip " +
                "cannot preserve the surrounding policy comments. Run 'arch-linter-net public-api migrate' to " +
                "move this contract onto a reviewed snapshot file first.");
        }

        if (!TryReadSnapshot(request.PolicyPath, request.SnapshotPath, out IReadOnlyList<PublicApiSnapshotEntry> declared, out string? error))
        {
            return new PublicApiUpdateOutcome(
                false, null, PublicApiDelta.Empty, request.DryRun, resolution.PreflightDiagnostics, error);
        }

        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(declared, resolution.Entries);
        string snapshot = Serialize(request.ContractId, resolution.Entries);
        return new PublicApiUpdateOutcome(true, snapshot, delta, request.DryRun, resolution.PreflightDiagnostics);
    }

    public PublicApiMigrateOutcome Migrate(PublicApiMigrateRequest request)
    {
        SurfaceResolution resolution = ResolveSurface(request.PolicyPath, request.ContractId, request.ConditionSetName);
        if (resolution.Error != null)
        {
            return new PublicApiMigrateOutcome(
                false, null, Array.Empty<string>(), Array.Empty<string>(), resolution.PreflightDiagnostics, resolution.Error);
        }

        // Path safety is enforced for the destination too: migrate writes a new reviewed artifact,
        // so it must land inside the same boundary a policy-declared snapshot would.
        try
        {
            snapshotStore.ResolvePath(request.PolicyPath, request.OutputPath);
        }
        catch (InvalidOperationException exception)
        {
            return new PublicApiMigrateOutcome(
                false, null, Array.Empty<string>(), Array.Empty<string>(), resolution.PreflightDiagnostics, exception.Message);
        }

        HashSet<string> actual = new(resolution.Entries.Select(entry => entry.Signature), StringComparer.Ordinal);
        HashSet<string> inline = new(resolution.Contract!.DeclaredApi, StringComparer.Ordinal);

        IReadOnlyList<string> stale = inline.Where(signature => !actual.Contains(signature))
            .OrderBy(signature => signature, StringComparer.Ordinal).ToArray();
        IReadOnlyList<string> undeclared = actual.Where(signature => !inline.Contains(signature))
            .OrderBy(signature => signature, StringComparer.Ordinal).ToArray();

        bool hasDrift = stale.Count > 0 || undeclared.Count > 0;
        if (hasDrift && !request.AcceptDrift)
        {
            return new PublicApiMigrateOutcome(
                false, null, stale, undeclared, resolution.PreflightDiagnostics,
                $"Contract '{request.ContractId}' has {stale.Count} stale inline declaration(s) and " +
                $"{undeclared.Count} undeclared exported member(s). Migrating now would silently accept that " +
                "drift as reviewed. Fix the surface, or re-run with drift acceptance to record the live " +
                "surface deliberately.");
        }

        return new PublicApiMigrateOutcome(
            true, Serialize(request.ContractId, resolution.Entries), stale, undeclared, resolution.PreflightDiagnostics);
    }

    private bool TryReadSnapshot(
        string policyPath,
        string snapshotPath,
        out IReadOnlyList<PublicApiSnapshotEntry> entries,
        out string? error)
    {
        entries = Array.Empty<PublicApiSnapshotEntry>();
        try
        {
            string resolved = snapshotStore.ResolvePath(policyPath, snapshotPath);
            if (!snapshotStore.Exists(resolved))
            {
                error = $"Public API snapshot not found: {snapshotPath}. " +
                    "Run 'arch-linter-net public-api capture' to create it.";
                return false;
            }

            entries = snapshotStore.Read(resolved, snapshotPath).Entries;
            error = null;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string Serialize(string contractId, IReadOnlyList<PublicApiSnapshotEntry> entries)
    {
        return PublicApiSnapshotFormat.Serialize(
            new PublicApiSnapshotDocument(PublicApiSnapshotFormat.CurrentVersion, contractId, entries));
    }

    private static int DistinctCount(IReadOnlyList<PublicApiSnapshotEntry> entries)
    {
        return entries.Select(entry => (entry.AssemblyName, entry.Signature)).Distinct().Count();
    }

    private SurfaceResolution ResolveSurface(string policyPath, string contractId, string? conditionSetName)
    {
        ArchitectureContractDocument document = runnerSetupService.LoadDocument(policyPath);

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

        ArchitectureRunnerSetup setup = runnerSetupService.BuildRunner(document, policyPath, conditionSetName);
        try
        {
            BuildStatePreflightResult preflight = RunBuildStatePreflight(setup.Runner);
            if (preflight.Blocked)
            {
                return SurfaceResolution.Failed(
                    "Build state preflight is blocked; the exported surface cannot be captured from artifacts " +
                    "that are missing, stale, or built for a different target framework.",
                    preflight.Diagnostics);
            }

            IReadOnlyList<PublicApiSnapshotEntry> entries = setup.Runner.Session.CapturePublicApiSurface(
                contract, out IReadOnlyList<string> missingAssemblies);

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
    private BuildStatePreflightResult RunBuildStatePreflight(IArchitectureContractRunner runner)
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
            BuildPreparationMode.Ordinary));
    }

    private sealed record SurfaceResolution(
        ArchitecturePublicApiSurfaceContract? Contract,
        IReadOnlyList<PublicApiSnapshotEntry> Entries,
        IReadOnlyCollection<BuildStatePreflightDiagnostic> PreflightDiagnostics,
        string? Error)
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
