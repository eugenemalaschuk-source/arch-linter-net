using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    private readonly Dictionary<string, ArchitectureFrameworkReferenceEvaluationResult> _frameworkEvaluationCache =
        new(StringComparer.Ordinal);

    public List<ArchitectureViolation> CheckFrameworkDependencyContract(ArchitectureFrameworkReferenceContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        IReadOnlyList<ArchitectureDiscoveredFrameworkReference> references = ResolveFrameworkReferences(contract.Source);

        foreach (string frameworkGroupName in contract.Forbidden)
        {
            if (!Document.FrameworkReferences.TryGetValue(
                    frameworkGroupName, out ArchitectureFrameworkReferenceGroup? frameworkGroup))
            {
                continue;
            }

            ArchitectureDiscoveredFrameworkReference[] matched = references
                .Where(reference => ArchitectureFrameworkReferenceResolver.MatchesGroup(frameworkGroup, reference.FrameworkName))
                .Where(reference => !executionContext.IsIgnored(
                    contract.Source, reference.FrameworkName, targetMember: FormatFrameworkReference(reference)))
                .ToArray();

            string[] forbiddenReferences = matched
                .Select(FormatFrameworkReference)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(reference => reference, StringComparer.Ordinal)
                .ToArray();

            if (forbiddenReferences.Length == 0)
            {
                continue;
            }

            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                contract.Source,
                $"framework group '{frameworkGroupName}'",
                forbiddenReferences)
            {
                Payload = new FrameworkReferencePayload(frameworkGroupName, BuildEvidence(matched))
            });
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckFrameworkAllowOnlyContract(ArchitectureFrameworkReferenceAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        IReadOnlyList<ArchitectureDiscoveredFrameworkReference> references = ResolveFrameworkReferences(contract.Source);

        List<ArchitectureFrameworkReferenceGroup> allowedGroups = contract.Allowed
            .Select(groupName => Document.FrameworkReferences.TryGetValue(
                groupName, out ArchitectureFrameworkReferenceGroup? group) ? group : null)
            .Where(group => group != null)
            .Select(group => group!)
            .ToList();

        ArchitectureDiscoveredFrameworkReference[] disallowed = references
            .Where(reference => !allowedGroups.Any(group =>
                ArchitectureFrameworkReferenceResolver.MatchesGroup(group, reference.FrameworkName)))
            .Where(reference => !executionContext.IsIgnored(
                contract.Source, reference.FrameworkName, targetMember: FormatFrameworkReference(reference)))
            .ToArray();

        string[] disallowedReferences = disallowed
            .Select(FormatFrameworkReference)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();

        if (disallowedReferences.Length > 0)
        {
            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                contract.Source,
                "outside allowed framework groups",
                disallowedReferences)
            {
                Payload = new FrameworkReferenceAllowOnlyPayload(contract.Allowed.ToArray(), BuildEvidence(disallowed))
            });
        }

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
    private IReadOnlyList<ArchitectureDiscoveredFrameworkReference> ResolveFrameworkReferences(string sourceAssemblyName)
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

    private ArchitectureFrameworkReferenceEvaluationResult EvaluateFrameworkReferences(ArchitectureDiscoveredProject owningProject)
    {
        string projectAbsolutePath = Path.GetFullPath(Path.Combine(Context.RepositoryRoot, owningProject.Path));

        if (_frameworkEvaluationCache.TryGetValue(projectAbsolutePath, out ArchitectureFrameworkReferenceEvaluationResult? cached))
        {
            return cached;
        }

        ArchitectureFrameworkReferenceEvaluationResult result =
            new ArchitectureFrameworkReferenceEvaluator().Evaluate(projectAbsolutePath);
        _frameworkEvaluationCache[projectAbsolutePath] = result;
        return result;
    }

    // Best-effort, display-only Condition lookup: matches the evaluated reference against the raw,
    // unevaluated declarations captured by the lightweight XML parser during generic project
    // discovery. Prefers a raw declaration whose condition text mentions the reference's real
    // evaluated TargetFramework; falls back to the first declaration with a matching name; returns
    // null when none is found. This is cosmetic evidence only - it is never authoritative and never
    // part of violation identity.
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

    private static IReadOnlyCollection<FrameworkReferenceEvidence> BuildEvidence(
        IEnumerable<ArchitectureDiscoveredFrameworkReference> references)
    {
        return references
            .Select(reference => new FrameworkReferenceEvidence(
                reference.FrameworkName, reference.TargetFramework, reference.Explicit, reference.SourcePath))
            .ToArray();
    }

    private static string FormatFrameworkReference(ArchitectureDiscoveredFrameworkReference reference)
    {
        return $"{reference.FrameworkName} ({reference.TargetFramework})";
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
