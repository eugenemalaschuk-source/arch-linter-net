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
    private const string UnusableSnapshotDelta = "snapshot-unusable";
    private const string ViolationCategory = "public API surface";

    public static List<ArchitectureViolation> Check(
        ArchitecturePublicApiSurfaceContract contract,
        IReadOnlyDictionary<string, Assembly> resolvedAssemblies,
        ArchitectureContractExecutionContext executionContext)
    {
        // A missing, unparsable, or foreign snapshot is deliberately not a policy-load failure (see
        // PublicApiSnapshotResolver), so validation is where it has to become loud. Reporting the
        // whole surface as undeclared instead would bury the actual cause in noise.
        if (contract.ApiSnapshotError != null)
        {
            return new List<ArchitectureViolation> { UnusableSnapshotViolation(contract) };
        }

        Evaluation evaluation = Evaluate(contract, resolvedAssemblies);
        List<ArchitectureViolation> violations = CollectSurfaceViolations(contract, evaluation, executionContext);

        AddDeltaViolations(contract, evaluation.Delta.Removed, RemovedDelta, executionContext, violations);
        AddDeltaViolations(contract, evaluation.Delta.Changed, ChangedDelta, executionContext, violations);

        return violations;
    }

    private static Evaluation Evaluate(
        ArchitecturePublicApiSurfaceContract contract,
        IReadOnlyDictionary<string, Assembly> resolvedAssemblies)
    {
        // The reviewed snapshot records the exact grammar (base signature plus the detail suffix
        // carrying constant values, accessor shape, static/ref/out/in, sealed/abstract, generic
        // constraints). A contract with only an inline `declared_api` list keeps comparing against
        // the legacy identity grammar, which is what keeps existing policies working unchanged.
        bool exactGrammar = !string.IsNullOrWhiteSpace(contract.ApiSnapshot);

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

        return new Evaluation(
            scannedByAssembly,
            exactGrammar,
            exact,
            delta,
            new HashSet<(string, string)>(delta.Added.Select(entry => (entry.AssemblyName, entry.Signature))),
            new HashSet<string>(contract.DeclaredApi, StringComparer.Ordinal),
            new HashSet<(string, string)>(
                contract.ResolvedSnapshotEntries.Select(entry => (entry.AssemblyName, entry.Signature))),
            new HashSet<string>(contract.AllowedPublicConstants, StringComparer.Ordinal));
    }

    private static List<ArchitectureViolation> CollectSurfaceViolations(
        ArchitecturePublicApiSurfaceContract contract,
        Evaluation evaluation,
        ArchitectureContractExecutionContext executionContext)
    {
        return contract.Assemblies
            .Select(assemblyName => evaluation.ScannedByAssembly.GetValueOrDefault(assemblyName))
            .Where(scanned => scanned != null)
            .SelectMany(scanned => OrderedViolations(contract, evaluation, scanned!))
            .Select(verdict => TryBuildViolation(contract, evaluation, verdict, executionContext))
            .Where(violation => violation != null)
            .Select(violation => violation!)
            .ToList();
    }

    private static IEnumerable<EntryVerdict> OrderedViolations(
        ArchitecturePublicApiSurfaceContract contract,
        Evaluation evaluation,
        List<ArchitectureExportedApiEntry> scanned)
    {
        return scanned
            .Select(entry => Classify(contract, evaluation, entry))
            .Where(verdict => verdict.IsViolation)
            .OrderBy(verdict => verdict.Entry.DeclaringTypeName, StringComparer.Ordinal)
            .ThenBy(verdict => verdict.Entry.Signature, StringComparer.Ordinal);
    }

    // Ignore entries may be authored against either grammar, so both the legacy identity and the
    // reported (possibly exact) signature are offered to the matcher.
    private static ArchitectureViolation? TryBuildViolation(
        ArchitecturePublicApiSurfaceContract contract,
        Evaluation evaluation,
        EntryVerdict verdict,
        ArchitectureContractExecutionContext executionContext)
    {
        string reported = evaluation.ExactGrammar ? verdict.Entry.ExactSignature : verdict.Entry.Signature;

        if (executionContext.IsIgnored(verdict.Entry.DeclaringTypeName, verdict.Entry.Signature)
            || executionContext.IsIgnored(verdict.Entry.DeclaringTypeName, reported))
        {
            return null;
        }

        return new ArchitectureViolation(
            contract.Name,
            contract.Id,
            verdict.Entry.DeclaringTypeName,
            ViolationCategory,
            new[] { reported })
        {
            Payload = new PublicApiSurfacePayload(
                UndeclaredApiSignature: reported,
                ForbiddenPublicConstant: verdict.ForbiddenConstant ? true : null,
                ApiAssemblyName: verdict.Entry.AssemblyName,
                ApiVisibility: verdict.Entry.Visibility,
                ApiDeltaKind: verdict.Undeclared ? AddedDelta : null),
        };
    }

    private static EntryVerdict Classify(
        ArchitecturePublicApiSurfaceContract contract,
        Evaluation evaluation,
        ArchitectureExportedApiEntry entry)
    {
        string projected = evaluation.ExactGrammar ? entry.ExactSignature : entry.Signature;

        bool undeclared = evaluation.Exact
            ? evaluation.AddedKeys.Contains((entry.AssemblyName, projected))
            : !evaluation.SnapshotDeclared.Contains((entry.AssemblyName, projected))
                && !evaluation.InlineDeclared.Contains(entry.Signature);

        bool forbiddenConstant = contract.ForbidPublicConstantsUnlessDeclared
            && entry.IsConst
            && entry.ConstQualifiedName != null
            && !evaluation.AllowedConstants.Contains(entry.ConstQualifiedName);

        return new EntryVerdict(entry, forbiddenConstant, undeclared);
    }

    private static ArchitectureViolation UnusableSnapshotViolation(ArchitecturePublicApiSurfaceContract contract)
    {
        return new ArchitectureViolation(
            contract.Name,
            contract.Id,
            contract.Name,
            "public API snapshot",
            new[] { contract.ApiSnapshotError! })
        {
            Payload = new PublicApiSurfacePayload(
                UndeclaredApiSignature: contract.ApiSnapshotError,
                ApiDeltaKind: UnusableSnapshotDelta),
        };
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
                ViolationCategory,
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
    // wildcards (PublicApiSnapshotDiffer.WildcardAssembly) — that is the spec's stated semantics for
    // an unattributed entry, and it must survive this step. Under the exact grammar the live surface
    // is written in a richer grammar than the legacy `declared_api` text, so a wildcard entry is
    // lifted to the live exact-signature text or it would text-compare unequal to every actual entry
    // and look removed. Lifting to a *specific assembly* would be wrong: two assemblies can
    // legitimately export the same base signature (possibly with different exact detail, e.g.
    // different visibility), and the wildcard must still match whichever of them the caller meant —
    // so every distinct live exact variant for that base signature becomes its own wildcard entry.
    // Internal (not private) specifically so it is directly unit-testable: constructing a real
    // duplicate export of the same base signature across two distinct loaded assemblies is not
    // practical in a unit test, and this is where the actual attribution logic lives.
    internal static List<PublicApiSnapshotEntry> DeclaredEntries(
        ArchitecturePublicApiSurfaceContract contract,
        Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly,
        bool exactGrammar)
    {
        List<PublicApiSnapshotEntry> declared = new(contract.ResolvedSnapshotEntries);

        ILookup<string, string> exactVariantsByBaseSignature = exactGrammar
            ? scannedByAssembly.Values
                .SelectMany(entries => entries)
                .ToLookup(entry => entry.Signature, entry => entry.ExactSignature, StringComparer.Ordinal)
            : Enumerable.Empty<ArchitectureExportedApiEntry>()
                .ToLookup(entry => entry.Signature, entry => entry.ExactSignature, StringComparer.Ordinal);

        foreach (string signature in contract.DeclaredApi)
        {
            string[] liveVariants = exactVariantsByBaseSignature[signature].Distinct(StringComparer.Ordinal).ToArray();

            if (liveVariants.Length == 0)
            {
                declared.Add(new PublicApiSnapshotEntry(PublicApiSnapshotDiffer.WildcardAssembly, signature));
                continue;
            }

            declared.AddRange(liveVariants.Select(
                variant => new PublicApiSnapshotEntry(PublicApiSnapshotDiffer.WildcardAssembly, variant)));
        }

        return declared;
    }

    private static List<PublicApiSnapshotEntry> ActualEntries(
        Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly,
        bool exactGrammar)
    {
        return scannedByAssembly.Values
            .SelectMany(entries => entries)
            .Select(entry => new PublicApiSnapshotEntry(
                entry.AssemblyName, exactGrammar ? entry.ExactSignature : entry.Signature))
            .ToList();
    }

    private readonly record struct EntryVerdict(
        ArchitectureExportedApiEntry Entry,
        bool ForbiddenConstant,
        bool Undeclared)
    {
        public bool IsViolation => ForbiddenConstant || Undeclared;
    }

    private sealed record Evaluation(
        Dictionary<string, List<ArchitectureExportedApiEntry>> ScannedByAssembly,
        bool ExactGrammar,
        bool Exact,
        PublicApiDelta Delta,
        HashSet<(string Assembly, string Signature)> AddedKeys,
        HashSet<string> InlineDeclared,
        HashSet<(string Assembly, string Signature)> SnapshotDeclared,
        HashSet<string> AllowedConstants);
}
