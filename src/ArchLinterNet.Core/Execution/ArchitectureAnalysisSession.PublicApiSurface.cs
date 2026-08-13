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
            contract, resolvedAssemblies, executionContext,
            PublicApiSurfaceChecker.BuildSurfaceSelectorPredicate(contract, Document, RoleIndex));
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    // Captures a contract's exported surface without evaluating it against any declaration. This is
    // the read side the public-api capture/diff/update/migrate workflow builds on; contract
    // selection and ignore matching deliberately do not apply to the captured entries themselves,
    // because a snapshot describes what the assemblies actually export, not what the policy
    // currently tolerates. A configured surface_selector still applies — capture must reflect the
    // same selected surface strict/audit validation governs (issue #525).
    //
    // Kept as its own public overload (unchanged since before #525) rather than folded into the
    // safety-aware one below: ArchitectureAnalysisSession is reviewed public API surface, and adding
    // an out parameter would replace this method's signature rather than add to it, breaking every
    // precompiled caller (PR #529 review). Callers needing the selector-safety verdict (currently
    // only ResolveSurface) use the internal overload directly.
    public IReadOnlyList<PublicApiSnapshotEntry> CapturePublicApiSurface(
        ArchitecturePublicApiSurfaceContract contract,
        out IReadOnlyList<string> missingAssemblies)
    {
        return CapturePublicApiSurface(contract, out missingAssemblies, out _);
    }

    // The selectorSafetyViolations output surfaces the same zero-match and first-party-escape
    // fail-closed checks strict and audit validation already run, so an unsafe selector
    // configuration cannot silently produce a usable snapshot through the capture, diff, update, or
    // migrate lifecycle. Ignore matching applies to those checks too, so a violation a reviewer has
    // already accepted during validation does not re-block this same lifecycle.
    //
    // This overload stays internal because only the public-api application service, in the same
    // assembly, currently needs the verdict; exposing it publicly would grow the reviewed surface
    // without an actual caller requiring it.
    internal IReadOnlyList<PublicApiSnapshotEntry> CapturePublicApiSurface(
        ArchitecturePublicApiSurfaceContract contract,
        out IReadOnlyList<string> missingAssemblies,
        out IReadOnlyList<ArchitectureViolation> selectorSafetyViolations)
    {
        Dictionary<string, Assembly> resolvedAssemblies = BuildAssemblyLookup();
        Func<Type, bool>? selectorPredicate =
            PublicApiSurfaceChecker.BuildSurfaceSelectorPredicate(contract, Document, RoleIndex);
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
}
