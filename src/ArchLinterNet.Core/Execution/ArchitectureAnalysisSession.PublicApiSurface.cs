using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckPublicApiSurfaceContract(ArchitecturePublicApiSurfaceContract contract)
    {
        if (!IsContractSelected(contract.Id) || IsDanglingButCoveredByRuleInputCoverage(contract))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        HashSet<string> declaredApi = new(contract.DeclaredApi, StringComparer.Ordinal);
        HashSet<string> allowedPublicConstants = new(contract.AllowedPublicConstants, StringComparer.Ordinal);
        Dictionary<string, Assembly> resolvedAssemblies = BuildAssemblyLookup();

        foreach (string assemblyName in contract.Assemblies)
        {
            if (!resolvedAssemblies.TryGetValue(assemblyName, out Assembly? targetAssembly))
            {
                continue;
            }

            List<(ArchitectureExportedApiEntry Entry, bool ForbiddenConstant)> violatingEntries = new();

            foreach (ArchitectureExportedApiEntry entry in ArchitecturePublicApiSurfaceScanner.GetExportedSurface(targetAssembly))
            {
                bool undeclared = !declaredApi.Contains(entry.Signature);
                bool forbiddenConstant = contract.ForbidPublicConstantsUnlessDeclared
                    && entry.IsConst
                    && entry.ConstQualifiedName != null
                    && !allowedPublicConstants.Contains(entry.ConstQualifiedName);

                if (!undeclared && !forbiddenConstant)
                {
                    continue;
                }

                violatingEntries.Add((entry, forbiddenConstant));
            }

            foreach (var (entry, forbiddenConstant) in violatingEntries
                         .OrderBy(v => v.Entry.DeclaringTypeName, StringComparer.Ordinal)
                         .ThenBy(v => v.Entry.Signature, StringComparer.Ordinal))
            {
                if (executionContext.IsIgnored(entry.DeclaringTypeName, entry.Signature))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    entry.DeclaringTypeName,
                    "public API surface",
                    new[] { entry.Signature })
                {
                    UndeclaredApiSignature = entry.Signature,
                    ForbiddenPublicConstant = forbiddenConstant ? true : null,
                    ApiAssemblyName = entry.AssemblyName,
                    ApiVisibility = entry.Visibility
                });
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }
}
