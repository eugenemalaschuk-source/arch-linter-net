using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Calculates metric evidence that comes from the topology projection. The projection and session
// are the sole inputs; this collaborator does not build a graph or measurement-level models.
internal static class ArchitectureTopologyMetricCalculator
{
    internal static ArchitectureMetricRawEvidence Calculate(
        ArchitectureAnalysisSession session,
        ArchitectureMetricDefinition definition,
        ArchitectureTopologyEvaluator.Projection topology,
        string node)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(topology);

        ArchitectureTopologyEvaluator.SubjectClassification[] scoped = topology.Classifications
            .Where(classification => classification.NodeIds.Contains(node, StringComparer.Ordinal))
            .ToArray();
        return definition.Kind switch
        {
            ArchitectureMetricKinds.TopologyTypeCount => TypeCount(node, scoped),
            ArchitectureMetricKinds.ComponentFootprintCount => ComponentFootprint(session, definition, node, scoped),
            ArchitectureMetricKinds.IncomingComponentCount or ArchitectureMetricKinds.OutgoingComponentCount =>
                ComponentRelations(session, topology, node, definition.Kind),
            _ => throw new ArgumentException($"Unsupported topology metric kind '{definition.Kind}'.", nameof(definition)),
        };
    }

    private static ArchitectureMetricRawEvidence TypeCount(
        string node,
        IReadOnlyCollection<ArchitectureTopologyEvaluator.SubjectClassification> scoped)
    {
        List<string> contributors = scoped
            .Where(classification => classification.Disposition == ArchitectureTopologyEvaluator.Disposition.Mapped)
            .Select(classification => classification.Subject.Identity)
            .ToList();
        return new ArchitectureMetricRawEvidence(node, null, Array.Empty<string>(), contributors);
    }

    private static ArchitectureMetricRawEvidence ComponentFootprint(
        ArchitectureAnalysisSession session,
        ArchitectureMetricDefinition definition,
        string node,
        IReadOnlyCollection<ArchitectureTopologyEvaluator.SubjectClassification> scoped)
    {
        List<string> reasons = new();
        List<string> contributors = new();
        foreach (ArchitectureTopologyEvaluator.SubjectClassification classification in scoped)
        {
            if (classification.Disposition != ArchitectureTopologyEvaluator.Disposition.Mapped)
            {
                continue;
            }

            string? owner = definition.Unit == "project"
                ? ResolveCanonicalProjectOwner(session, classification.Subject)
                : classification.Subject.CanonicalAssemblyIdentity;
            if (string.IsNullOrWhiteSpace(owner))
            {
                reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
            }
            else
            {
                contributors.Add(owner);
            }
        }

        return new ArchitectureMetricRawEvidence(node, definition.Unit, reasons, contributors);
    }

    private static ArchitectureMetricRawEvidence ComponentRelations(
        ArchitectureAnalysisSession session,
        ArchitectureTopologyEvaluator.Projection topology,
        string node,
        string kind)
    {
        IReadOnlyList<ArchitectureTopologyObservedDependency> dependencies = topology.Dependencies;
        Dictionary<string, ArchitectureTopologyEvaluator.SubjectClassification> classes =
            topology.Classifications.ToDictionary(
                classification => classification.Subject.Identity,
                StringComparer.Ordinal);
        List<string> reasons = new();
        List<string> contributors = new();
        bool outgoing = kind == ArchitectureMetricKinds.OutgoingComponentCount;
        bool hasIncompleteRequiredSource = topology.IncompleteDependencySourceIdentities.Any(identity =>
            classes.TryGetValue(identity, out ArchitectureTopologyEvaluator.SubjectClassification? source)
            && source.Disposition == ArchitectureTopologyEvaluator.Disposition.Mapped
            && (!outgoing || source.NodeIds.Contains(node, StringComparer.Ordinal)));
        if (hasIncompleteRequiredSource)
        {
            // An omitted direct edge from a selected outgoing source or any mapped incoming source
            // can change this component's relation universe. Do not retain known edges once their
            // required evidence is incomplete.
            reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
            return new ArchitectureMetricRawEvidence(node, null, reasons, contributors);
        }

        foreach (ArchitectureTopologyObservedDependency dependency in dependencies)
        {
            string selectedIdentity = outgoing ? dependency.SourceIdentity : dependency.TargetIdentity;
            string otherIdentity = outgoing ? dependency.TargetIdentity : dependency.SourceIdentity;
            ArchitectureTopologyAssemblyEndpointBinding selectedBinding = outgoing
                ? dependency.SourceBinding
                : dependency.TargetBinding;
            string? selectedAssemblyName = outgoing
                ? dependency.SourceAssemblyName
                : dependency.TargetAssemblyName;
            if (selectedBinding == ArchitectureTopologyAssemblyEndpointBinding.Ambiguous
                && CouldBeSelectedNode(topology, selectedAssemblyName, node))
            {
                // An ambiguous endpoint cannot enter the exact classification map. If one of its
                // candidates belongs to this node, skipping it would manufacture a trusted zero.
                reasons.Add(ArchitectureApplicabilityReasonCodes.AmbiguousSubject);
                continue;
            }

            if (!classes.TryGetValue(selectedIdentity, out ArchitectureTopologyEvaluator.SubjectClassification? selected))
            {
                continue;
            }

            if (selected.Disposition != ArchitectureTopologyEvaluator.Disposition.Mapped
                || !selected.NodeIds.Contains(node, StringComparer.Ordinal))
            {
                continue;
            }

            ArchitectureTopologyAssemblyEndpointBinding otherBinding = outgoing
                ? dependency.TargetBinding
                : dependency.SourceBinding;
            if (otherBinding == ArchitectureTopologyAssemblyEndpointBinding.Ambiguous)
            {
                reasons.Add(ArchitectureApplicabilityReasonCodes.AmbiguousSubject);
                continue;
            }

            if (otherBinding == ArchitectureTopologyAssemblyEndpointBinding.Missing)
            {
                reasons.Add(ArchitectureApplicabilityReasonCodes.UnmappedSubject);
                continue;
            }

            // An endpoint excluded from the classification projection was not explicitly reviewed
            // out of scope. Treating it as absent would make a partial count appear trustworthy.
            if (!classes.TryGetValue(otherIdentity, out ArchitectureTopologyEvaluator.SubjectClassification? other))
            {
                reasons.Add(ArchitectureApplicabilityReasonCodes.UnmappedSubject);
                continue;
            }

            if (topology.Topology.SubjectKind == "project"
                && (!HasCanonicalProjectOwner(session, selected.Subject)
                    || !HasCanonicalProjectOwner(session, other.Subject)))
            {
                reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
                continue;
            }

            if (other.Disposition == ArchitectureTopologyEvaluator.Disposition.Unmapped)
            {
                reasons.Add(ArchitectureApplicabilityReasonCodes.UnmappedSubject);
                continue;
            }

            if (other.Disposition == ArchitectureTopologyEvaluator.Disposition.Ambiguous)
            {
                reasons.Add(ArchitectureApplicabilityReasonCodes.AmbiguousSubject);
                continue;
            }

            if (other.Disposition == ArchitectureTopologyEvaluator.Disposition.ReviewedOutOfScope)
            {
                continue;
            }

            foreach (string targetNode in other.NodeIds)
            {
                if (!string.Equals(targetNode, node, StringComparison.Ordinal))
                {
                    contributors.Add(targetNode);
                }
            }
        }

        return new ArchitectureMetricRawEvidence(node, null, reasons, contributors);
    }

    private static bool CouldBeSelectedNode(
        ArchitectureTopologyEvaluator.Projection topology,
        string? assemblyName,
        string node) =>
        !string.IsNullOrEmpty(assemblyName)
        && topology.Classifications.Any(classification =>
            string.Equals(classification.Subject.Assembly, assemblyName, StringComparison.Ordinal)
            && classification.NodeIds.Contains(node, StringComparer.Ordinal));

    // Project ownership must be resolved from the discovered artifact that supplied the observed
    // subject. A same-simple-name assembly loaded by the host is not sufficient evidence.
    internal static bool HasCanonicalProjectOwner(
        ArchitectureAnalysisSession session,
        ArchitectureTopologyObservedSubject subject) =>
        subject.ResolvedAssembly is not null
        && session.Facts.TryGetProjectByResolvedAssembly(subject.ResolvedAssembly, out var project)
        && !session.Facts.HasAmbiguousProjectOutputAssemblyName(project.AssemblyName);

    internal static string? ResolveCanonicalProjectOwner(
        ArchitectureAnalysisSession session,
        ArchitectureTopologyObservedSubject subject) =>
        HasCanonicalProjectOwner(session, subject)
        && session.Facts.TryGetProjectByResolvedAssembly(subject.ResolvedAssembly!, out var project)
            ? ProjectPathNormalizer.Normalize(project.Path)
            : null;

    internal static bool HasAmbiguousProjectSelectorIdentity(
        ArchitectureTopology topology,
        string nodeId,
        IEnumerable<ArchitectureTopologyEvaluator.SubjectClassification> classifications) =>
        topology.Nodes
            .Where(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal))
            .SelectMany(node => node.Mappings)
            .Where(selector => !string.IsNullOrEmpty(selector.Project))
            .Any(selector => classifications
                .Where(classification => string.Equals(
                    classification.Subject.ProjectSelectorIdentity ?? classification.Subject.Project,
                    selector.Project,
                    StringComparison.Ordinal))
                .Select(classification => classification.Subject.Project)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any());
}
