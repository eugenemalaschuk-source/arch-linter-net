using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Execution.Checkers;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

// Session-side entry points for the framework-reference families, plus the MSBuild framework
// evaluation this session caches. The evaluation stays here rather than moving into
// FrameworkReferenceChecker because it is run-scoped fact resolution shared with
// CheckConfiguration's fail-closed evaluation-failure surfacing, not family checking.
public sealed partial class ArchitectureAnalysisSession
{
    private readonly Dictionary<string, ArchitectureFrameworkReferenceEvaluationResult> _frameworkEvaluationCache =
        new(StringComparer.Ordinal);

    public List<ArchitectureViolation> CheckFrameworkDependencyContract(ArchitectureFrameworkReferenceContract contract)
    {
        if (!IsContractSelected(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations = FrameworkReferenceChecker.Check(contract, CheckerContext, executionContext);
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckFrameworkAllowOnlyContract(ArchitectureFrameworkReferenceAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract))
        {
            return new List<ArchitectureViolation>();
        }

        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);
        List<ArchitectureViolation> violations =
            FrameworkReferenceChecker.CheckAllowOnly(contract, CheckerContext, executionContext);
        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    // Resolves the real, MSBuild-evaluated FrameworkReference declarations for the discovered project
    // whose AssemblyName matches `sourceAssemblyName`. Evaluation is cached per absolute project path
    // for the lifetime of this session, so multiple framework contracts sharing a source project only
    // trigger one real Buildalyzer design-time build. Evaluation failures are recorded for
    // CheckConfiguration to surface as fail-closed configuration violations; the contract check itself
    // simply sees no references for a project it could not evaluate, never a crash or a silent pass
    // that fabricates data.
    internal ArchitectureDiscoveredFrameworkReference[] ResolveFrameworkReferences(string sourceAssemblyName)
    {
        ArchitectureDiscoveredProject? owningProject = FindDiscoveredProject(sourceAssemblyName);

        if (owningProject == null)
        {
            return Array.Empty<ArchitectureDiscoveredFrameworkReference>();
        }

        ArchitectureFrameworkReferenceEvaluationResult result = EvaluateFrameworkReferences(owningProject);

        if (!result.Succeeded)
        {
            return Array.Empty<ArchitectureDiscoveredFrameworkReference>();
        }

        return result.References
            .Select(reference => reference with { Condition = FindBestEffortCondition(owningProject, reference) })
            .ToArray();
    }

    private ArchitectureDiscoveredProject? FindDiscoveredProject(string assemblyName)
    {
        return Context.ProjectDiscovery?.DiscoveredProjects
            .FirstOrDefault(project => string.Equals(project.AssemblyName, assemblyName, StringComparison.Ordinal));
    }

    // Matches analysis.configuration, defaulting to "Debug" exactly like project discovery's own
    // output-path resolution (ArchitectureProjectDiscoveryService.TryResolveOutput) - so a policy
    // targeting Release evaluates Release-conditioned FrameworkReference declarations, and a Debug
    // baseline entry does not silently freeze a distinct Release occurrence of the same
    // FrameworkName+TargetFramework, or vice versa.
    internal string ResolvedBuildConfiguration =>
        string.IsNullOrWhiteSpace(Document.Analysis.Configuration) ? "Debug" : Document.Analysis.Configuration;

    private ArchitectureFrameworkReferenceEvaluationResult EvaluateFrameworkReferences(ArchitectureDiscoveredProject owningProject)
    {
        string projectAbsolutePath = Path.GetFullPath(Path.Combine(Context.RepositoryRoot, owningProject.Path));
        string configuration = ResolvedBuildConfiguration;
        string cacheKey = $"{projectAbsolutePath}|{configuration}";

        if (_frameworkEvaluationCache.TryGetValue(cacheKey, out ArchitectureFrameworkReferenceEvaluationResult? cached))
        {
            return cached;
        }

        ArchitectureFrameworkReferenceEvaluationResult result =
            new ArchitectureFrameworkReferenceEvaluator().Evaluate(projectAbsolutePath, configuration);
        _frameworkEvaluationCache[cacheKey] = result;
        return result;
    }

    // Best-effort, display-only Condition lookup: matches the evaluated reference against the raw,
    // unevaluated declarations captured by the lightweight XML parser during generic project
    // discovery. Prefers a raw declaration whose condition text mentions the reference's real
    // evaluated TargetFramework; falls back to the first declaration with a matching name; returns
    // null when none is found. This is cosmetic evidence only.
    //
    // Two simultaneously active declarations of the SAME framework name for the SAME evaluated target
    // framework cannot occur in a project that MSBuild actually builds: the .NET SDK itself rejects
    // this ("Multiple FrameworkReference items for '<name>' were included in the project.", confirmed
    // empirically) as a hard build error, which ArchitectureFrameworkReferenceEvaluator surfaces as an
    // evaluation Failure - the fail-closed configuration violation, not a silent duplicate identity, is
    // what a policy author actually sees for that project. FrameworkName+TargetFramework is therefore
    // already a genuinely unique key for any project that builds; this lookup only needs to disambiguate
    // which single raw declaration to display when a name appears once.
    private static string? FindBestEffortCondition(
        ArchitectureDiscoveredProject owningProject, ArchitectureDiscoveredFrameworkReference reference)
    {
        List<ArchitectureDiscoveredFrameworkReference> candidates = owningProject.FrameworkReferences
            .Where(raw => string.Equals(raw.FrameworkName, reference.FrameworkName, StringComparison.Ordinal))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        ArchitectureDiscoveredFrameworkReference? tfmMatch = candidates.FirstOrDefault(raw =>
            raw.Condition != null && raw.Condition.Contains(reference.TargetFramework, StringComparison.OrdinalIgnoreCase));

        return (tfmMatch ?? candidates[0]).Condition;
    }

    // Fail-closed surfacing: for every distinct (contract, source project) pair that a framework
    // dependency/allow-only contract references, evaluates (via the same session-cached evaluator
    // used by the contract checks) and reports one configuration violation per project/TFM that
    // MSBuild could not evaluate. A project with no discovered project metadata at all is already
    // reported by AddFrameworkMetadataViolations and is skipped here to avoid duplicate noise.
    private void AddFrameworkEvaluationFailureViolations(
        List<ArchitectureViolation> violations, ArchitectureConfigurationReferenceCollector collector)
    {
        foreach ((IArchitectureContract contract, string source) in collector.FrameworkContractSources
                     .DistinctBy(entry => (entry.Contract, entry.Source)))
        {
            ArchitectureDiscoveredProject? owningProject = FindDiscoveredProject(source);

            if (owningProject == null)
            {
                continue;
            }

            ArchitectureFrameworkReferenceEvaluationResult result = EvaluateFrameworkReferences(owningProject);

            if (result.Succeeded)
            {
                continue;
            }

            foreach (ArchitectureFrameworkReferenceEvaluationFailure failure in result.Failures)
            {
                string tfmDescription = string.IsNullOrEmpty(failure.TargetFramework)
                    ? "the project"
                    : $"target framework '{failure.TargetFramework}'";

                var violation = new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    source,
                    "framework reference evaluation failed",
                    new[]
                    {
                        $"Contract '{contract.Name}' declares source '{source}', but MSBuild evaluation of {tfmDescription} " +
                        $"in project '{failure.ProjectPath}' failed: {failure.Reason} " +
                        "Framework dependency/allow-only contracts require a project that can be evaluated by MSBuild " +
                        "for every configured target framework; without it, this contract cannot be trusted to report violations."
                    });
                violations.Add(Document.Provenance.Enrich(violation, contract));
            }
        }
    }
}
