using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckPortBoundaryContract(ArchitecturePortBoundaryContract contract)
    {
        if (!IsContractSelected(contract.Id)) return new List<ArchitectureViolation>();
        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext context = CreateExecutionContext(contract, contract.IgnoredViolations);
        foreach (Type source in FindContextSelectorMatchingTypes(contract.Source))
        {
            if (!RoleIndex.TryGetRole(source, out ArchitectureTypeClassificationResult sourceRole)) continue;
            string sourceName = ArchitectureTypeNames.SafeFullName(source);
            foreach (Type target in ArchitectureReferenceScanner.GetReferencedTypes(source).Distinct())
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
        return new ArchitectureViolation(contract.Name, contract.Id, sourceName,
            $"forbidden direct edge; expected seam: {expectedSeam}", new[] { targetName })
        {
            Payload = new PortBoundaryPayload(sourceRole.Role, sourceRole.Metadata, targetRole.Role,
                targetRole.Metadata, "direct_reference", expectedSeam,
                "Depend on the approved port abstraction or add an explicit reviewed exception.")
        };
    }

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
        Type? implementedExpectedPort = adapter.GetInterfaces()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .FirstOrDefault(@interface => ArchitectureContextSelectorMatcher.Matches(
                binding.ExpectedPort, @interface, RoleIndex, adapterRole));
        bool implementsExpectedPort = implementedExpectedPort != null;
        if (inAllowedContext && implementsExpectedPort) return null;
        string adapterName = ArchitectureTypeNames.SafeFullName(adapter);
        if (context.IsIgnored(adapterName, DescribeContextSelector(binding.ExpectedPort))) return null;

        Type? actualPort = implementedExpectedPort ?? adapter.GetInterfaces()
            .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal)
            .FirstOrDefault();
        ArchitectureTypeClassificationResult? actualPortRole = null;
        if (actualPort != null && RoleIndex.TryGetRole(actualPort, out ArchitectureTypeClassificationResult resolvedPortRole))
        {
            actualPortRole = resolvedPortRole;
        }
        string actualPortName = actualPort == null
            ? "no implemented interface"
            : ArchitectureTypeNames.SafeFullName(actualPort);
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
