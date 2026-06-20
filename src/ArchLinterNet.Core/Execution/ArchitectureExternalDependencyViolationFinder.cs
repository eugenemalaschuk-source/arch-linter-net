using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

internal static class ArchitectureExternalDependencyViolationFinder
{
    public static IEnumerable<ArchitectureViolation> FindViolations(
        string contractName,
        string? contractId,
        string externalGroupName,
        Type[] sourceTypes,
        ArchitectureExternalDependencyGroup externalGroup,
        IReadOnlyCollection<ArchitectureIgnoredViolation> ignoredViolations)
    {
        return sourceTypes
            .Select(type =>
            {
                string sourceType = ArchitectureTypeNames.SafeFullName(type);
                string[] forbiddenReferences = ArchitectureReferenceScanner.GetReferencedTypes(type)
                    .Select(reference => new
                    {
                        FullName = ArchitectureTypeNames.SafeFullName(reference),
                        Namespace = ArchitectureTypeNames.SafeNamespace(reference)
                    })
                    .Where(reference => !string.IsNullOrEmpty(reference.FullName))
                    .Where(reference =>
                        ArchitectureExternalDependencyResolver.MatchesGroup(externalGroup, reference.FullName,
                            reference.Namespace))
                    .Where(reference =>
                        !ArchitectureIgnoreMatcher.IsIgnored(sourceType, reference.FullName, ignoredViolations))
                    .Select(reference => reference.FullName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();

                if (forbiddenReferences.Length == 0)
                {
                    return null;
                }

                return new ArchitectureViolation(
                    contractName,
                    contractId,
                    sourceType,
                    $"external dependency group '{externalGroupName}'",
                    forbiddenReferences)
                {
                    ForbiddenExternalGroup = externalGroupName
                };
            })
            .Where(violation => violation != null)!;
    }
}
