using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckPackageDependencyContract(ArchitecturePackageDependencyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        RequireDirectDependencyDepth(contract.Name, contract.DependencyDepth);

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        Dictionary<string, IReadOnlyList<ArchitectureDiscoveredPackageReference>> packagesByProject = BuildPackageLookup();

        if (!packagesByProject.TryGetValue(contract.Source, out IReadOnlyList<ArchitectureDiscoveredPackageReference>? references))
        {
            executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
            return violations;
        }

        foreach (string packageGroupName in contract.Forbidden)
        {
            if (!Document.Packages.TryGetValue(packageGroupName, out ArchitecturePackageGroup? packageGroup))
            {
                continue;
            }

            string[] forbiddenReferences = references
                .Where(reference => ArchitecturePackageDependencyResolver.MatchesGroup(packageGroup, reference.PackageId))
                .Where(reference => !executionContext.IsIgnored(contract.Source, reference.PackageId))
                .Select(FormatPackageReference)
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
                $"package group '{packageGroupName}'",
                forbiddenReferences)
            {
                ForbiddenPackageGroup = packageGroupName
            });
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    public List<ArchitectureViolation> CheckPackageAllowOnlyContract(ArchitecturePackageAllowOnlyContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        RequireDirectDependencyDepth(contract.Name, contract.DependencyDepth);

        List<ArchitectureViolation> violations = new();
        ArchitectureContractExecutionContext executionContext = CreateExecutionContext(contract, contract.IgnoredViolations);

        Dictionary<string, IReadOnlyList<ArchitectureDiscoveredPackageReference>> packagesByProject = BuildPackageLookup();

        if (!packagesByProject.TryGetValue(contract.Source, out IReadOnlyList<ArchitectureDiscoveredPackageReference>? references))
        {
            executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
            return violations;
        }

        List<ArchitecturePackageGroup> allowedGroups = contract.Allowed
            .Select(groupName => Document.Packages.TryGetValue(groupName, out ArchitecturePackageGroup? group) ? group : null)
            .Where(group => group != null)
            .Select(group => group!)
            .ToList();

        string[] disallowedReferences = references
            .Where(reference => !allowedGroups.Any(group => ArchitecturePackageDependencyResolver.MatchesGroup(group, reference.PackageId)))
            .Where(reference => !executionContext.IsIgnored(contract.Source, reference.PackageId))
            .Select(FormatPackageReference)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToArray();

        if (disallowedReferences.Length > 0)
        {
            violations.Add(new ArchitectureViolation(
                contract.Name,
                contract.Id,
                contract.Source,
                "outside allowed package groups",
                disallowedReferences));
        }

        executionContext.CollectUnmatchedIgnores(_unmatchedIgnoredViolations);
        return violations;
    }

    private Dictionary<string, IReadOnlyList<ArchitectureDiscoveredPackageReference>> BuildPackageLookup()
    {
        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects =
            Context.ProjectDiscovery?.DiscoveredProjects ?? Array.Empty<ArchitectureDiscoveredProject>();

        return discoveredProjects
            .GroupBy(project => project.AssemblyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ArchitectureDiscoveredPackageReference>)group.First().PackageReferences,
                StringComparer.Ordinal);
    }

    private static string FormatPackageReference(ArchitectureDiscoveredPackageReference reference)
    {
        return reference.Version == null ? reference.PackageId : $"{reference.PackageId}@{reference.Version}";
    }
}
