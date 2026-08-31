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
        List<ArchitectureApplicabilityReason> reasons = CollectRootReasons(context, roots, provenance);

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
        Dictionary<ArchitectureContractExposureTarget, int[]> matchingSelectorsByTarget =
            MatchForbiddenTargets(context, contract, targetUniverse, reasons, provenance);

        List<ArchitectureViolation> violations = exposure is null || matchingSelectorsByTarget.Count == 0
            ? new List<ArchitectureViolation>()
            : BuildViolations(new ContractSurfaceExposureFindingInput(
                contract,
                executionContext,
                exposure,
                matchingSelectorsByTarget,
                ExportedSurface,
                contract.Source.PublicApiSurface));

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

    private static List<ArchitectureApplicabilityReason> CollectRootReasons(
        ArchitectureCheckerContext context,
        RootSelection roots,
        ArchitectureApplicabilityProvenance provenance)
    {
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

        return reasons;
    }

    internal static List<ArchitectureViolation> BuildViolations(ContractSurfaceExposureFindingInput input)
    {
        var violations = new List<ArchitectureViolation>();
        foreach (ArchitectureContractExposure occurrence in input.Exposure.Exposures)
        {
            if (!input.MatchingSelectorsByTarget.TryGetValue(occurrence.ReferencedType, out int[]? matchingSelectors))
            {
                continue;
            }

            string targetReference = occurrence.ReferencedType.Identity;
            string sourceType = occurrence.DeclaringType.FullTypeName;
            string? sourceSite = SourceSite(occurrence.Path);
            bool ignored = input.ExecutionContext.IsIgnored(
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
                input.Contract.Name,
                input.Contract.Id,
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
                    input.SourceSurface,
                    sourceSite,
                    input.ReviewedPublicApiSurface,
                    matchingSelectors),
            });
        }

        return violations;
    }

    private static Dictionary<ArchitectureContractExposureTarget, int[]> MatchForbiddenTargets(
        ArchitectureCheckerContext context,
        ArchitectureContractSurfaceExposureContract contract,
        IReadOnlyList<Type> targetUniverse,
        List<ArchitectureApplicabilityReason> reasons,
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
        HashSet<string> targetAssemblyNames = context.AnalysisContext.TargetAssemblies
            .Select(assembly => assembly.GetName().Name ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);

        RootFilterResolution filters = ResolveRootFilters(context, source, targetAssemblyNames);
        RootCandidateResolution candidateResolution = ResolveRootCandidates(
            context, source, filters.AssemblyFilter, filters.ProjectAssemblies);

        List<Type> candidates = FilterCandidates(
            context, contract, source, candidateResolution.Candidates, filters.AssemblyFilter, filters.ProjectAssemblies);

        Type[] roots = candidates
            .Distinct()
            .OrderBy(TypeIdentity, StringComparer.Ordinal)
            .ToArray();
        return new RootSelection(
            roots,
            filters.IsComplete && candidateResolution.IsComplete,
            filters.Stale || candidateResolution.Stale);
    }

    // Assembly/project selectors are declarations over the resolved analysis graph. A partly
    // resolved selector cannot be treated as an empty, clean source set: otherwise one typo or
    // one missing project would silently narrow the contract to the remaining assemblies.
    private static RootFilterResolution ResolveRootFilters(
        ArchitectureCheckerContext context,
        ArchitectureContractSurfaceExposureSource source,
        HashSet<string> targetAssemblyNames)
    {
        bool hasAssemblyFilter = source.Assemblies is { Count: > 0 };
        HashSet<string>? assemblyFilter = hasAssemblyFilter
            ? source.Assemblies.ToHashSet(StringComparer.Ordinal)
            : null;
        HashSet<string>? projectAssemblies = null;
        bool stale = false;
        bool isComplete = true;

        if (hasAssemblyFilter)
        {
            bool hasUnknownAssembly = source.Assemblies
                .Distinct(StringComparer.Ordinal)
                .Any(name => !targetAssemblyNames.Contains(name));
            stale |= hasUnknownAssembly;
            isComplete &= !hasUnknownAssembly;
        }

        if (source.Projects is { Count: > 0 })
        {
            string[] requestedProjects = source.Projects.Distinct(StringComparer.Ordinal).ToArray();
            projectAssemblies = context.ResolveProjectAssemblyNames(requestedProjects.ToList())
                .ToHashSet(StringComparer.Ordinal);
            bool hasUnknownProject = projectAssemblies.Count != requestedProjects.Length;
            bool hasNoResolvedTargetProject = !projectAssemblies.Any(targetAssemblyNames.Contains);
            stale |= hasUnknownProject || hasNoResolvedTargetProject;
            isComplete &= !hasUnknownProject && !hasNoResolvedTargetProject;
        }

        return new RootFilterResolution(assemblyFilter, projectAssemblies, isComplete, stale);
    }

    private static RootCandidateResolution ResolveRootCandidates(
        ArchitectureCheckerContext context,
        ArchitectureContractSurfaceExposureSource source,
        HashSet<string>? assemblyFilter,
        HashSet<string>? projectAssemblies)
    {
        if (!string.IsNullOrWhiteSpace(source.PublicApiSurface))
        {
            ArchitecturePublicApiSurfaceRootResolution resolved =
                context.ResolvePublicApiSurfaceRoots(source.PublicApiSurface);
            return new RootCandidateResolution(
                resolved.Roots.ToList(),
                resolved.HasContract && resolved.IsComplete,
                !resolved.HasContract);
        }

        var candidates = new List<Type>();
        bool isComplete = true;
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

        bool stale = assemblyFilter is not null && candidates.Count == 0;
        return new RootCandidateResolution(candidates, isComplete, stale);
    }

    private static List<Type> FilterCandidates(
        ArchitectureCheckerContext context,
        ArchitectureContractSurfaceExposureContract contract,
        ArchitectureContractSurfaceExposureSource source,
        List<Type> candidates,
        HashSet<string>? assemblyFilter,
        HashSet<string>? projectAssemblies)
    {
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

        return candidates;
    }

    private static ArchitectureApplicabilityReason Reason(
        string code,
        ArchitectureApplicabilityProvenance provenance) => new(code, provenance);

    private static string? SourceSite(ArchitectureContractExposurePath path)
    {
        ArchitectureContractExposurePathSegment member = path.Segments
            .FirstOrDefault(segment => segment.Kind is "member" or "attribute" or "attribute_argument");
        return member.ToString();
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

    private sealed record RootFilterResolution(
        HashSet<string>? AssemblyFilter,
        HashSet<string>? ProjectAssemblies,
        bool IsComplete,
        bool Stale);

    private sealed record RootCandidateResolution(
        List<Type> Candidates,
        bool IsComplete,
        bool Stale);
}

internal sealed record ContractSurfaceExposureEvaluationResult(
    IReadOnlyList<ArchitectureViolation> Violations,
    ArchitectureApplicabilityExpectedEntry ApplicabilityExpectedEntry,
    ArchitectureApplicabilityRecord ApplicabilityRecord);

/// <summary>Shared typed input for families that project existing exposure facts into findings.</summary>
internal sealed record ContractSurfaceExposureFindingInput(
    IArchitectureContract Contract,
    ArchitectureContractExecutionContext ExecutionContext,
    ArchitectureContractSurfaceExposureResult Exposure,
    IReadOnlyDictionary<ArchitectureContractExposureTarget, int[]> MatchingSelectorsByTarget,
    string SourceSurface,
    string? ReviewedPublicApiSurface);
