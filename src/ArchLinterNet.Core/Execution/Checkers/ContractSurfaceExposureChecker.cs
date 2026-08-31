using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Execution.Checkers;

/// <summary>Joins bounded source/target selectors with cached recursive exposure evidence.</summary>
internal static class ContractSurfaceExposureChecker
{
    internal const string Family = "contract_surface_exposure";
    private const string ExportedSurface = "exported";

    internal static ContractSurfaceExposureEvaluationResult Evaluate(
        ArchitectureCheckerContext context,
        ArchitectureContractSurfaceExposureContract contract,
        ArchitectureContractExecutionContext executionContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(executionContext);

        ArchitectureApplicabilityProvenance provenance = new(
            Family,
            contract.Id ?? contract.Name,
            context.Document.Name);
        ArchitectureApplicabilityExpectedEntry expected = new(
            contract.Id ?? contract.Name,
            Family,
            ArchitectureApplicabilityMembership.Required,
            provenance);

        RootSelection roots = ResolveRoots(context, contract);
        List<ArchitectureApplicabilityReason> reasons = new();

        if (!roots.IsComplete)
        {
            reasons.Add(Reason(ArchitectureApplicabilityReasonCodes.MissingRequiredInput, provenance));
        }

        if (roots.Roots.Count == 0)
        {
            reasons.Add(Reason(
                roots.HasStaleSource
                    ? ArchitectureApplicabilityReasonCodes.StaleDeclaration
                    : ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput,
                provenance));
        }

        if (!context.TypeIndex.HasCompleteTypeUniverse)
        {
            reasons.Add(Reason(ArchitectureApplicabilityReasonCodes.MissingRequiredInput, provenance));
        }

        // The analysis type index contains only first-party targets. Exposure facts also retain
        // the actual reflected Type for every referenced target, including framework/external
        // assemblies, so forbidden selectors operate over the complete relevant universe.
        ArchitectureContractSurfaceExposureResult? exposure = roots.Roots.Count == 0
            ? null
            : context.GetContractSurfaceExposure(roots.Roots, ArchitectureContractSurfaceShape.Exported);
        if (exposure is not null && !exposure.IsComplete)
        {
            reasons.Add(Reason(ArchitectureApplicabilityReasonCodes.MissingRequiredInput, provenance));
        }

        Type[] targetUniverse = context.TypeIndex.AllTypes()
            .Concat(exposure?.ReferencedTypes.Values ?? Enumerable.Empty<Type>())
            .Distinct()
            .OrderBy(TypeIdentity, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyDictionary<ArchitectureContractExposureTarget, int[]> matchingSelectorsByTarget =
            MatchForbiddenTargets(context, contract, targetUniverse, reasons, provenance);

        var violations = new List<ArchitectureViolation>();
        if (exposure is not null && matchingSelectorsByTarget.Count > 0)
        {
            foreach (ArchitectureContractExposure occurrence in exposure.Exposures)
            {
                if (!matchingSelectorsByTarget.TryGetValue(
                        occurrence.ReferencedType,
                        out int[]? matchingSelectors))
                {
                    continue;
                }

                string targetReference = occurrence.ReferencedType.Identity;
                string sourceType = occurrence.DeclaringType.FullTypeName;
                string? sourceSite = SourceSite(occurrence.Path);
                bool ignored = executionContext.IsIgnored(
                    sourceType,
                    targetReference,
                    sourceAssembly: occurrence.DeclaringType.AssemblyName,
                    targetAssembly: occurrence.ReferencedType.AssemblyName,
                    targetType: occurrence.ReferencedType.FullTypeName,
                    sourceMember: sourceSite,
                    targetMember: occurrence.Path.CanonicalKey);
                if (ignored)
                {
                    continue;
                }

                violations.Add(new ArchitectureViolation(
                    contract.Name,
                    contract.Id,
                    sourceType,
                    occurrence.ReferencedType.FullTypeName,
                    [targetReference])
                {
                    Payload = new ContractSurfaceExposurePayload(
                        occurrence.DeclaringType.AssemblyName,
                        sourceType,
                        occurrence.Path.ToString(),
                        occurrence.Path.CanonicalKey,
                        occurrence.ReferencedType.AssemblyName,
                        occurrence.ReferencedType.FullTypeName,
                        ExportedSurface,
                        sourceSite,
                        contract.Source.PublicApiSurface,
                        matchingSelectors),
                });
            }
        }

        ArchitectureApplicabilityRecord record = reasons.Count == 0
            ? new ArchitectureApplicabilityRecord(
                contract.Id ?? contract.Name,
                Family,
                ArchitectureApplicabilityRecordState.Evaluable,
                provenance)
            : new ArchitectureApplicabilityRecord(
                contract.Id ?? contract.Name,
                Family,
                ArchitectureApplicabilityRecordState.Unassessable,
                reasons.Distinct().ToArray(),
                provenance);
        return new ContractSurfaceExposureEvaluationResult(violations, expected, record);
    }

    private static IReadOnlyDictionary<ArchitectureContractExposureTarget, int[]> MatchForbiddenTargets(
        ArchitectureCheckerContext context,
        ArchitectureContractSurfaceExposureContract contract,
        IReadOnlyList<Type> targetUniverse,
        ICollection<ArchitectureApplicabilityReason> reasons,
        ArchitectureApplicabilityProvenance provenance)
    {
        var selectorIndexesByTarget = new Dictionary<ArchitectureContractExposureTarget, List<int>>();

        for (int index = 0; index < contract.Forbidden.Count; index++)
        {
            ArchitecturePublicApiSurfaceSelector selector = contract.Forbidden[index];
            bool matched = false;
            foreach (Type type in targetUniverse)
            {
                if (!ArchitecturePublicApiSurfaceSelectorMatcher.Matches(
                        type, selector, context.Document, contract.Name, context.RoleIndex))
                {
                    continue;
                }

                matched = true;
                ArchitectureContractExposureTarget target = TargetIdentity(type);
                if (!selectorIndexesByTarget.TryGetValue(target, out List<int>? indexes))
                {
                    indexes = new List<int>();
                    selectorIndexesByTarget.Add(target, indexes);
                }

                indexes.Add(index);
            }

            if (!matched)
            {
                reasons.Add(Reason(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput, provenance));
            }
        }

        return selectorIndexesByTarget.ToDictionary(
            item => item.Key,
            item => item.Value.ToArray());
    }

    private static RootSelection ResolveRoots(
        ArchitectureCheckerContext context,
        ArchitectureContractSurfaceExposureContract contract)
    {
        ArchitectureContractSurfaceExposureSource source = contract.Source;
        bool hasAssemblyFilter = source.Assemblies is { Count: > 0 };
        bool hasProjectFilter = source.Projects is { Count: > 0 };
        HashSet<string>? assemblyFilter = hasAssemblyFilter
            ? source.Assemblies.ToHashSet(StringComparer.Ordinal)
            : null;
        HashSet<string>? projectAssemblies = null;
        bool stale = false;
        HashSet<string> targetAssemblyNames = context.AnalysisContext.TargetAssemblies
            .Select(assembly => assembly.GetName().Name ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        bool isComplete = true;

        // Assembly/project selectors are declarations over the resolved analysis graph. A partly
        // resolved selector cannot be treated as an empty, clean source set: otherwise one typo or
        // one missing project would silently narrow the contract to the remaining assemblies.
        if (hasAssemblyFilter)
        {
            bool hasUnknownAssembly = source.Assemblies
                .Distinct(StringComparer.Ordinal)
                .Any(name => !targetAssemblyNames.Contains(name));
            stale |= hasUnknownAssembly;
            isComplete &= !hasUnknownAssembly;
        }

        if (hasProjectFilter)
        {
            string[] requestedProjects = source.Projects.Distinct(StringComparer.Ordinal).ToArray();
            projectAssemblies = context.ResolveProjectAssemblyNames(requestedProjects.ToList())
                .ToHashSet(StringComparer.Ordinal);
            bool hasUnknownProject = projectAssemblies.Count != requestedProjects.Length;
            bool hasNoResolvedTargetProject = !projectAssemblies.Any(targetAssemblyNames.Contains);
            stale |= hasUnknownProject || hasNoResolvedTargetProject;
            isComplete &= !hasUnknownProject && !hasNoResolvedTargetProject;
        }

        List<Type> candidates;
        if (!string.IsNullOrWhiteSpace(source.PublicApiSurface))
        {
            ArchitecturePublicApiSurfaceRootResolution resolved =
                context.ResolvePublicApiSurfaceRoots(source.PublicApiSurface!);
            candidates = resolved.Roots.ToList();
            isComplete &= resolved.HasContract && resolved.IsComplete;
            stale |= !resolved.HasContract;
        }
        else
        {
            candidates = new List<Type>();
            foreach (Assembly assembly in context.AnalysisContext.TargetAssemblies
                         .Distinct()
                         .OrderBy(AssemblyIdentity, StringComparer.Ordinal))
            {
                string assemblyName = assembly.GetName().Name ?? string.Empty;
                if (assemblyFilter is not null && !assemblyFilter.Contains(assemblyName))
                {
                    continue;
                }

                if (projectAssemblies is not null && !projectAssemblies.Contains(assemblyName))
                {
                    continue;
                }

                ArchitecturePublicApiSurfaceMaterialization surface = context.GetPublicApiSurface(assembly);
                isComplete &= surface.IsComplete;
                candidates.AddRange(surface.ExportedTypes);
            }

            if (hasAssemblyFilter && candidates.Count == 0)
            {
                stale = true;
            }
        }

        if (assemblyFilter is not null)
        {
            candidates = candidates
                .Where(type => assemblyFilter.Contains(type.Assembly.GetName().Name ?? string.Empty))
                .ToList();
        }

        if (projectAssemblies is not null)
        {
            candidates = candidates
                .Where(type => projectAssemblies.Contains(type.Assembly.GetName().Name ?? string.Empty))
                .ToList();
        }

        if (source.TypesMatching is not null)
        {
            candidates = candidates
                .Where(type => ArchitecturePublicApiSurfaceSelectorMatcher.Matches(
                    type, source.TypesMatching, context.Document, contract.Name, context.RoleIndex))
                .ToList();
        }

        Type[] roots = candidates
            .Distinct()
            .OrderBy(TypeIdentity, StringComparer.Ordinal)
            .ToArray();
        return new RootSelection(roots, isComplete, stale);
    }

    private static ArchitectureApplicabilityReason Reason(
        string code,
        ArchitectureApplicabilityProvenance provenance) => new(code, provenance);

    private static string? SourceSite(ArchitectureContractExposurePath path)
    {
        ArchitectureContractExposurePathSegment? member = path.Segments
            .FirstOrDefault(segment => segment.Kind is "member" or "attribute" or "attribute_argument");
        return member?.ToString();
    }

    private static ArchitectureContractExposureTarget TargetIdentity(Type type) =>
        new(AssemblyIdentity(type.Assembly), type.FullName ?? type.Name);

    private static string TypeIdentity(Type type) =>
        $"{AssemblyIdentity(type.Assembly)}\u001f{type.FullName ?? type.Name}";

    private static string AssemblyIdentity(Assembly assembly) => assembly.FullName ?? assembly.GetName().Name ?? string.Empty;

    private sealed record RootSelection(
        IReadOnlyList<Type> Roots,
        bool IsComplete,
        bool HasStaleSource);
}

internal sealed record ContractSurfaceExposureEvaluationResult(
    IReadOnlyList<ArchitectureViolation> Violations,
    ArchitectureApplicabilityExpectedEntry ApplicabilityExpectedEntry,
    ArchitectureApplicabilityRecord ApplicabilityRecord);
