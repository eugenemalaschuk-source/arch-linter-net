using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution.Checkers;

internal sealed class AssemblyIndependenceChecker
{
    private AssemblyIndependenceChecker()
    {
    }

    public static List<ArchitectureViolation> Check(
        ArchitectureAssemblyIndependenceContract contract,
        IEnumerable<Assembly> targetAssemblies,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        Dictionary<string, Assembly> resolvedAssemblies = targetAssemblies
            .GroupBy(assembly => assembly.GetName().Name ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (string sourceAssemblyName in contract.Assemblies)
        {
            if (!resolvedAssemblies.TryGetValue(sourceAssemblyName, out Assembly? sourceAssembly))
            {
                continue;
            }

            HashSet<string> directReferences = new(
                sourceAssembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty),
                StringComparer.Ordinal);

            foreach (string forbiddenAssemblyName in contract.Assemblies)
            {
                if (string.Equals(sourceAssemblyName, forbiddenAssemblyName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!directReferences.Contains(forbiddenAssemblyName))
                {
                    continue;
                }

                if (executionContext.IsIgnored(sourceAssemblyName, forbiddenAssemblyName))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    sourceAssemblyName,
                    forbiddenAssemblyName,
                    new[] { sourceAssembly.Location }));
            }
        }

        return violations;
    }
}
