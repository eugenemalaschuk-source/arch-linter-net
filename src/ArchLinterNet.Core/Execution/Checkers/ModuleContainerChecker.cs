using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution.Checkers;

internal static class ModuleContainerChecker
{
    private static readonly IReadOnlySet<string> _cliCommandSegments = new HashSet<string>(StringComparer.Ordinal)
    {
        "EntryPoint", "Application", "Abstractions", "Models", "Exceptions",
    };

    private static readonly IReadOnlySet<string> _genericModuleNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "Common", "Shared", "Utils",
    };

    public static List<ArchitectureViolation> Check(
        ArchitectureModuleContainerContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        ArchitectureModuleContainerDiscovery.Inventory inventory = ArchitectureModuleContainerDiscovery.Discover(
            context.AnalysisContext.TargetAssemblies, contract.Container);
        var violations = new List<ArchitectureViolation>();

        CheckStructure(contract, inventory, context, executionContext, violations);
        CheckDependencies(contract, inventory, context, executionContext, violations);
        return violations;
    }

    private static void CheckStructure(
        ArchitectureModuleContainerContract contract,
        ArchitectureModuleContainerDiscovery.Inventory inventory,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        foreach ((Type type, ArchitectureModuleNamespaceMembership location) in inventory.Locations
                     .OrderBy(candidate => ArchitectureTypeNames.SafeFullName(candidate.Key), StringComparer.Ordinal))
        {
            string typeName = ArchitectureTypeNames.SafeFullName(type);
            if (location.IsContainerRoot && !contract.AllowedContainerRootTypes.Contains(typeName, StringComparer.Ordinal))
            {
                AddStructuralViolation(contract, type, "<container-root>", context, executionContext, violations);
                continue;
            }

            if (location.ModuleName == null)
            {
                continue;
            }

            if (_genericModuleNames.Contains(location.ModuleName))
            {
                AddStructuralViolation(contract, type, $"<generic-module:{location.ModuleName}>", context, executionContext, violations);
                continue;
            }

            if (location.Segment == null && !contract.AllowedModuleRootTypes.Contains(typeName, StringComparer.Ordinal))
            {
                AddStructuralViolation(contract, type, $"<module-root:{location.ModuleName}>", context, executionContext, violations);
                continue;
            }

            if (location.Segment != null && !_cliCommandSegments.Contains(location.Segment))
            {
                AddStructuralViolation(contract, type, $"<undeclared-segment:{location.Segment}>", context, executionContext, violations);
            }
        }
    }

    private static void CheckDependencies(
        ArchitectureModuleContainerContract contract,
        ArchitectureModuleContainerDiscovery.Inventory inventory,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        foreach ((Type sourceType, ArchitectureModuleNamespaceMembership source) in inventory.Locations
                     .Where(candidate => candidate.Value.ModuleName != null)
                     .OrderBy(candidate => ArchitectureTypeNames.SafeFullName(candidate.Key), StringComparer.Ordinal))
        {
            foreach (Type target in ArchitectureReferenceScanner.GetReferencedTypes(sourceType)
                         .Distinct()
                         .OrderBy(ArchitectureTypeNames.SafeFullName, StringComparer.Ordinal))
            {
                if (!inventory.Locations.TryGetValue(target, out ArchitectureModuleNamespaceMembership? targetLocation)
                    || targetLocation.ModuleName == null)
                {
                    continue;
                }

                if (!string.Equals(source.ModuleName, targetLocation.ModuleName, StringComparison.Ordinal))
                {
                    AddDependencyViolation(
                        contract, sourceType, target, $"{contract.Container}.{targetLocation.ModuleName}",
                        context, executionContext, violations);
                    continue;
                }

                if (source.Segment != null
                    && targetLocation.Segment != null
                    && !IsAllowedCliCommandDependency(source.Segment, targetLocation.Segment))
                {
                    AddDependencyViolation(
                        contract, sourceType, target,
                        $"{contract.Container}.{source.ModuleName}.{targetLocation.Segment}",
                        context, executionContext, violations);
                }
            }
        }
    }

    private static bool IsAllowedCliCommandDependency(string sourceSegment, string targetSegment)
    {
        if (string.Equals(sourceSegment, targetSegment, StringComparison.Ordinal))
        {
            return true;
        }

        return sourceSegment switch
        {
            "EntryPoint" => targetSegment is "Application" or "Abstractions" or "Models" or "Exceptions",
            "Application" => targetSegment is "Abstractions" or "Models" or "Exceptions",
            "Abstractions" => targetSegment == "Models",
            _ => false,
        };
    }

    private static void AddStructuralViolation(
        ArchitectureModuleContainerContract contract,
        Type source,
        string marker,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        AddViolation(contract, source, target: null, marker, context, executionContext, violations);
    }

    private static void AddDependencyViolation(
        ArchitectureModuleContainerContract contract,
        Type source,
        Type target,
        string forbiddenNamespace,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        AddViolation(contract, source, target, forbiddenNamespace, context, executionContext, violations);
    }

    private static void AddViolation(
        ArchitectureModuleContainerContract contract,
        Type source,
        Type? target,
        string forbiddenNamespace,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext,
        List<ArchitectureViolation> violations)
    {
        string sourceType = ArchitectureTypeNames.SafeFullName(source);
        string targetType = target == null ? forbiddenNamespace : ArchitectureTypeNames.SafeFullName(target);
        string sourceAssembly = ArchitectureTypeNames.SafeAssemblyName(source) ?? string.Empty;
        string? targetAssembly = target == null ? null : ArchitectureTypeNames.SafeAssemblyName(target);

        if (executionContext.IsIgnored(
                sourceType,
                targetType,
                sourceAssembly,
                targetAssembly,
                targetType,
                targetType))
        {
            return;
        }

        violations.Add(new ArchitectureViolation(
            contract.Name,
            contract.Id,
            sourceType,
            forbiddenNamespace,
            new[] { targetType }));
    }
}
