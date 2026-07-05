using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckAssemblyDependencyContract(ArchitectureAssemblyDependencyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        Dictionary<string, Assembly> resolvedAssemblies = BuildAssemblyLookup();

        if (!resolvedAssemblies.TryGetValue(contract.Source, out Assembly? sourceAssembly))
        {
            executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
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

            if (executionContext.IsIgnored(contract.Source, forbiddenAssemblyName))
            {
                continue;
            }

            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                contract.Source,
                forbiddenAssemblyName,
                new[] { sourceAssembly.Location }));
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckAssemblyAllowOnlyContract(ArchitectureAssemblyAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        Dictionary<string, Assembly> resolvedAssemblies = BuildAssemblyLookup();

        if (!resolvedAssemblies.TryGetValue(contract.Source, out Assembly? sourceAssembly))
        {
            executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
            return violations;
        }

        HashSet<string> allowedNames = new(contract.Allowed, StringComparer.Ordinal) { contract.Source };

        string[] disallowedReferences = sourceAssembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .Where(name => !string.IsNullOrEmpty(name))
            .Where(resolvedAssemblies.ContainsKey)
            .Where(name => !allowedNames.Contains(name))
            .Where(name => !executionContext.IsIgnored(contract.Source, name))
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

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private Dictionary<string, Assembly> BuildAssemblyLookup()
    {
        return Context.TargetAssemblies
            .GroupBy(assembly => assembly.GetName().Name ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }
}
