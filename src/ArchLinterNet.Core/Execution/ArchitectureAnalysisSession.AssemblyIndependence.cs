using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckAssemblyIndependenceContract(ArchitectureAssemblyIndependenceContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        Dictionary<string, Assembly> resolvedAssemblies = Context.TargetAssemblies
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

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }
}
