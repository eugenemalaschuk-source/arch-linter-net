using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution.Checkers;

// Both package families: "package_dependency" (forbidden groups) and "package_allow_only".
internal static class PackageDependencyChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitecturePackageDependencyContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        if (!TryGetPackageReferences(
                context, contract.Source, out IReadOnlyList<ArchitectureDiscoveredPackageReference> references))
        {
            return violations;
        }

        foreach (string packageGroupName in contract.Forbidden)
        {
            if (!context.Document.Packages.TryGetValue(packageGroupName, out ArchitecturePackageGroup? packageGroup))
            {
                continue;
            }

            string[] forbiddenReferences = references
                .Where(reference => ArchitecturePackageDependencyResolver.MatchesGroup(packageGroup, reference.PackageId))
                .Where(reference => !executionContext.IsIgnored(
                    contract.Source,
                    reference.PackageId,
                    sourceAssembly: contract.Source,
                    targetType: reference.PackageId,
                    targetMember: reference.PackageId))
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
                Payload = new PackageDependencyPayload(packageGroupName)
            });
        }

        return violations;
    }

    public static List<ArchitectureViolation> CheckAllowOnly(
        ArchitecturePackageAllowOnlyContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();

        if (!TryGetPackageReferences(
                context, contract.Source, out IReadOnlyList<ArchitectureDiscoveredPackageReference> references))
        {
            return violations;
        }

        List<ArchitecturePackageGroup> allowedGroups = contract.Allowed
            .Select(groupName => context.Document.Packages.TryGetValue(groupName, out ArchitecturePackageGroup? group) ? group : null)
            .Where(group => group != null)
            .Select(group => group!)
            .ToList();

        string[] disallowedReferences = references
            .Where(reference => !allowedGroups.Any(group => ArchitecturePackageDependencyResolver.MatchesGroup(group, reference.PackageId)))
            .Where(reference => !executionContext.IsIgnored(
                contract.Source,
                reference.PackageId,
                sourceAssembly: contract.Source,
                targetType: reference.PackageId,
                targetMember: reference.PackageId))
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
                disallowedReferences)
            {
                Payload = new PackageAllowOnlyPayload(contract.Allowed.ToArray())
            });
        }

        return violations;
    }

    private static bool TryGetPackageReferences(
        ArchitectureCheckerContext context,
        string source,
        out IReadOnlyList<ArchitectureDiscoveredPackageReference> references)
    {
        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects =
            context.AnalysisContext.ProjectDiscovery?.DiscoveredProjects ?? Array.Empty<ArchitectureDiscoveredProject>();

        Dictionary<string, IReadOnlyList<ArchitectureDiscoveredPackageReference>> packagesByProject = discoveredProjects
            .GroupBy(project => project.AssemblyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().PackageReferences,
                StringComparer.Ordinal);

        return packagesByProject.TryGetValue(source, out references!);
    }

    private static string FormatPackageReference(ArchitectureDiscoveredPackageReference reference)
    {
        return reference.Version == null ? reference.PackageId : $"{reference.PackageId}@{reference.Version}";
    }
}
