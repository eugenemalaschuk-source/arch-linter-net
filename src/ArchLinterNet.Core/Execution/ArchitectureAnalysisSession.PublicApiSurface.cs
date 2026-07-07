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

            List<(string Signature, string DeclaringType, bool ForbiddenConstant)> entries = new();

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

                entries.Add((entry.Signature, entry.DeclaringTypeName, forbiddenConstant && !undeclared));
            }

            foreach (var (signature, declaringType, forbiddenConstant) in entries
                         .OrderBy(e => e.DeclaringType, StringComparer.Ordinal)
                         .ThenBy(e => e.Signature, StringComparer.Ordinal))
            {
                if (executionContext.IsIgnored(declaringType, signature))
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    declaringType,
                    "public API surface",
                    new[] { signature })
                {
                    UndeclaredApiSignature = signature,
                    ForbiddenPublicConstant = forbiddenConstant ? true : null
                });
            }
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }
}
