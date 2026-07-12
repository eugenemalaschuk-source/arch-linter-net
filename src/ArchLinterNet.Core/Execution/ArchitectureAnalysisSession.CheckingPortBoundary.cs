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
                if (!MatchesTargetContext(contract.TargetContext, target, sourceRole)
                    || IsExcludedFromContextMatch(target, contract.Exclude, sourceRole)) continue;
                bool allowed = contract.AllowedSeams.Any(s => ArchitectureContextSelectorMatcher.Matches(s, target, RoleIndex, sourceRole));
                ArchitectureContextSelector? forbidden = contract.Forbidden.FirstOrDefault(s => ArchitectureContextSelectorMatcher.Matches(s, target, RoleIndex, sourceRole));
                if (allowed || forbidden is null) continue;
                string targetName = ArchitectureTypeNames.SafeFullName(target);
                if (string.IsNullOrEmpty(targetName) || context.IsIgnored(sourceName, targetName)) continue;
                RoleIndex.TryGetRole(target, out ArchitectureTypeClassificationResult targetRole);
                violations.Add(new ArchitectureViolation(contract.Name, contract.Id, sourceName,
                    $"forbidden direct edge; expected seam: {string.Join(" or ", contract.AllowedSeams.Select(DescribeContextSelector))}", new[] { targetName })
                {
                    Payload = new PortBoundaryPayload(sourceRole.Role, sourceRole.Metadata, targetRole.Role,
                        targetRole.Metadata, "direct_reference", string.Join(" or ", contract.AllowedSeams.Select(DescribeContextSelector)))
                });
            }
        }
        CollectAdapterBindingViolations(contract, context, violations);
        context.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private void CollectAdapterBindingViolations(ArchitecturePortBoundaryContract contract,
        ArchitectureContractExecutionContext context, List<ArchitectureViolation> violations)
    {
        foreach (ArchitectureAdapterPortBinding binding in contract.AdapterBindings)
            foreach (Type adapter in FindContextSelectorMatchingTypes(binding.Adapter))
            {
                if (!RoleIndex.TryGetRole(adapter, out ArchitectureTypeClassificationResult adapterRole)) continue;
                bool inAllowedContext = binding.AllowedContexts.Count == 0 || binding.AllowedContexts.Any(selector =>
                    ArchitectureContextSelectorMatcher.Matches(selector, adapter, RoleIndex, adapterRole));
                bool implementsExpectedPort = adapter.GetInterfaces().Any(@interface =>
                    ArchitectureContextSelectorMatcher.Matches(binding.ExpectedPort, @interface, RoleIndex, adapterRole));
                if (inAllowedContext && implementsExpectedPort) continue;
                string adapterName = ArchitectureTypeNames.SafeFullName(adapter);
                if (context.IsIgnored(adapterName, DescribeContextSelector(binding.ExpectedPort))) continue;
                string kind = implementsExpectedPort ? "adapter_context" : "adapter_port_mismatch";
                string detail = implementsExpectedPort
                    ? "adapter is outside approved adapter context"
                    : $"adapter does not implement expected port {DescribeContextSelector(binding.ExpectedPort)}";
                violations.Add(
                    new ArchitectureViolation(
                        contract.Name,
                        contract.Id,
                        adapterName,
                        detail,
                        new[] { DescribeContextSelector(binding.ExpectedPort) })
                    {
                        Payload = new PortBoundaryPayload(
                            adapterRole.Role,
                            adapterRole.Metadata,
                            null,
                            null,
                            kind,
                            DescribeContextSelector(binding.ExpectedPort))
                    });
            }
    }

    private bool MatchesTargetContext(ArchitectureContextMetadataSelector selector, Type target,
        ArchitectureTypeClassificationResult source)
    {
        if (!RoleIndex.TryGetRole(target, out ArchitectureTypeClassificationResult targetRole)) return false;
        foreach ((string key, object expected) in selector.Metadata)
        {
            if (!targetRole.Metadata.TryGetValue(key, out object? actual)) return false;
            if (expected is string text && text == "*") continue;
            if (expected is string textValue && textValue == $"!{{source.metadata.{key}}}")
            {
                if (!source.Metadata.TryGetValue(key, out object? sourceValue) || Equals(actual, sourceValue)) return false;
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
}
