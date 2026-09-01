using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Execution.Checkers;

/// <summary>Applies named version/surface groups to existing recursive exposure evidence.</summary>
internal static class VersionedContractSurfaceIsolationChecker
{
    internal const string Family = "versioned_contract_surface_isolation";

    internal static ContractSurfaceExposureEvaluationResult Evaluate(
        ArchitectureCheckerContext context,
        ArchitectureVersionedContractSurfaceIsolationContract contract,
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
        List<ArchitectureApplicabilityReason> reasons = new();

        if (!context.TypeIndex.HasCompleteTypeUniverse)
        {
            reasons.Add(Reason(ArchitectureApplicabilityReasonCodes.MissingRequiredInput, provenance));
        }

        Type[] firstPartyTypes = context.TypeIndex.AllTypes()
            .Distinct()
            .OrderBy(TypeIdentity, StringComparer.Ordinal)
            .ToArray();
        var surfaces = contract.Surfaces.ToDictionary(surface => surface.Id, StringComparer.OrdinalIgnoreCase);
        ArchitectureVersionedContractSurfaceIsolationSurface sourceSurface = surfaces[contract.SourceSurface];
        Type[] sourceMatches = MatchSurface(context, contract, sourceSurface, firstPartyTypes);
        if (sourceMatches.Length == 0)
        {
            reasons.Add(Reason(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput, provenance));
        }

        ExportedRootResolution exportedRoots = ContractSurfaceExposureChecker.ResolveExportedRoots(
            context,
            context.AnalysisContext.TargetAssemblies);
        if (!exportedRoots.IsComplete)
        {
            reasons.Add(Reason(ArchitectureApplicabilityReasonCodes.MissingRequiredInput, provenance));
        }

        HashSet<Type> matchedSourceTypes = new(sourceMatches);
        Type[] roots = exportedRoots.Roots
            .Where(matchedSourceTypes.Contains)
            .OrderBy(TypeIdentity, StringComparer.Ordinal)
            .ToArray();
        if (roots.Length == 0)
        {
            reasons.Add(Reason(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput, provenance));
        }

        ArchitectureContractSurfaceExposureResult? exposure = roots.Length == 0
            ? null
            : context.GetContractSurfaceExposure(roots, ArchitectureContractSurfaceShape.Exported);
        if (exposure is not null && !exposure.IsComplete)
        {
            reasons.Add(Reason(ArchitectureApplicabilityReasonCodes.MissingRequiredInput, provenance));
        }

        Type[] targetUniverse = firstPartyTypes
            .Concat(exposure?.ReferencedTypes.Values ?? Enumerable.Empty<Type>())
            .Distinct()
            .OrderBy(TypeIdentity, StringComparer.Ordinal)
            .ToArray();
        Dictionary<ArchitectureContractExposureTarget, int[]> forbiddenTargets = MatchForbiddenSurfaces(
            context,
            contract,
            surfaces,
            targetUniverse,
            reasons,
            provenance);

        List<ArchitectureViolation> violations = exposure is null || forbiddenTargets.Count == 0
            ? new List<ArchitectureViolation>()
            : ContractSurfaceExposureChecker.BuildViolations(new ContractSurfaceExposureFindingInput(
                contract,
                executionContext,
                exposure,
                forbiddenTargets,
                sourceSurface.Id,
                null));

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

    private static Dictionary<ArchitectureContractExposureTarget, int[]> MatchForbiddenSurfaces(
        ArchitectureCheckerContext context,
        ArchitectureVersionedContractSurfaceIsolationContract contract,
        IReadOnlyDictionary<string, ArchitectureVersionedContractSurfaceIsolationSurface> surfaces,
        IReadOnlyList<Type> targetUniverse,
        List<ArchitectureApplicabilityReason> reasons,
        ArchitectureApplicabilityProvenance provenance)
    {
        var indexesByTarget = new Dictionary<ArchitectureContractExposureTarget, List<int>>();
        for (int index = 0; index < contract.ForbiddenSurfaces.Count; index++)
        {
            ArchitectureVersionedContractSurfaceIsolationSurface surface = surfaces[contract.ForbiddenSurfaces[index]];
            Type[] matches = MatchSurface(context, contract, surface, targetUniverse);
            if (matches.Length == 0)
            {
                reasons.Add(Reason(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput, provenance));
                continue;
            }

            foreach (Type type in matches)
            {
                ArchitectureContractExposureTarget target = TargetIdentity(type);
                if (!indexesByTarget.TryGetValue(target, out List<int>? indexes))
                {
                    indexes = new List<int>();
                    indexesByTarget.Add(target, indexes);
                }

                indexes.Add(index);
            }
        }

        return indexesByTarget.ToDictionary(item => item.Key, item => item.Value.ToArray());
    }

    private static Type[] MatchSurface(
        ArchitectureCheckerContext context,
        ArchitectureVersionedContractSurfaceIsolationContract contract,
        ArchitectureVersionedContractSurfaceIsolationSurface surface,
        IEnumerable<Type> types) =>
        types.Where(type => ArchitecturePublicApiSurfaceSelectorMatcher.Matches(
                type,
                surface.TypesMatching,
                context.Document,
                contract.Name,
                context.RoleIndex))
            .Distinct()
            .OrderBy(TypeIdentity, StringComparer.Ordinal)
            .ToArray();

    private static ArchitectureApplicabilityReason Reason(
        string code,
        ArchitectureApplicabilityProvenance provenance) => new(code, provenance);

    private static ArchitectureContractExposureTarget TargetIdentity(Type type) =>
        new(AssemblyIdentity(type.Assembly), type.FullName ?? type.Name);

    private static string TypeIdentity(Type type) =>
        $"{AssemblyIdentity(type.Assembly)}\u001f{type.FullName ?? type.Name}";

    private static string AssemblyIdentity(Assembly assembly) => assembly.FullName ?? assembly.GetName().Name ?? string.Empty;
}
