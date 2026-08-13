using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;

namespace ArchLinterNet.Core.Execution.Checkers;

// Both framework-reference families: "framework_dependency" (forbidden groups) and
// "framework_allow_only". MSBuild evaluation itself stays session-owned (it is cached per run and
// also feeds CheckConfiguration's fail-closed evaluation-failure surfacing); this checker only reads
// the resolved references through ArchitectureCheckerContext.
internal static class FrameworkReferenceChecker
{
    public static List<ArchitectureViolation> Check(
        ArchitectureFrameworkReferenceContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();
        string configuration = context.ResolvedBuildConfiguration;

        IReadOnlyList<ArchitectureDiscoveredFrameworkReference> references =
            context.ResolveFrameworkReferences(contract.Source);

        foreach (string frameworkGroupName in contract.Forbidden)
        {
            if (!context.Document.FrameworkReferences.TryGetValue(
                    frameworkGroupName, out ArchitectureFrameworkReferenceGroup? frameworkGroup))
            {
                continue;
            }

            ArchitectureDiscoveredFrameworkReference[] matched = references
                .Where(reference => ArchitectureFrameworkReferenceResolver.MatchesGroup(frameworkGroup, reference.FrameworkName))
                .Where(reference => !executionContext.IsIgnored(
                    contract.Source, reference.FrameworkName,
                    sourceAssembly: contract.Source,
                    targetType: reference.FrameworkName,
                    targetMember: FormatFrameworkReference(reference),
                    configuration: configuration))
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
                Payload = new FrameworkReferencePayload(frameworkGroupName, BuildEvidence(matched, configuration))
            });
        }

        return violations;
    }

    public static List<ArchitectureViolation> CheckAllowOnly(
        ArchitectureFrameworkReferenceAllowOnlyContract contract,
        ArchitectureCheckerContext context,
        ArchitectureContractExecutionContext executionContext)
    {
        List<ArchitectureViolation> violations = new();
        string configuration = context.ResolvedBuildConfiguration;

        IReadOnlyList<ArchitectureDiscoveredFrameworkReference> references =
            context.ResolveFrameworkReferences(contract.Source);

        List<ArchitectureFrameworkReferenceGroup> allowedGroups = contract.Allowed
            .Select(groupName => context.Document.FrameworkReferences.TryGetValue(
                groupName, out ArchitectureFrameworkReferenceGroup? group) ? group : null)
            .Where(group => group != null)
            .Select(group => group!)
            .ToList();

        ArchitectureDiscoveredFrameworkReference[] disallowed = references
            .Where(reference => !allowedGroups.Any(group =>
                ArchitectureFrameworkReferenceResolver.MatchesGroup(group, reference.FrameworkName)))
                .Where(reference => !executionContext.IsIgnored(
                    contract.Source, reference.FrameworkName,
                    sourceAssembly: contract.Source,
                    targetType: reference.FrameworkName,
                    targetMember: FormatFrameworkReference(reference),
                    configuration: configuration))
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
                Payload = new FrameworkReferenceAllowOnlyPayload(
                    contract.Allowed.ToArray(), BuildEvidence(disallowed, configuration))
            });
        }

        return violations;
    }

    private static FrameworkReferenceEvidence[] BuildEvidence(
        IEnumerable<ArchitectureDiscoveredFrameworkReference> references, string configuration)
    {
        return references
            .Select(reference => new FrameworkReferenceEvidence(
                reference.FrameworkName, reference.TargetFramework, reference.Explicit, reference.SourcePath, configuration))
            .ToArray();
    }

    private static string FormatFrameworkReference(ArchitectureDiscoveredFrameworkReference reference)
    {
        return $"{reference.FrameworkName} ({reference.TargetFramework})";
    }
}
