using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    // Suppression/ignore key for an incomplete reference scan on a source type - distinct from any
    // real target name, since there is no single forbidden reference to point at.
    private const string UnsupportedEvidenceReference = "<unsupported-evidence>";

    public List<ArchitectureViolation> CheckPortBoundaryContract(ArchitecturePortBoundaryContract contract)
    {
        if (!IsContractSelected(contract.Id)) return new List<ArchitectureViolation>();
        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext context = CreateExecutionContext(contract, contract.IgnoredViolations);
        foreach (Type source in FindContextSelectorMatchingTypes(contract.Source))
        {
            if (!RoleIndex.TryGetRole(source, out ArchitectureTypeClassificationResult sourceRole)) continue;
            string sourceName = ArchitectureTypeNames.SafeFullName(source);

            // Per the "Unsupported evidence fails closed with explicit diagnostics" requirement:
            // a member (field/property/method/parameter) whose type couldn't be loaded is silently
            // dropped from the reference scan, not merely skipped - the source's real target set may
            // be incomplete, so a forbidden direct edge could vanish with no violation and no signal.
            // Report that explicitly instead of letting the contract pass on partial evidence.
            bool scanComplete = ArchitectureReferenceScanner.TryGetReferencedTypes(source, out List<Type> referencedTypes);
            if (!scanComplete && !context.IsIgnored(sourceName, UnsupportedEvidenceReference))
            {
                violations.Add(BuildUnsupportedEvidenceViolation(contract, sourceRole, sourceName));
            }

            foreach (Type target in referencedTypes.Distinct())
            {
                ArchitectureViolation? violation = TryBuildDirectEdgeViolation(contract, context, sourceRole, sourceName, target);
                if (violation != null) violations.Add(violation);
            }
        }
        CollectAdapterBindingViolations(contract, context, violations);
        context.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private ArchitectureViolation? TryBuildDirectEdgeViolation(ArchitecturePortBoundaryContract contract,
        ArchitectureContractExecutionContext context, ArchitectureTypeClassificationResult sourceRole,
        string sourceName, Type target)
    {
        if (!MatchesTargetContext(contract.TargetContext, target, sourceRole)
            || IsExcludedFromContextMatch(target, contract.Exclude, sourceRole)) return null;
        bool matchesForbidden = contract.Forbidden.Any(s => ArchitectureContextSelectorMatcher.Matches(s, target, RoleIndex, sourceRole));
        bool matchesAllowedSeam = contract.AllowedSeams.Any(s => ArchitectureContextSelectorMatcher.Matches(s, target, RoleIndex, sourceRole));
        if (matchesAllowedSeam && !matchesForbidden) return null;
        string targetName = ArchitectureTypeNames.SafeFullName(target);
        if (string.IsNullOrEmpty(targetName) || context.IsIgnored(sourceName, targetName)) return null;
        RoleIndex.TryGetRole(target, out ArchitectureTypeClassificationResult targetRole);
        string expectedSeam = string.Join(" or ", contract.AllowedSeams.Select(DescribeContextSelector));
        // matchesForbidden distinguishes an explicit forbidden-selector match from a target that
        // simply never matched any allowed_seams selector - both are violations, but the evidence
        // kind/message tell an operator which of the two authoring gaps to close.
        string evidenceKind = matchesForbidden ? "forbidden_reference" : "missing_approved_seam";
        string message = matchesForbidden
            ? $"forbidden direct edge; expected seam: {expectedSeam}"
            : $"direct edge matches no approved seam; expected seam: {expectedSeam}";
        return new ArchitectureViolation(contract.Name, contract.Id, sourceName, message, new[] { targetName })
        {
            Payload = new PortBoundaryPayload(sourceRole.Role, sourceRole.Metadata, targetRole.Role,
                targetRole.Metadata, evidenceKind, expectedSeam,
                "Depend on the approved port abstraction or add an explicit reviewed exception.")
        };
    }

    private static ArchitectureViolation BuildUnsupportedEvidenceViolation(ArchitecturePortBoundaryContract contract,
        ArchitectureTypeClassificationResult sourceRole, string sourceName) =>
        new(contract.Name, contract.Id, sourceName,
            "unable to fully enumerate compiled references; a member's type could not be loaded, so the " +
            "direct-edge scan for this source is incomplete and may have missed a forbidden reference",
            new[] { UnsupportedEvidenceReference })
        {
            Payload = new PortBoundaryPayload(sourceRole.Role, sourceRole.Metadata, null, null,
                "unsupported_evidence", string.Empty,
                "Ensure all referenced assemblies are resolvable, or add an explicit reviewed exception if the " +
                "missing dependency is known and out of scope.")
        };

    private void CollectAdapterBindingViolations(ArchitecturePortBoundaryContract contract,
        ArchitectureContractExecutionContext context, List<ArchitectureViolation> violations)
    {
        foreach (ArchitectureAdapterPortBinding binding in contract.AdapterBindings)
            foreach (Type adapter in FindContextSelectorMatchingTypes(binding.Adapter))
            {
                ArchitectureViolation? violation = TryBuildAdapterBindingViolation(contract, binding, context, adapter);
                if (violation != null) violations.Add(violation);
            }
    }

    private ArchitectureViolation? TryBuildAdapterBindingViolation(ArchitecturePortBoundaryContract contract,
        ArchitectureAdapterPortBinding binding, ArchitectureContractExecutionContext context, Type adapter)
    {
        if (!RoleIndex.TryGetRole(adapter, out ArchitectureTypeClassificationResult adapterRole)) return null;
        bool inAllowedContext = binding.AllowedContexts.Count == 0 || binding.AllowedContexts.Any(selector =>
            ArchitectureContextSelectorMatcher.Matches(selector, adapter, RoleIndex, adapterRole));

        // ArchitectureReferenceScanner already wraps type.GetInterfaces() to swallow
        // TypeLoadException/FileNotFoundException from a partially-loadable dependency graph, since a
        // missing optional/transitive assembly is an expected scenario here, not a reason to crash the
        // whole strict/audit run. Reuse that same safe helper and compute the interface set once for
        // both lookups below instead of calling the unguarded GetInterfaces() twice.
        Type[] implementedInterfaces = ArchitectureReferenceScanner.SafeGetInterfaces(adapter);
        Type? implementedExpectedPort = implementedInterfaces
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .FirstOrDefault(@interface => ArchitectureContextSelectorMatcher.Matches(
                binding.ExpectedPort, @interface, RoleIndex, adapterRole));
        bool implementsExpectedPort = implementedExpectedPort != null;
        if (inAllowedContext && implementsExpectedPort) return null;
        string adapterName = ArchitectureTypeNames.SafeFullName(adapter);

        // Prefer an interface RoleIndex actually classifies (e.g. a wrong-but-known port) over an
        // incidental unclassified one (e.g. IDisposable) so the reported mismatch evidence is
        // meaningful rather than whichever interface happens to sort first alphabetically.
        Type? actualPort = implementedExpectedPort ?? implementedInterfaces
            .OrderByDescending(@interface => RoleIndex.TryGetRole(@interface, out ArchitectureTypeClassificationResult r) && r.Role != null)
            .ThenBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .FirstOrDefault();
        ArchitectureTypeClassificationResult? actualPortRole = null;
        if (actualPort != null && RoleIndex.TryGetRole(actualPort, out ArchitectureTypeClassificationResult resolvedPortRole))
        {
            actualPortRole = resolvedPortRole;
        }
        string actualPortName = actualPort == null
            ? "no implemented interface"
            : ArchitectureTypeNames.SafeFullName(actualPort);

        // The suppression key must match what's reported below as ForbiddenReferences (actualPortName),
        // not a static description of the expected port - otherwise a baseline entry keeps suppressing
        // findings after the adapter's actual mismatched/missing interface changes, and a manual ignore
        // copied from the reported forbidden_reference never matches (see PR #306 review).
        if (context.IsIgnored(adapterName, actualPortName)) return null;

        string kind = implementsExpectedPort ? "adapter_context" : "adapter_port_mismatch";
        string detail = implementsExpectedPort
            ? "adapter is outside approved adapter context"
            : $"adapter implements {actualPortName}, not expected port {DescribeContextSelector(binding.ExpectedPort)}";

        return new ArchitectureViolation(contract.Name, contract.Id, adapterName, detail, new[] { actualPortName })
        {
            Payload = new PortBoundaryPayload(
                adapterRole.Role,
                adapterRole.Metadata,
                actualPortRole?.Role,
                actualPortRole?.Metadata,
                kind,
                DescribeContextSelector(binding.ExpectedPort),
                implementsExpectedPort
                    ? "Move the adapter to an approved adapter context or add an explicit reviewed exception."
                    : "Implement the expected port or correct the adapter's reviewed port metadata.")
        };
    }

    private bool MatchesTargetContext(ArchitectureContextMetadataSelector selector, Type target,
        ArchitectureTypeClassificationResult source)
    {
        if (!RoleIndex.TryGetRole(target, out ArchitectureTypeClassificationResult targetRole)) return false;
        foreach ((string key, object expected) in selector.Metadata)
        {
            if (!targetRole.Metadata.TryGetValue(key, out object? actual)) return false;
            if (expected is string text && text == "*") continue;
            if (expected is string textValue && TryGetSourceMetadataKey(textValue, out string sourceKey))
            {
                if (!source.Metadata.TryGetValue(sourceKey, out object? sourceValue)
                    || ArchitectureMetadataValueComparer.ValuesEqual(actual, sourceValue)) return false;
                continue;
            }
            if (expected is System.Collections.IEnumerable values && expected is not string)
            {
                bool anyMatch = values.Cast<object>().Any(value => ArchitectureMetadataValueComparer.ValuesEqual(actual, value));
                if (!anyMatch) return false;
                continue;
            }
            if (!ArchitectureMetadataValueComparer.ValuesEqual(actual, expected)) return false;
        }
        return true;
    }

    private static bool TryGetSourceMetadataKey(string value, out string key)
    {
        const string Prefix = "!{source.metadata.";
        key = string.Empty;
        if (!value.StartsWith(Prefix, StringComparison.Ordinal) || !value.EndsWith('}')) return false;
        string candidate = value[Prefix.Length..^1];
        if (candidate.Length == 0) return false;
        key = candidate;
        return true;
    }
}
