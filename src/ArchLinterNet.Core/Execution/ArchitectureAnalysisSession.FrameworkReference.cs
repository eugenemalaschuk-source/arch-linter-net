using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckFrameworkDependencyContract(ArchitectureFrameworkReferenceContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        Dictionary<string, IReadOnlyList<ArchitectureDiscoveredFrameworkReference>> frameworksByProject =
            BuildFrameworkReferenceLookup();

        if (!frameworksByProject.TryGetValue(
                contract.Source, out IReadOnlyList<ArchitectureDiscoveredFrameworkReference>? references))
        {
            executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
            return violations;
        }

        foreach (string frameworkGroupName in contract.Forbidden)
        {
            if (!Document.FrameworkReferences.TryGetValue(
                    frameworkGroupName, out ArchitectureFrameworkReferenceGroup? frameworkGroup))
            {
                continue;
            }

            string[] forbiddenReferences = references
                .Where(reference => ArchitectureFrameworkReferenceResolver.MatchesGroup(frameworkGroup, reference.FrameworkName))
                .Where(reference => !executionContext.IsIgnored(contract.Source, reference.FrameworkName))
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
                Payload = new FrameworkReferencePayload(frameworkGroupName)
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

        Dictionary<string, IReadOnlyList<ArchitectureDiscoveredFrameworkReference>> frameworksByProject =
            BuildFrameworkReferenceLookup();

        if (!frameworksByProject.TryGetValue(
                contract.Source, out IReadOnlyList<ArchitectureDiscoveredFrameworkReference>? references))
        {
            executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
            return violations;
        }

        List<ArchitectureFrameworkReferenceGroup> allowedGroups = contract.Allowed
            .Select(groupName => Document.FrameworkReferences.TryGetValue(
                groupName, out ArchitectureFrameworkReferenceGroup? group) ? group : null)
            .Where(group => group != null)
            .Select(group => group!)
            .ToList();

        string[] disallowedReferences = references
            .Where(reference => !allowedGroups.Any(group =>
                ArchitectureFrameworkReferenceResolver.MatchesGroup(group, reference.FrameworkName)))
            .Where(reference => !executionContext.IsIgnored(contract.Source, reference.FrameworkName))
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
                Payload = new FrameworkReferenceAllowOnlyPayload(contract.Allowed.ToArray())
            });
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private Dictionary<string, IReadOnlyList<ArchitectureDiscoveredFrameworkReference>> BuildFrameworkReferenceLookup()
    {
        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects =
            Context.ProjectDiscovery?.DiscoveredProjects ?? Array.Empty<ArchitectureDiscoveredProject>();

        return discoveredProjects
            .GroupBy(project => project.AssemblyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ArchitectureDiscoveredFrameworkReference>)group.First().FrameworkReferences,
                StringComparer.Ordinal);
    }

    private static string FormatFrameworkReference(ArchitectureDiscoveredFrameworkReference reference)
    {
        return string.IsNullOrWhiteSpace(reference.Condition)
            ? reference.FrameworkName
            : $"{reference.FrameworkName} (Condition: {reference.Condition})";
    }
}
