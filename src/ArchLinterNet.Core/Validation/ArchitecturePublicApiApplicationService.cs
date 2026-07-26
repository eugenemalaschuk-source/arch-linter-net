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
//
// Every artifact path is resolved here, against the policy boundary, and returned on the outcome.
// The host must use that resolved path: resolving the authored string again in the host would
// silently target a different file whenever the process working directory is not the repository
// root (for example with an absolute --policy).
public sealed partial class ArchitecturePublicApiApplicationService(
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
            return new PublicApiCaptureOutcome(
                false, null, 0, null, resolution.PreflightDiagnostics, resolution.Error, resolution.FailureKind);
        }

        if (!TryResolveDestination(request.PolicyPath, request.OutputPath, out string? destination, out string? pathError))
        {
            return new PublicApiCaptureOutcome(
                false, null, 0, null, resolution.PreflightDiagnostics, pathError, PublicApiFailureKind.InvalidInput);
        }

        string snapshot = Serialize(request.ContractId, resolution.Entries);
        return new PublicApiCaptureOutcome(
            true, snapshot, DistinctCount(resolution.Entries), destination, resolution.PreflightDiagnostics);
    }

    public PublicApiDiffOutcome Diff(PublicApiDiffRequest request)
    {
        SurfaceResolution resolution = ResolveSurface(request.PolicyPath, request.ContractId, request.ConditionSetName);
        if (resolution.Error != null)
        {
            return new PublicApiDiffOutcome(
                false, false, PublicApiDelta.Empty, null, resolution.PreflightDiagnostics,
                resolution.Error, resolution.FailureKind);
        }

        SnapshotRead read = ReadSnapshot(request.PolicyPath, request.SnapshotPath, resolution.Contract!);
        if (read.Error != null)
        {
            return new PublicApiDiffOutcome(
                false, false, PublicApiDelta.Empty, read.ResolvedPath, resolution.PreflightDiagnostics,
                read.Error, PublicApiFailureKind.InvalidInput);
        }

        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(read.Entries, resolution.Entries);
        return new PublicApiDiffOutcome(
            true, !delta.HasChanges, delta, read.ResolvedPath, resolution.PreflightDiagnostics);
    }

    public PublicApiUpdateOutcome Update(PublicApiUpdateRequest request)
    {
        SurfaceResolution resolution = ResolveSurface(request.PolicyPath, request.ContractId, request.ConditionSetName);
        if (resolution.Error != null)
        {
            return new PublicApiUpdateOutcome(
                false, null, PublicApiDelta.Empty, request.DryRun, null, resolution.PreflightDiagnostics,
                resolution.Error, resolution.FailureKind);
        }

        ArchitecturePublicApiSurfaceContract contract = resolution.Contract!;

        // Updating an inline `declared_api` list in place would require rewriting the policy YAML,
        // which cannot preserve surrounding comments through a round-trip. Refusing is the honest
        // branch: the reviewed content is never silently reformatted or stripped.
        if (string.IsNullOrWhiteSpace(contract.ApiSnapshot))
        {
            return new PublicApiUpdateOutcome(
                false, null, PublicApiDelta.Empty, request.DryRun, null, resolution.PreflightDiagnostics,
                $"Contract '{request.ContractId}' declares its surface inline via 'declared_api' and has no " +
                "'api_snapshot'. Updating an inline declaration in place is refused because a YAML round-trip " +
                "cannot preserve the surrounding policy comments. Run 'arch-linter-net public-api migrate' to " +
                "move this contract onto a reviewed snapshot file first.",
                PublicApiFailureKind.InvalidInput);
        }

        if (!TryResolveDestination(request.PolicyPath, request.SnapshotPath, out string? destination, out string? pathError))
        {
            return new PublicApiUpdateOutcome(
                false, null, PublicApiDelta.Empty, request.DryRun, null, resolution.PreflightDiagnostics,
                pathError, PublicApiFailureKind.InvalidInput);
        }

        // Writing anywhere other than the contract's own declared snapshot would leave the policy
        // pointing at a stale file while reporting success against a different one.
        if (contract.ResolvedSnapshotPath != null && !PathsMatch(destination!, contract.ResolvedSnapshotPath))
        {
            return new PublicApiUpdateOutcome(
                false, null, PublicApiDelta.Empty, request.DryRun, destination, resolution.PreflightDiagnostics,
                $"--snapshot '{request.SnapshotPath}' does not resolve to the snapshot declared by contract " +
                $"'{request.ContractId}' ('{contract.ApiSnapshot}'). Update always rewrites the contract's own " +
                "reviewed snapshot.",
                PublicApiFailureKind.InvalidInput);
        }

        // On a first update the contract snapshot may legitimately be absent, which must not block
        // the write. An unreadable or foreign one must never be silently replaced.
        if (contract.ApiSnapshotError != null && !IsMissingSnapshot(contract))
        {
            return new PublicApiUpdateOutcome(
                false, null, PublicApiDelta.Empty, request.DryRun, destination, resolution.PreflightDiagnostics,
                $"Contract '{request.ContractId}' {contract.ApiSnapshotError}",
                PublicApiFailureKind.InvalidInput);
        }

        PublicApiDelta delta = PublicApiSnapshotDiffer.Diff(contract.ResolvedSnapshotEntries, resolution.Entries);
        string snapshot = Serialize(request.ContractId, resolution.Entries);
        return new PublicApiUpdateOutcome(
            true, snapshot, delta, request.DryRun, destination, resolution.PreflightDiagnostics);
    }

    public PublicApiMigrateOutcome Migrate(PublicApiMigrateRequest request)
    {
        SurfaceResolution resolution = ResolveSurface(request.PolicyPath, request.ContractId, request.ConditionSetName);
        if (resolution.Error != null)
        {
            return new PublicApiMigrateOutcome(
                false, null, Array.Empty<string>(), Array.Empty<string>(), null,
                resolution.PreflightDiagnostics, resolution.Error, resolution.FailureKind);
        }

        if (!TryResolveDestination(request.PolicyPath, request.OutputPath, out string? destination, out string? pathError))
        {
            return new PublicApiMigrateOutcome(
                false, null, Array.Empty<string>(), Array.Empty<string>(), null,
                resolution.PreflightDiagnostics, pathError, PublicApiFailureKind.InvalidInput);
        }

        HashSet<string> actualExact = new(resolution.Entries.Select(entry => entry.Signature), StringComparer.Ordinal);
        HashSet<string> actualBase = new(
            resolution.Entries.Select(entry => Scanning.ArchitecturePublicApiSignatureDetails.StripDetails(entry.Signature)),
            StringComparer.Ordinal);
        HashSet<string> inline = new(resolution.Contract!.DeclaredApi, StringComparer.Ordinal);

        // The inline list is written in the legacy identity grammar, so it is compared against the
        // stripped form of the captured exact signatures — otherwise every entry would look stale
        // purely because the snapshot grammar carries more detail.
        string[] stale = inline.Where(signature => !actualBase.Contains(signature))
            .OrderBy(signature => signature, StringComparer.Ordinal).ToArray();
        string[] undeclared = actualExact
            .Where(signature => !inline.Contains(Scanning.ArchitecturePublicApiSignatureDetails.StripDetails(signature)))
            .OrderBy(signature => signature, StringComparer.Ordinal).ToArray();

        bool hasDrift = stale.Length > 0 || undeclared.Length > 0;
        if (hasDrift && !request.AcceptDrift)
        {
            return new PublicApiMigrateOutcome(
                false, null, stale, undeclared, destination, resolution.PreflightDiagnostics,
                $"Contract '{request.ContractId}' has {stale.Length} stale inline declaration(s) and " +
                $"{undeclared.Length} undeclared exported member(s). Migrating now would silently accept that " +
                "drift as reviewed. Fix the surface, or re-run with drift acceptance to record the live " +
                "surface deliberately.",
                PublicApiFailureKind.Drift);
        }

        return new PublicApiMigrateOutcome(
            true, Serialize(request.ContractId, resolution.Entries), stale, undeclared, destination,
            resolution.PreflightDiagnostics);
    }

    // Typed, not a substring match against the human-readable message: an existing, corrupt
    // snapshot could legitimately be named in a way that contains "does not exist", which would
    // misclassify a ParseError/OwnershipError as the recoverable Missing state and let update
    // silently replace a file it should have refused to touch.
    private static bool IsMissingSnapshot(ArchitecturePublicApiSurfaceContract contract)
    {
        return contract.ApiSnapshotErrorKind == PublicApiSnapshotErrorKind.Missing;
    }

    // File-path identity cannot be inferred from the OS alone: the default HFS+/APFS format is
    // case-insensitive, but macOS explicitly supports formatting a volume as case-sensitive APFS,
    // and ext4 (the common Linux filesystem) is always case-sensitive. Assuming every macOS host is
    // case-insensitive would treat 'Surface.txt' and 'surface.txt' as the same file on a
    // case-sensitive install and silently update the wrong one.
    //
    // `second` is the reference path the caller already trusts (the policy file, or the contract's
    // declared snapshot). When it exists, the real filesystem is asked whether `first`'s casing also
    // resolves to it: if both report existing, this filesystem folds the two spellings onto one
    // file. When `second` does not exist yet (for example a snapshot before its first capture),
    // there is nothing to probe, so the safe default is to require an exact match.
    internal bool PathsMatch(string first, string second)
    {
        if (string.Equals(first, second, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return snapshotStore.Exists(second) && snapshotStore.Exists(first);
    }

    // The policy (and any imported policy source) must never be a snapshot destination: a --force
    // write would otherwise replace the very file that defines the contract.
    private bool TryResolveDestination(
        string policyPath, string authoredPath, out string? destination, out string? error)
    {
        destination = null;

        try
        {
            destination = snapshotStore.ResolvePath(policyPath, authoredPath);
        }
        catch (InvalidOperationException exception)
        {
            error = exception.Message;
            return false;
        }

        if (PathsMatch(destination, Path.GetFullPath(policyPath)))
        {
            error = $"Refusing to use the policy file '{policyPath}' as a public API snapshot path.";
            destination = null;
            return false;
        }

        error = null;
        return true;
    }

    private SnapshotRead ReadSnapshot(
        string policyPath, string snapshotPath, ArchitecturePublicApiSurfaceContract contract)
    {
        string? resolved = null;
        try
        {
            resolved = snapshotStore.ResolvePath(policyPath, snapshotPath);
            if (!snapshotStore.Exists(resolved))
            {
                return SnapshotRead.Failed(
                    resolved,
                    $"Public API snapshot not found: {snapshotPath}. " +
                    "Run 'arch-linter-net public-api capture' to create it.");
            }

            PublicApiSnapshotDocument document = snapshotStore.Read(resolved, snapshotPath);
            string? ownershipError = PublicApiSnapshotResolver.ValidateOwnership(document, contract, snapshotPath);
            return ownershipError == null
                ? new SnapshotRead(document.Entries, resolved, null)
                : SnapshotRead.Failed(resolved, $"Contract '{contract.Id ?? contract.Name}' {ownershipError}");
        }
        catch (InvalidOperationException exception)
        {
            return SnapshotRead.Failed(resolved, exception.Message);
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

    private sealed record SnapshotRead(
        IReadOnlyList<PublicApiSnapshotEntry> Entries,
        string? ResolvedPath,
        string? Error)
    {
        public static SnapshotRead Failed(string? resolvedPath, string error)
        {
            return new SnapshotRead(Array.Empty<PublicApiSnapshotEntry>(), resolvedPath, error);
        }
    }
}
