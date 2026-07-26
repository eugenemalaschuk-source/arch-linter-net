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

        // A missing, unparsable, or foreign snapshot is deliberately not a policy-load failure (see
        // PublicApiSnapshotResolver), so validation is where it has to become loud. Reporting the
        // whole surface as undeclared instead would bury the actual cause in noise.
        if (contract.ApiSnapshotError != null)
        {
            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                contract.Name,
                "public API snapshot",
                new[] { contract.ApiSnapshotError })
            {
                Payload = new PublicApiSurfacePayload(
                    UndeclaredApiSignature: contract.ApiSnapshotError,
                    ApiDeltaKind: "snapshot-unusable"),
            });
            return violations;
        }

        // The reviewed snapshot records the exact grammar (base signature plus the detail suffix
        // carrying constant values, accessor shape, static/ref/out/in, sealed/abstract, generic
        // constraints). A contract with only an inline `declared_api` list keeps comparing against
        // the legacy identity grammar, which is what keeps existing policies working unchanged.
        bool exactGrammar = !string.IsNullOrWhiteSpace(contract.ApiSnapshot);

        HashSet<string> inlineDeclared = new(contract.DeclaredApi, StringComparer.Ordinal);
        HashSet<(string Assembly, string Signature)> snapshotDeclared = new(
            contract.ResolvedSnapshotEntries.Select(entry => (entry.AssemblyName, entry.Signature)));
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
            ? PublicApiSnapshotDiffer.Diff(
                DeclaredEntries(contract, scannedByAssembly, exactGrammar),
                ActualEntries(scannedByAssembly, exactGrammar))
            : PublicApiDelta.Empty;

        HashSet<(string Assembly, string Signature)> addedKeys = new(
            delta.Added.Select(entry => (entry.AssemblyName, entry.Signature)));

        foreach (string assemblyName in contract.Assemblies)
        {
            if (!scannedByAssembly.TryGetValue(assemblyName, out List<ArchitectureExportedApiEntry>? scanned))
            {
                continue;
            }

            List<(ArchitectureExportedApiEntry Entry, bool ForbiddenConstant, bool Undeclared)> violatingEntries = new();

            foreach (ArchitectureExportedApiEntry entry in scanned)
            {
                string projected = exactGrammar ? entry.ExactSignature : entry.Signature;

                bool undeclared = exact
                    ? addedKeys.Contains((entry.AssemblyName, projected))
                    : !snapshotDeclared.Contains((entry.AssemblyName, projected))
                        && !inlineDeclared.Contains(entry.Signature);

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
                string reported = exactGrammar ? entry.ExactSignature : entry.Signature;
                if (executionContext.IsIgnored(entry.DeclaringTypeName, entry.Signature)
                    || executionContext.IsIgnored(entry.DeclaringTypeName, reported))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    entry.DeclaringTypeName,
                    "public API surface",
                    new[] { reported })
                {
                    Payload = new PublicApiSurfacePayload(
                        UndeclaredApiSignature: reported,
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

    // Inline `declared_api` entries carry no assembly attribution, so they enter the differ as
    // wildcards. Under the exact grammar they are also written in the legacy identity grammar, so an
    // inline entry that still has a live counterpart is lifted to that counterpart's exact signature
    // — otherwise every inline entry on a snapshot-backed contract would look removed.
    private static IReadOnlyList<PublicApiSnapshotEntry> DeclaredEntries(
        ArchitecturePublicApiSurfaceContract contract,
        Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly,
        bool exactGrammar)
    {
        List<PublicApiSnapshotEntry> declared = new(contract.ResolvedSnapshotEntries);

        Dictionary<string, ArchitectureExportedApiEntry> byBaseSignature = new(StringComparer.Ordinal);
        if (exactGrammar)
        {
            foreach (ArchitectureExportedApiEntry entry in scannedByAssembly.Values.SelectMany(entries => entries))
            {
                byBaseSignature.TryAdd(entry.Signature, entry);
            }
        }

        foreach (string signature in contract.DeclaredApi)
        {
            if (exactGrammar && byBaseSignature.TryGetValue(signature, out ArchitectureExportedApiEntry live))
            {
                declared.Add(new PublicApiSnapshotEntry(live.AssemblyName, live.ExactSignature));
                continue;
            }

            declared.Add(new PublicApiSnapshotEntry(PublicApiSnapshotDiffer.WildcardAssembly, signature));
        }

        return declared;
    }

    private static IReadOnlyList<PublicApiSnapshotEntry> ActualEntries(
        Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly,
        bool exactGrammar)
    {
        return scannedByAssembly.Values
            .SelectMany(entries => entries)
            .Select(entry => new PublicApiSnapshotEntry(
                entry.AssemblyName, exactGrammar ? entry.ExactSignature : entry.Signature))
            .ToList();
    }
}
