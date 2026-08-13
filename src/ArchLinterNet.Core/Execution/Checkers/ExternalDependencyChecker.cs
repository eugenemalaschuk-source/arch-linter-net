using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

// Both external-dependency families: "external" (forbidden groups) and "external_allow_only".
internal static class ExternalDependencyChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureExternalDependencyContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, contract.Source);
        Type[] sourceTypes = context.FindTypesInLayer(sourceLayer);
        List<ArchitectureViolation> violations = new();

        // One scanner for every forbidden group of this contract: each group walks the same source
        // types over the same IL, so sharing the instance shares its IL-token resolution cache
        // instead of re-resolving every token once per group (issue #419). Group matching itself
        // stays per call, so the shared instance cannot leak one group's verdicts into another.
        ArchitectureExternalDependencyIlScanner ilScanner = new();

        foreach (string externalGroupName in contract.Forbidden)
        {
            if (!context.Document.ExternalDependencies.TryGetValue(
                    externalGroupName, out ArchitectureExternalDependencyGroup? externalGroup))
            {
                continue;
            }

            violations.AddRange(ArchitectureExternalDependencyViolationFinder.FindViolations(
                externalGroupName,
                sourceTypes,
                externalGroup,
                executionContext));

            violations.AddRange(ilScanner.FindMethodBodyViolations(
                sourceTypes,
                externalGroupName,
                externalGroup,
                executionContext,
                context.AnalysisContext.CancellationToken));
        }

        return violations;
    }

    public static List<ArchitectureViolation> CheckAllowOnly(
        ArchitectureExternalAllowOnlyContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        ArchitectureLayer sourceLayer =
            ArchitectureLayerResolver.ResolveLayer(context.Document, contract.Name, contract.Source);
        Type[] sourceTypes = context.FindTypesInLayer(sourceLayer);
        List<ArchitectureViolation> violations = new();

        var allowedGroups = contract.Allowed.ToHashSet(StringComparer.Ordinal);
        IEnumerable<string> disallowedGroups = context.Document.ExternalDependencies.Keys
            .Where(name => !allowedGroups.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal);

        string allowedGroupsSuffix = $" (allowed groups: [{string.Join(", ", contract.Allowed)}])";

        foreach (string externalGroupName in disallowedGroups)
        {
            ArchitectureExternalDependencyGroup externalGroup = context.Document.ExternalDependencies[externalGroupName];

            foreach (ArchitectureViolation violation in ArchitectureExternalDependencyViolationFinder.FindViolations(
                         externalGroupName, sourceTypes, externalGroup, executionContext, contract.AllowedTypes))
            {
                violations.Add(violation with { ForbiddenNamespace = violation.ForbiddenNamespace + allowedGroupsSuffix });
            }
        }

        return violations;
    }
}
