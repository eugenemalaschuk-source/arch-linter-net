using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution.Checkers;
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

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        Dictionary<string, Assembly> resolvedAssemblies = BuildAssemblyLookup();
        List<ArchitectureViolation> violations = PublicApiSurfaceChecker.Check(contract, resolvedAssemblies, executionContext);
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    // Captures a contract's exported surface without evaluating it against any declaration. This is
    // the read side the public-api capture/diff/update/migrate workflow builds on; contract
    // selection and ignore matching deliberately do not apply, because a snapshot describes what
    // the assemblies actually export, not what the policy currently tolerates.
    public IReadOnlyList<PublicApiSnapshotEntry> CapturePublicApiSurface(
        ArchitecturePublicApiSurfaceContract contract,
        out IReadOnlyList<string> missingAssemblies)
    {
        Dictionary<string, Assembly> resolvedAssemblies = BuildAssemblyLookup();
        List<PublicApiSnapshotEntry> entries = new();
        List<string> missing = new();

        foreach (string assemblyName in contract.Assemblies.Distinct(StringComparer.Ordinal)
                     .OrderBy(name => name, StringComparer.Ordinal))
        {
            if (!resolvedAssemblies.TryGetValue(assemblyName, out Assembly? targetAssembly))
            {
                missing.Add(assemblyName);
                continue;
            }

            // A snapshot records the exact grammar (base signature plus the detail suffix), because
            // an identity-only capture cannot tell a changed constant value, accessor shape, or
            // ref/out direction from no change at all.
            entries.AddRange(ArchitecturePublicApiSurfaceScanner.GetExportedSurface(targetAssembly)
                .Select(entry => new PublicApiSnapshotEntry(entry.AssemblyName, entry.ExactSignature)));
        }

        missingAssemblies = missing;
        return entries;
    }
}
