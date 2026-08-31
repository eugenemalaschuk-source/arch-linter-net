using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal sealed class ArchitecturePublicApiSurfaceAnalysisService
{
    private readonly ArchitectureAnalysisSession _session;

    public ArchitecturePublicApiSurfaceAnalysisService(ArchitectureAnalysisSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public List<ArchitectureViolation> CheckPublicApiSurfaceContract(ArchitecturePublicApiSurfaceContract contract)
    {
        if (!_session.IsContractSelected(contract.Id) || _session.IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = _session.CreateExecutionContext(contract, contract.IgnoredViolations);
        IReadOnlyDictionary<string, Assembly> resolvedAssemblies = _session.Facts.BuildAssemblyLookup();
        List<ArchitectureViolation> violations = PublicApiSurfaceChecker.Check(
            contract, resolvedAssemblies, executionContext,
            PublicApiSurfaceChecker.BuildSurfaceSelectorPredicate(contract, _session.Document, _session.RoleIndex),
            _session.GetPublicApiSurface);
        _session.CollectUnmatchedIgnores(executionContext);
        return violations;
    }

    // The contract-surface exposure family consumes this exact selected exported-type universe.
    // Keeping the resolver on the public-api analysis seam is important: API membership remains
    // owned by the materialization/index path and exposure evaluation never reconstructs it from
    // snapshots or by running a second reflection selector.
    internal ArchitecturePublicApiSurfaceRootResolution ResolveSelectedRoots(
        string publicApiSurfaceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicApiSurfaceId);

        ArchitecturePublicApiSurfaceContract? contract = _session.Document.Contracts.StrictPublicApiSurface
            .Concat(_session.Document.Contracts.AuditPublicApiSurface)
            .SingleOrDefault(candidate => string.Equals(
                candidate.Id, publicApiSurfaceId, StringComparison.OrdinalIgnoreCase));

        if (contract is null)
        {
            return new ArchitecturePublicApiSurfaceRootResolution(
                Array.Empty<Type>(), IsComplete: false, HasContract: false);
        }

        IReadOnlyDictionary<string, Assembly> resolvedAssemblies = _session.Facts.BuildAssemblyLookup();
        Func<Type, bool>? selectorPredicate =
            PublicApiSurfaceChecker.BuildSurfaceSelectorPredicate(contract, _session.Document, _session.RoleIndex);
        List<Type> roots = new();
        // Snapshot capture/diff errors affect only the compatibility artifact lifecycle. They do
        // not make the policy-selected exported membership unknowable to another checker.
        bool isComplete = true;

        foreach (string assemblyName in contract.Assemblies
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!resolvedAssemblies.TryGetValue(assemblyName, out Assembly? assembly))
            {
                isComplete = false;
                continue;
            }

            ArchitecturePublicApiSurfaceMaterialization surface = _session.GetPublicApiSurface(assembly);
            isComplete &= surface.IsComplete;
            roots.AddRange(selectorPredicate is null
                ? surface.ExportedTypes
                : surface.ExportedTypes.Where(selectorPredicate));
        }

        Type[] distinctRoots = roots
            .Distinct()
            .OrderBy(type => type.Assembly.FullName ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(type => type.FullName ?? type.Name, StringComparer.Ordinal)
            .ToArray();
        return new ArchitecturePublicApiSurfaceRootResolution(
            distinctRoots, isComplete, HasContract: true);
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
        return CapturePublicApiSurface(
            contract,
            out missingAssemblies,
            out selectorSafetyViolations,
            out _);
    }

    // The metric evaluator needs this integrity bit in addition to the legacy captured entries.
    // Validation retains the established best-effort materialization behavior and deliberately does
    // not turn a partial exported type universe into a new validation diagnostic.
    internal IReadOnlyList<PublicApiSnapshotEntry> CapturePublicApiSurface(
        ArchitecturePublicApiSurfaceContract contract,
        out IReadOnlyList<string> missingAssemblies,
        out IReadOnlyList<ArchitectureViolation> selectorSafetyViolations,
        out bool isComplete)
    {
        IReadOnlyDictionary<string, Assembly> resolvedAssemblies = _session.Facts.BuildAssemblyLookup();
        Func<Type, bool>? selectorPredicate =
            PublicApiSurfaceChecker.BuildSurfaceSelectorPredicate(contract, _session.Document, _session.RoleIndex);
        Func<Assembly, ArchitecturePublicApiSurfaceMaterialization> surfaceResolver = _session.GetPublicApiSurface;
        List<PublicApiSnapshotEntry> entries = new();
        List<string> missing = new();
        isComplete = true;

        foreach (string assemblyName in contract.Assemblies.Distinct(StringComparer.Ordinal)
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!resolvedAssemblies.TryGetValue(assemblyName, out Assembly? targetAssembly))
            {
                missing.Add(assemblyName);
                continue;
            }

            ArchitecturePublicApiSurfaceMaterialization surface = surfaceResolver(targetAssembly);
            isComplete &= surface.IsComplete;
            IEnumerable<ArchitectureExportedApiEntry> exported = surface.Entries;
            if (selectorPredicate != null)
            {
                HashSet<string> selectedTypeNames = surface.ExportedTypes
                    .Where(selectorPredicate)
                    .Select(ArchitectureTypeNames.SafeFullName)
                    .ToHashSet(StringComparer.Ordinal);
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
                contract,
                resolvedAssemblies,
                _session.CreateExecutionContext(contract, contract.IgnoredViolations),
                selectorPredicate,
                surfaceResolver)
            : Array.Empty<ArchitectureViolation>();

        return entries;
    }
}
