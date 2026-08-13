using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution.Checkers;

// Transitive assembly-reference-path resolution is not implemented for any assembly-scoped family,
// so a contract asking for it must fail loudly rather than silently degrade to direct-only results.
internal static class AssemblyDependencyDepthGuard
{
    public static void RequireDirect(string contractName, DependencyDepthMode dependencyDepth)
    {
        if (dependencyDepth != DependencyDepthMode.Direct)
        {
            throw new InvalidOperationException(
                $"Assembly contract '{contractName}' declares 'dependency_depth: transitive', which is not " +
                "supported yet. Assembly dependency and assembly allow-only contracts only support " +
                "'dependency_depth: direct' (the default) in this release; transitive assembly-reference-path " +
                "resolution is a planned follow-up.");
        }
    }
}

// Both assembly-reference families: "assembly_dependency" and "assembly_allow_only".
internal static class AssemblyDependencyChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureAssemblyDependencyContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        Dictionary<string, Assembly> resolvedAssemblies = context.BuildAssemblyLookup();

        if (!resolvedAssemblies.TryGetValue(contract.Source, out Assembly? sourceAssembly))
        {
            return violations;
        }

        HashSet<string> directReferences = new(
            sourceAssembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty),
            StringComparer.Ordinal);

        foreach (string forbiddenAssemblyName in contract.Forbidden)
        {
            if (string.Equals(contract.Source, forbiddenAssemblyName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!directReferences.Contains(forbiddenAssemblyName))
            {
                continue;
            }

            if (executionContext.IsIgnored(
                    contract.Source,
                    forbiddenAssemblyName,
                    sourceAssembly: contract.Source,
                    targetAssembly: forbiddenAssemblyName,
                    targetType: forbiddenAssemblyName,
                    targetMember: forbiddenAssemblyName))
            {
                continue;
            }

            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                contract.Source,
                forbiddenAssemblyName,
                new[] { $"{contract.Source} -> {forbiddenAssemblyName}" }));
        }

        return violations;
    }

    public static List<ArchitectureViolation> CheckAllowOnly(
        ArchitectureAssemblyAllowOnlyContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        Dictionary<string, Assembly> resolvedAssemblies = context.BuildAssemblyLookup();

        if (!resolvedAssemblies.TryGetValue(contract.Source, out Assembly? sourceAssembly))
        {
            return violations;
        }

        HashSet<string> allowedNames = new(contract.Allowed, StringComparer.Ordinal) { contract.Source };

        string[] disallowedReferences = sourceAssembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .Where(name => !string.IsNullOrEmpty(name))
            .Where(resolvedAssemblies.ContainsKey)
            .Where(name => !allowedNames.Contains(name))
            .Where(name => !executionContext.IsIgnored(
                contract.Source,
                name,
                sourceAssembly: contract.Source,
                targetAssembly: name,
                targetType: name,
                targetMember: name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (disallowedReferences.Length > 0)
        {
            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                contract.Source,
                "outside allowed assemblies",
                disallowedReferences));
        }

        return violations;
    }
}
