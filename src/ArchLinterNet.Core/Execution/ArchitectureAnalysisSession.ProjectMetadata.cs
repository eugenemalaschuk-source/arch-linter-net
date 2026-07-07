using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Execution;

public sealed partial class ArchitectureAnalysisSession
{
    public List<ArchitectureViolation> CheckProjectMetadataContract(ArchitectureProjectMetadataContract contract)
    {
        if (!IsContractSelected(contract.Id))
        {
            return new List<ArchitectureViolation>();
        }

        Dictionary<string, ArchitectureDiscoveredProject> projectsByPath = BuildProjectMetadataLookup();
        List<ArchitectureViolation> violations = new();

        foreach (string configuredProjectPath in contract.Projects
                     .Select(NormalizeProjectPath)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (!projectsByPath.TryGetValue(configuredProjectPath, out ArchitectureDiscoveredProject? project))
            {
                continue;
            }

            violations.AddRange(CheckRequiredProperties(contract, project));
            violations.AddRange(CheckForbiddenProperties(contract, project));
            violations.AddRange(CheckFriendAssemblies(contract, project));
            violations.AddRange(CheckForbiddenProjectReferences(contract, project));
        }

        return violations
            .OrderBy(v => v.SourceType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.ProjectMetadataKey ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(v => v.ForbiddenNamespace, StringComparer.Ordinal)
            .ThenBy(v => v.ForbiddenReferences.FirstOrDefault() ?? string.Empty, StringComparer.Ordinal)
            .ToList();
    }

    private static IEnumerable<ArchitectureViolation> CheckRequiredProperties(
        ArchitectureProjectMetadataContract contract,
        ArchitectureDiscoveredProject project)
    {
        foreach (KeyValuePair<string, string> requirement in contract.RequiredProperties
                     .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            bool found = project.Properties.TryGetValue(requirement.Key, out ArchitectureDiscoveredProjectProperty? property);
            string? actualValue = found ? property!.Value : null;

            if (actualValue != null && string.Equals(actualValue, requirement.Value, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new ArchitectureViolation(
                contract.Name,
                contract.Id,
                project.Path,
                "required project property mismatch",
                new[]
                {
                    $"Property '{requirement.Key}' expected '{requirement.Value}' but actual value was '{actualValue ?? "<missing>"}'."
                })
            {
                ProjectMetadataKind = "required_property",
                ProjectMetadataKey = requirement.Key,
                ProjectMetadataExpectedValue = requirement.Value,
                ProjectMetadataActualValue = actualValue,
                ProjectMetadataSourcePath = property?.SourcePath
            };
        }
    }

    private static IEnumerable<ArchitectureViolation> CheckForbiddenProperties(
        ArchitectureProjectMetadataContract contract,
        ArchitectureDiscoveredProject project)
    {
        foreach (KeyValuePair<string, string> rule in contract.ForbiddenProperties
                     .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!project.Properties.TryGetValue(rule.Key, out ArchitectureDiscoveredProjectProperty? property)
                || !string.Equals(property.Value, rule.Value, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new ArchitectureViolation(
                contract.Name,
                contract.Id,
                project.Path,
                "forbidden project property value",
                new[]
                {
                    $"Property '{rule.Key}' must not be '{rule.Value}'."
                })
            {
                ProjectMetadataKind = "forbidden_property",
                ProjectMetadataKey = rule.Key,
                ProjectMetadataExpectedValue = rule.Value,
                ProjectMetadataActualValue = property.Value,
                ProjectMetadataSourcePath = property.SourcePath
            };
        }
    }

    private static IEnumerable<ArchitectureViolation> CheckFriendAssemblies(
        ArchitectureProjectMetadataContract contract,
        ArchitectureDiscoveredProject project)
    {
        if (!contract.AllowedFriendAssemblies.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            yield break;
        }

        HashSet<string> allowed = new(
            contract.AllowedFriendAssemblies.Where(value => !string.IsNullOrWhiteSpace(value)),
            StringComparer.Ordinal);

        foreach (ArchitectureDiscoveredFriendAssembly friendAssembly in project.FriendAssemblies
                     .OrderBy(entry => entry.AssemblyName, StringComparer.Ordinal))
        {
            if (allowed.Contains(friendAssembly.AssemblyName))
            {
                continue;
            }

            yield return new ArchitectureViolation(
                contract.Name,
                contract.Id,
                project.Path,
                "forbidden friend assembly",
                new[]
                {
                    $"Friend assembly '{friendAssembly.AssemblyName}' is not present in allowed_friend_assemblies."
                })
            {
                ProjectMetadataKind = "friend_assembly",
                ProjectMetadataKey = "InternalsVisibleTo",
                ProjectMetadataActualValue = friendAssembly.AssemblyName,
                ProjectMetadataSourcePath = friendAssembly.SourcePath
            };
        }
    }

    private static IEnumerable<ArchitectureViolation> CheckForbiddenProjectReferences(
        ArchitectureProjectMetadataContract contract,
        ArchitectureDiscoveredProject project)
    {
        foreach (ArchitectureDiscoveredProjectReference projectReference in project.ProjectReferences
                     .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            string? matchedPattern = contract.ForbiddenProjectReferences
                .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                .OrderBy(pattern => pattern, StringComparer.Ordinal)
                .FirstOrDefault(pattern => ProjectPathGlob.IsMatch(projectReference.Path, pattern));

            if (matchedPattern == null)
            {
                continue;
            }

            yield return new ArchitectureViolation(
                contract.Name,
                contract.Id,
                project.Path,
                "forbidden project reference",
                new[]
                {
                    $"Project reference '{projectReference.Path}' matches forbidden pattern '{matchedPattern}'."
                })
            {
                ProjectMetadataKind = "project_reference",
                ProjectMetadataKey = "ProjectReference",
                ProjectMetadataExpectedValue = matchedPattern,
                ProjectMetadataActualValue = projectReference.Path,
                ProjectMetadataSourcePath = projectReference.SourcePath
            };
        }
    }

    private Dictionary<string, ArchitectureDiscoveredProject> BuildProjectMetadataLookup()
    {
        IReadOnlyCollection<ArchitectureDiscoveredProject> discoveredProjects =
            Context.ProjectDiscovery?.DiscoveredProjects ?? Array.Empty<ArchitectureDiscoveredProject>();

        return discoveredProjects
            .GroupBy(project => NormalizeProjectPath(project.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
    }
}
