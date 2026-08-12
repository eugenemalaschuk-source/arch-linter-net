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
    private const string ZeroMatchDelta = "selector-zero-match";
    private const string ViolationCategory = "public API surface";

    public static List<ArchitectureViolation> Check(
        ArchitecturePublicApiSurfaceContract contract,
        IReadOnlyDictionary<string, Assembly> resolvedAssemblies,
        ArchitectureContractExecutionContext executionContext,
        Func<Type, bool>? surfaceSelectorPredicate)
    {
        // A missing, unparsable, or foreign snapshot is deliberately not a policy-load failure (see
        // PublicApiSnapshotResolver), so validation is where it has to become loud. Reporting the
        // whole surface as undeclared instead would bury the actual cause in noise.
        if (contract.ApiSnapshotError != null)
        {
            return new List<ArchitectureViolation> { UnusableSnapshotViolation(contract) };
        }

        (Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly,
                Dictionary<string, List<ArchitectureExportedApiEntry>> governedByAssembly) =
            ScanAndFilter(contract, resolvedAssemblies, surfaceSelectorPredicate);

        List<ArchitectureViolation> selectorSafetyViolations =
            CheckSelectorSafety(contract, scannedByAssembly, governedByAssembly, executionContext);

        // Zero-match is the sole finding when it applies — the selected surface is empty, so there
        // is nothing else to evaluate against declared_api/exact-diff.
        if (selectorSafetyViolations.Any(IsZeroMatch))
        {
            return selectorSafetyViolations;
        }

        Evaluation evaluation = BuildEvaluation(contract, scannedByAssembly, governedByAssembly);

        List<ArchitectureViolation> violations = CollectSurfaceViolations(contract, evaluation, executionContext);
        violations.AddRange(selectorSafetyViolations);

        AddDeltaViolations(contract, evaluation.Delta.Removed, RemovedDelta, executionContext, violations);
        AddDeltaViolations(contract, evaluation.Delta.Changed, ChangedDelta, executionContext, violations);

        return violations;
    }

    // The selector-safety checks (zero-match, first-party escape) independent of declared_api/exact
    // diff — the part strict/audit validation and the capture/diff/update/migrate lifecycle must
    // agree on identically, since a selector unsafe for one is unsafe for the other (issue #525,
    // PR #529 review: capture/update previously could accept a selector configuration validate would
    // reject). This overload scans for itself, for callers (the capture/update/diff/migrate seam)
    // that have not already built scannedByAssembly/governedByAssembly.
    internal static List<ArchitectureViolation> CheckSelectorSafety(
        ArchitecturePublicApiSurfaceContract contract,
        IReadOnlyDictionary<string, Assembly> resolvedAssemblies,
        ArchitectureContractExecutionContext executionContext,
        Func<Type, bool>? surfaceSelectorPredicate)
    {
        (Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly,
                Dictionary<string, List<ArchitectureExportedApiEntry>> governedByAssembly) =
            ScanAndFilter(contract, resolvedAssemblies, surfaceSelectorPredicate);

        return CheckSelectorSafety(contract, scannedByAssembly, governedByAssembly, executionContext);
    }

    private static List<ArchitectureViolation> CheckSelectorSafety(
        ArchitecturePublicApiSurfaceContract contract,
        Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly,
        Dictionary<string, List<ArchitectureExportedApiEntry>> governedByAssembly,
        ArchitectureContractExecutionContext executionContext)
    {
        if (contract.SurfaceSelector == null)
        {
            return new List<ArchitectureViolation>();
        }

        // Selector resolution needs reflected assemblies, unavailable at policy-load time, so a
        // required selector matching nothing has to become loud here instead — mirroring the
        // ApiSnapshotError short-circuit in Check() — or a typo'd selector would silently pass
        // strict validation, or silently capture/update an empty snapshot, with an empty effective
        // surface.
        if (scannedByAssembly.Count > 0 && governedByAssembly.Values.All(entries => entries.Count == 0))
        {
            return new List<ArchitectureViolation> { ZeroMatchSelectorViolation(contract) };
        }

        return CollectFirstPartyEscapeViolations(contract, scannedByAssembly, governedByAssembly, executionContext).ToList();
    }

    private static bool IsZeroMatch(ArchitectureViolation violation) =>
        (violation.Payload as PublicApiSurfacePayload)?.ApiDeltaKind == ZeroMatchDelta;

    private static (
        Dictionary<string, List<ArchitectureExportedApiEntry>> ScannedByAssembly,
        Dictionary<string, List<ArchitectureExportedApiEntry>> GovernedByAssembly) ScanAndFilter(
        ArchitecturePublicApiSurfaceContract contract,
        IReadOnlyDictionary<string, Assembly> resolvedAssemblies,
        Func<Type, bool>? surfaceSelectorPredicate)
    {
        Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly =
            ScanContractAssemblies(contract, resolvedAssemblies);

        // scannedByAssembly stays the full, unfiltered first-party universe (used by the escape
        // check); governedByAssembly is what a selector, when configured, actually governs — every
        // other computation (violations, exact diff) targets the governed set so a type outside the
        // selected surface is never enumerated or reported at all.
        Dictionary<string, List<ArchitectureExportedApiEntry>> governedByAssembly = surfaceSelectorPredicate == null
            ? scannedByAssembly
            : FilterToSelected(scannedByAssembly, resolvedAssemblies, surfaceSelectorPredicate);

        return (scannedByAssembly, governedByAssembly);
    }

    private static Evaluation BuildEvaluation(
        ArchitecturePublicApiSurfaceContract contract,
        Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly,
        Dictionary<string, List<ArchitectureExportedApiEntry>> governedByAssembly)
    {
        // The reviewed snapshot records the exact grammar (base signature plus the detail suffix
        // carrying constant values, accessor shape, static/ref/out/in, sealed/abstract, generic
        // constraints). A contract with only an inline `declared_api` list keeps comparing against
        // the legacy identity grammar, which is what keeps existing policies working unchanged.
        bool exactGrammar = !string.IsNullOrWhiteSpace(contract.ApiSnapshot);

        // Exact mode replaces "is this signature declared?" with a correlated delta, so a re-signed
        // member reports once as a change instead of as an unrelated addition plus removal. It only
        // runs when every declared assembly actually resolved: against a partially resolved
        // contract, every entry of the missing assembly would masquerade as a removal.
        bool exact = string.Equals(contract.ApiComparison, PublicApiComparisonModes.Exact, StringComparison.Ordinal)
            && governedByAssembly.Count == contract.Assemblies.Distinct(StringComparer.Ordinal).Count();

        PublicApiDelta delta = exact
            ? PublicApiSnapshotDiffer.Diff(
                DeclaredEntries(contract, governedByAssembly, exactGrammar),
                ActualEntries(governedByAssembly, exactGrammar))
            : PublicApiDelta.Empty;

        return new Evaluation(
            governedByAssembly,
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

    // Per-assembly selected type full names, computed once and applied to that assembly's already-
    // scanned entries. Assembly keys mirror scannedByAssembly (only resolved assemblies), values
    // filtered to what the selector actually matched — possibly empty, which is what the zero-match
    // check above looks for.
    private static Dictionary<string, List<ArchitectureExportedApiEntry>> FilterToSelected(
        Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly,
        IReadOnlyDictionary<string, Assembly> resolvedAssemblies,
        Func<Type, bool> surfaceSelectorPredicate)
    {
        Dictionary<string, List<ArchitectureExportedApiEntry>> governed = new(StringComparer.Ordinal);

        foreach ((string assemblyName, List<ArchitectureExportedApiEntry> entries) in scannedByAssembly)
        {
            if (!resolvedAssemblies.TryGetValue(assemblyName, out Assembly? assembly))
            {
                continue;
            }

            HashSet<string> selectedTypeNames =
                ArchitecturePublicApiSurfaceScanner.SelectedTypeFullNames(assembly, surfaceSelectorPredicate);
            governed[assemblyName] = entries.Where(entry => selectedTypeNames.Contains(entry.DeclaringTypeName)).ToList();
        }

        return governed;
    }

    // Fails closed when a governed (selected) member's signature depends on a first-party exported
    // type (declared in one of the contract's own assemblies) that surface_selector did not itself
    // select. Caller (CheckSelectorSafety) only invokes this when a selector is configured — with no
    // selector every first-party type is governed by construction, so escape is impossible.
    private static IEnumerable<ArchitectureViolation> CollectFirstPartyEscapeViolations(
        ArchitecturePublicApiSurfaceContract contract,
        Dictionary<string, List<ArchitectureExportedApiEntry>> scannedByAssembly,
        Dictionary<string, List<ArchitectureExportedApiEntry>> governedByAssembly,
        ArchitectureContractExecutionContext executionContext)
    {
        // Keyed by (assembly, type name), not name alone: two distinct assemblies can legitimately
        // export a type under the identical full name, and a name-only key would let a selected
        // assembly's type mask an unselected same-named type from a different assembly.
        HashSet<(string Assembly, string Type)> firstPartyTypes = new(
            scannedByAssembly.Values.SelectMany(entries => entries)
                .Select(entry => (entry.AssemblyName, entry.DeclaringTypeName)));
        HashSet<(string Assembly, string Type)> selectedTypes = new(
            governedByAssembly.Values.SelectMany(entries => entries)
                .Select(entry => (entry.AssemblyName, entry.DeclaringTypeName)));

        foreach (ArchitectureExportedApiEntry entry in governedByAssembly.Values.SelectMany(entries => entries))
        {
            foreach ((string referencedAssembly, string referencedType) in entry.ReferencedTypes)
            {
                (string, string) referenced = (referencedAssembly, referencedType);
                if (!firstPartyTypes.Contains(referenced) || selectedTypes.Contains(referenced))
                {
                    continue;
                }

                if (executionContext.IsIgnored(
                        entry.DeclaringTypeName,
                        entry.Signature,
                        sourceAssembly: entry.AssemblyName,
                        targetAssembly: entry.AssemblyName,
                        targetType: entry.DeclaringTypeName,
                        targetMember: entry.Signature))
                {
                    continue;
                }

                yield return new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    entry.DeclaringTypeName,
                    ViolationCategory,
                    new[] { entry.Signature })
                {
                    Payload = new PublicApiSurfacePayload(
                        UndeclaredApiSignature: entry.Signature,
                        ApiAssemblyName: entry.AssemblyName,
                        ApiVisibility: entry.Visibility)
                    {
                        UnselectedFirstPartyDependency = referencedType,
                    },
                };
            }
        }
    }

    private static ArchitectureViolation ZeroMatchSelectorViolation(ArchitecturePublicApiSurfaceContract contract)
    {
        const string Message =
            "surface_selector matched zero exported types across the contract's resolved assemblies.";
        return new ArchitectureViolation(
            contract.Name, contract.Id, contract.Name, "public API surface selector", new[] { Message })
        {
            Payload = new PublicApiSurfacePayload(
                UndeclaredApiSignature: Message,
                ApiDeltaKind: ZeroMatchDelta),
        };
    }

    private static List<ArchitectureViolation> CollectSurfaceViolations(
        ArchitecturePublicApiSurfaceContract contract,
        Evaluation evaluation,
        ArchitectureContractExecutionContext executionContext)
    {
        return contract.Assemblies
            .Select(assemblyName => evaluation.GovernedByAssembly.GetValueOrDefault(assemblyName))
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

        if (executionContext.IsIgnoredWithAliases(
                verdict.Entry.DeclaringTypeName,
                new[] { verdict.Entry.Signature, reported }.Distinct(StringComparer.Ordinal).ToArray(),
                reported,
                sourceAssembly: verdict.Entry.AssemblyName,
                targetAssembly: verdict.Entry.AssemblyName,
                targetType: verdict.Entry.DeclaringTypeName,
                targetMember: reported))
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
            if (executionContext.IsIgnored(
                    declaringTypeName,
                    entry.Signature,
                    sourceAssembly: entry.AssemblyName,
                    targetAssembly: entry.AssemblyName,
                    targetType: declaringTypeName,
                    targetMember: entry.Signature))
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
        Dictionary<string, List<ArchitectureExportedApiEntry>> GovernedByAssembly,
        Dictionary<string, List<ArchitectureExportedApiEntry>> ScannedByAssembly,
        bool ExactGrammar,
        bool Exact,
        PublicApiDelta Delta,
        HashSet<(string Assembly, string Signature)> AddedKeys,
        HashSet<string> InlineDeclared,
        HashSet<(string Assembly, string Signature)> SnapshotDeclared,
        HashSet<string> AllowedConstants);
}
