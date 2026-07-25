using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class PublicApiSurfaceChecker
{
    private const string AddedDelta = "added";
    private const string RemovedDelta = "removed";
    private const string ChangedDelta = "changed";

    public static List<ArchitectureViolation> Check(
        ArchitecturePublicApiSurfaceContract contract,
        IReadOnlyDictionary<string, Assembly> resolvedAssemblies,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        HashSet<string> declaredApi = new(DeclaredSignatures(contract), StringComparer.Ordinal);
        HashSet<string> allowedPublicConstants = new(contract.AllowedPublicConstants, StringComparer.Ordinal);

        Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly =
            ScanContractAssemblies(contract, resolvedAssemblies);

        // Exact mode replaces "is this signature declared?" with a correlated delta, so a re-signed
        // member reports once as a change instead of as an unrelated addition plus removal. It only
        // runs when every declared assembly actually resolved: against a partially resolved
        // contract, every entry of the missing assembly would masquerade as a removal.
        bool exact = string.Equals(contract.ApiComparison, PublicApiComparisonModes.Exact, StringComparison.Ordinal)
            && scannedByAssembly.Count == contract.Assemblies.Distinct(StringComparer.Ordinal).Count();

        PublicApiDelta delta = exact
            ? PublicApiSnapshotDiffer.Diff(DeclaredEntries(contract), ActualEntries(scannedByAssembly))
            : PublicApiDelta.Empty;
        HashSet<string> addedSignatures = new(delta.Added.Select(entry => entry.Signature), StringComparer.Ordinal);

        foreach (string assemblyName in contract.Assemblies)
        {
            if (!scannedByAssembly.TryGetValue(assemblyName, out List<ArchitectureExportedApiEntry>? scanned))
            {
                continue;
            }

            List<(ArchitectureExportedApiEntry Entry, bool ForbiddenConstant, bool Undeclared)> violatingEntries = new();

            foreach (ArchitectureExportedApiEntry entry in scanned)
            {
                bool undeclared = exact
                    ? addedSignatures.Contains(entry.Signature)
                    : !declaredApi.Contains(entry.Signature);
                bool forbiddenConstant = contract.ForbidPublicConstantsUnlessDeclared
                    && entry.IsConst
                    && entry.ConstQualifiedName != null
                    && !allowedPublicConstants.Contains(entry.ConstQualifiedName);

                if (!undeclared && !forbiddenConstant)
                {
                    continue;
                }

                violatingEntries.Add((entry, forbiddenConstant, undeclared));
            }

            foreach (var (entry, forbiddenConstant, undeclared) in violatingEntries
                         .OrderBy(v => v.Entry.DeclaringTypeName, StringComparer.Ordinal)
                         .ThenBy(v => v.Entry.Signature, StringComparer.Ordinal))
            {
                if (executionContext.IsIgnored(entry.DeclaringTypeName, entry.Signature))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    entry.DeclaringTypeName,
                    "public API surface",
                    new[] { entry.Signature })
                {
                    Payload = new PublicApiSurfacePayload(
                        UndeclaredApiSignature: entry.Signature,
                        ForbiddenPublicConstant: forbiddenConstant ? true : null,
                        ApiAssemblyName: entry.AssemblyName,
                        ApiVisibility: entry.Visibility,
                        ApiDeltaKind: undeclared ? AddedDelta : null)
                });
            }
        }

        AddDeltaViolations(contract, delta.Removed, RemovedDelta, executionContext, violations);
        AddDeltaViolations(contract, delta.Changed, ChangedDelta, executionContext, violations);

        return violations;
    }

    private static void AddDeltaViolations(
        ArchitecturePublicApiSurfaceContract contract,
        IReadOnlyList<PublicApiDeltaEntry> entries,
        string deltaKind,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        foreach (PublicApiDeltaEntry entry in entries)
        {
            // A removed member has no live reflection entry, so its declaring type and the
            // signature reviewers recognize both come from the reviewed string itself.
            string declaringTypeName = PublicApiSignatureIdentity.DeclaringTypeName(entry.Signature);
            if (executionContext.IsIgnored(declaringTypeName, entry.Signature))
            {
                continue;
            }

            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                declaringTypeName,
                "public API surface",
                new[] { entry.Signature })
            {
                Payload = new PublicApiSurfacePayload(
                    UndeclaredApiSignature: entry.Signature,
                    ApiAssemblyName: entry.AssemblyName,
                    ApiDeltaKind: deltaKind,
                    PreviousApiSignature: entry.PreviousSignature)
            });
        }
    }

    private static Dictionary<string, List<ArchitectureExportedApiEntry>> ScanContractAssemblies(
        ArchitecturePublicApiSurfaceContract contract,
        IReadOnlyDictionary<string, Assembly> resolvedAssemblies)
    {
        Dictionary<string, List<ArchitectureExportedApiEntry>> scanned = new(StringComparer.Ordinal);

        foreach (string assemblyName in contract.Assemblies)
        {
            if (scanned.ContainsKey(assemblyName)
                || !resolvedAssemblies.TryGetValue(assemblyName, out Assembly? targetAssembly))
            {
                continue;
            }

            scanned[assemblyName] = ArchitecturePublicApiSurfaceScanner.GetExportedSurface(targetAssembly).ToList();
        }

        return scanned;
    }

    private static IEnumerable<string> DeclaredSignatures(ArchitecturePublicApiSurfaceContract contract)
    {
        return contract.DeclaredApi.Concat(contract.ResolvedSnapshotEntries.Select(entry => entry.Signature));
    }

    // Inline `declared_api` entries carry no assembly attribution, so they resolve to the empty
    // assembly name. Correlation is by signature identity, which never depends on the assembly, so
    // this only affects how a removal is attributed in the reported delta.
    private static IReadOnlyList<PublicApiSnapshotEntry> DeclaredEntries(ArchitecturePublicApiSurfaceContract contract)
    {
        return contract.DeclaredApi
            .Select(signature => new PublicApiSnapshotEntry(string.Empty, signature))
            .Concat(contract.ResolvedSnapshotEntries)
            .ToList();
    }

    private static IReadOnlyList<PublicApiSnapshotEntry> ActualEntries(
        Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly)
    {
        return scannedByAssembly.Values
            .SelectMany(entries => entries)
            .Select(entry => new PublicApiSnapshotEntry(entry.AssemblyName, entry.Signature))
            .ToList();
    }
}
