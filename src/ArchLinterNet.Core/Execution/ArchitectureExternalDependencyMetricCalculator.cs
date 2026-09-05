using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Recovers the topology subject for each cached external-dependency fact and emits the matching
// group names. External facts and source completeness are already session-owned; this calculator
// only binds them to the supplied metric topology projection.
internal static class ArchitectureExternalDependencyMetricCalculator
{
    // The method remains a focused seam for tests of source identity recovery. Its result is raw
    // evidence; ArchitectureMetricEvaluator owns normalization and final metric construction.
    internal static ArchitectureMetricRawEvidence ExternalGroups(
        ArchitectureAnalysisSession session,
        ArchitectureTopologyEvaluator.Projection topology,
        string node,
        IReadOnlyList<ArchitectureExternalDependencyFact> facts,
        IReadOnlySet<Type>? incompleteSourceTypes = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(facts);

        Dictionary<string, ArchitectureTopologyEvaluator.SubjectClassification[]> classificationsByIdentity =
            topology.Classifications
                .GroupBy(classification => classification.Subject.Identity, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        List<string> reasons = new();
        List<string> contributors = new();
        if (incompleteSourceTypes != null && incompleteSourceTypes.Any(sourceType =>
                FindClassifications(classificationsByIdentity, topology, sourceType, session).Any(source =>
                    source.Disposition == ArchitectureTopologyEvaluator.Disposition.Mapped
                    && source.NodeIds.Contains(node, StringComparer.Ordinal))))
        {
            reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
            return new ArchitectureMetricRawEvidence(node, null, reasons, contributors);
        }

        foreach (ArchitectureExternalDependencyFact fact in facts)
        {
            foreach (ArchitectureTopologyEvaluator.SubjectClassification source in FindClassifications(
                         classificationsByIdentity, topology, fact.SourceType, session))
            {
                if (source.Disposition == ArchitectureTopologyEvaluator.Disposition.Mapped
                    && source.NodeIds.Contains(node, StringComparer.Ordinal))
                {
                    if (topology.Topology.SubjectKind == "project"
                        && !ArchitectureTopologyMetricCalculator.HasCanonicalProjectOwner(session, source.Subject))
                    {
                        reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
                    }
                    else
                    {
                        contributors.Add(fact.GroupName);
                    }
                }
            }
        }

        return new ArchitectureMetricRawEvidence(node, null, reasons, contributors);
    }

    private static IEnumerable<ArchitectureTopologyEvaluator.SubjectClassification> FindClassifications(
        IReadOnlyDictionary<string, ArchitectureTopologyEvaluator.SubjectClassification[]> classificationsByIdentity,
        ArchitectureTopologyEvaluator.Projection topology,
        Type sourceType,
        ArchitectureAnalysisSession session)
    {
        string identity = ResolveMetricSubjectIdentity(topology, sourceType, session);
        return classificationsByIdentity.TryGetValue(identity, out ArchitectureTopologyEvaluator.SubjectClassification[]? classifications)
            ? classifications
            : Array.Empty<ArchitectureTopologyEvaluator.SubjectClassification>();
    }

    private static string ResolveMetricSubjectIdentity(
        ArchitectureTopologyEvaluator.Projection topology,
        Type sourceType,
        ArchitectureAnalysisSession session)
    {
        string fullTypeName = ArchitectureTypeNames.SafeFullName(sourceType);
        string project = ArchitectureTopologyMetricObserver.ResolveProjectForMetric(session, sourceType);
        string assembly = ArchitectureTypeNames.SafeAssemblyName(sourceType) ?? string.Empty;
        string canonicalAssemblyIdentity = ArchitectureTopologyMetricObserver.ResolveCanonicalAssemblyIdentity(sourceType);
        string subject = topology.Topology.SubjectKind switch
        {
            "type" => fullTypeName,
            "namespace" => ArchitectureTypeNames.SafeNamespace(sourceType),
            "project" => project,
            "assembly" => ArchitectureTypeNames.SafeAssemblyName(sourceType) ?? string.Empty,
            _ => string.Empty,
        };
        return ArchitectureTopologyMetricObserver.BuildMetricSubjectIdentity(
            topology.Topology.SubjectKind,
            project,
            assembly,
            canonicalAssemblyIdentity,
            subject);
    }
}
