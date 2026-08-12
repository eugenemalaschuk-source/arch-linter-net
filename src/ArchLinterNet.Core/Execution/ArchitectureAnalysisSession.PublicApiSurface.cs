using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckPublicApiSurfaceContract(ArchitecturePublicApiSurfaceContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        Dictionary<string, Assembly> resolvedAssemblies = BuildAssemblyLookup();
        List<ArchitectureViolation> violations = PublicApiSurfaceChecker.Check(
            contract, resolvedAssemblies, executionContext, BuildSurfaceSelectorPredicate(contract));
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    // Captures a contract's exported surface without evaluating it against any declaration. This is
    // the read side the public-api capture/diff/update/migrate workflow builds on; contract
    // selection and ignore matching deliberately do not apply to the captured entries themselves,
    // because a snapshot describes what the assemblies actually export, not what the policy
    // currently tolerates. A configured surface_selector still applies — capture must reflect the
    // same selected surface strict/audit validation governs (issue #525) — and selectorSafetyViolations
    // surfaces the same zero-match/first-party-escape fail-closed checks strict/audit validation runs
    // (PR #529 review), so a selector configuration unsafe for `validate` cannot silently produce a
    // usable snapshot through `capture`/`diff`/`update`/`migrate`. Ignore matching DOES apply to
    // those checks — the same ignored_violations a reviewer already accepted in `validate` should not
    // re-block the lifecycle that reuses this method.
    public IReadOnlyList<PublicApiSnapshotEntry> CapturePublicApiSurface(
        ArchitecturePublicApiSurfaceContract contract,
        out IReadOnlyList<string> missingAssemblies,
        out IReadOnlyList<ArchitectureViolation> selectorSafetyViolations)
    {
        Dictionary<string, Assembly> resolvedAssemblies = BuildAssemblyLookup();
        Func<Type, bool>? selectorPredicate = BuildSurfaceSelectorPredicate(contract);
        List<PublicApiSnapshotEntry> entries = new();
        List<string> missing = new();

        foreach (string assemblyName in contract.Assemblies.Distinct(StringComparer.Ordinal)
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!resolvedAssemblies.TryGetValue(assemblyName, out Assembly? targetAssembly))
            {
                missing.Add(assemblyName);
                continue;
            }

            IEnumerable<ArchitectureExportedApiEntry> exported =
                ArchitecturePublicApiSurfaceScanner.GetExportedSurface(targetAssembly);
            if (selectorPredicate != null)
            {
                HashSet<string> selectedTypeNames =
                    ArchitecturePublicApiSurfaceScanner.SelectedTypeFullNames(targetAssembly, selectorPredicate);
                exported = exported.Where(entry => selectedTypeNames.Contains(entry.DeclaringTypeName));
            }

            // A snapshot records the exact grammar (base signature plus the detail suffix), because
            // an identity-only capture cannot tell a changed constant value, accessor shape, or
            // ref/out direction from no change at all.
            entries.AddRange(exported.Select(entry => new PublicApiSnapshotEntry(entry.AssemblyName, entry.ExactSignature)));
        }

        missingAssemblies = missing;

        // An unresolved assembly already fails the operation on its own (missingAssemblies); running
        // the safety check against a partial universe would misreport escapes/zero-match against
        // types that simply never resolved, mirroring how exact-diff mode guards the same condition.
        selectorSafetyViolations = missing.Count == 0
            ? PublicApiSurfaceChecker.CheckSelectorSafety(
                contract, resolvedAssemblies, CreateExecutionContext(contract, contract.IgnoredViolations), selectorPredicate)
            : Array.Empty<ArchitectureViolation>();

        return entries;
    }

    // Reuses ArchitectureTypeRoleMatcher (structural fields) and the semantic role index (Role)
    // through ArchitecturePublicApiSurfaceSelectorMatcher — no new matcher engine. Null selector
    // means "no surface_selector authored", which every existing call site treats as "everything is
    // governed", preserving pre-existing assembly-wide behavior exactly.
    private Func<Type, bool>? BuildSurfaceSelectorPredicate(ArchitecturePublicApiSurfaceContract contract)
    {
        if (contract.SurfaceSelector == null)
        {
            return null;
        }

        ArchitecturePublicApiSurfaceSelector selector = contract.SurfaceSelector;
        return type => ArchitecturePublicApiSurfaceSelectorMatcher.Matches(type, selector, Document, contract.Name, RoleIndex);
    }
}
