using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution.Expressions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using ArchLinterNet.Core.Scanning;

namespace ArchLinterNet.Core.Execution;

// Evaluates the policy-owned declared topology over the same session facts used by validation.
// It intentionally has no YAML or output dependency: policy loading owns declaration validity and
// Reporting owns presentation. This class only creates canonical native evidence and ordinary
// violations for the executor to transport through the established result seams.
internal static class ArchitectureTopologyEvaluator
{
    internal const string Family = "declared_topology";
    internal const string ControlIdentity = "declared-topology";
    private const string PolicyIdentity = "topology";

    // Kept internal for focused deterministic tests. Session observation and policy matching remain
    // separate so ordering/cardinality tests never need to construct real assemblies.
    internal static Result Evaluate(
        ArchitectureAnalysisSession? session,
        ArchitectureTopology topology,
        IReadOnlyList<ArchitectureTopologyObservedSubject> observedSubjects,
        IReadOnlyList<ArchitectureTopologyObservedDependency> observedDependencies,
        IReadOnlySet<string>? incompleteDependencySourceIdentities = null)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(observedSubjects);
        ArgumentNullException.ThrowIfNull(observedDependencies);

        Projection projection = Project(
            session,
            topology,
            observedSubjects,
            observedDependencies,
            incompleteDependencySourceIdentities);
        List<SubjectClassification> classifications = projection.Classifications.ToList();

        Dictionary<string, SubjectClassification> classificationsByIdentity = classifications
            .ToDictionary(classification => classification.Subject.Identity, StringComparer.Ordinal);
        Dictionary<string, string> nodeBySubject = classifications
            .Where(classification => classification.Disposition == Disposition.Mapped)
            .ToDictionary(classification => classification.Subject.Identity, classification => classification.NodeIds[0], StringComparer.Ordinal);

        List<Relationship> relationships = BuildRelationships(projection.Dependencies, nodeBySubject);
        HashSet<(string Source, string Target)> allowedEdges = topology.AllowedEdges
            .Select(edge => (edge.From, edge.To))
            .ToHashSet();
        List<ArchitectureTopologyRelationEvidence> relationshipEvidence = relationships
            .Select(relationship => new ArchitectureTopologyRelationEvidence(
                relationship.SourceNode,
                relationship.TargetNode,
                relationship.Witness,
                allowedEdges.Contains((relationship.SourceNode, relationship.TargetNode))))
            .ToList();

        // Relationship enforcement can only use exactly mapped endpoints, but that projection is
        // not enough authority to infer declaration drift. An unresolved subject might correspond
        // to one or more declared nodes or edges, so stale facts are supported only after the
        // whole declared universe is mapped exactly (or explicitly reviewed out of scope) and a
        // required universe did not resolve to zero subjects.
        bool missingRequiredUniverse = string.Equals(topology.Mode, "exhaustive", StringComparison.Ordinal)
            && classifications.Count == 0
            && !topology.Scope.AllowEmpty;
        bool mappingComplete = !missingRequiredUniverse && classifications.All(classification =>
            classification.Disposition is Disposition.Mapped or Disposition.ReviewedOutOfScope);
        bool canInferDrift = topology.StaleDeclarations && mappingComplete;
        List<string> staleNodes = canInferDrift
            ? topology.Nodes
                .Where(node => !nodeBySubject.Values.Contains(node.Id, StringComparer.Ordinal))
                .Select(node => node.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList()
            : new List<string>();
        List<ArchitectureTopologyStaleEdgeEvidence> staleEdges = canInferDrift
            ? topology.AllowedEdges
                .Where(edge => !relationships.Any(relationship =>
                    string.Equals(relationship.SourceNode, edge.From, StringComparison.Ordinal)
                    && string.Equals(relationship.TargetNode, edge.To, StringComparison.Ordinal)))
                .Select(edge => new ArchitectureTopologyStaleEdgeEvidence(edge.From, edge.To))
                .OrderBy(edge => edge.SourceNode, StringComparer.Ordinal)
                .ThenBy(edge => edge.TargetNode, StringComparer.Ordinal)
                .ToList()
            : new List<ArchitectureTopologyStaleEdgeEvidence>();

        ArchitectureTopologyMappingEvidence evidence = new(
            topology.Mode,
            topology.SubjectKind,
            topology.Nodes.Count,
            classifications.Select(ToEvidence).ToArray(),
            relationshipEvidence,
            staleNodes,
            staleEdges);

        ArchitectureApplicabilityMembership membership = string.Equals(topology.Mode, "exhaustive", StringComparison.Ordinal)
            ? ArchitectureApplicabilityMembership.Required
            : ArchitectureApplicabilityMembership.Optional;
        ArchitectureApplicabilityProvenance provenance = new(Family, ControlIdentity, PolicyIdentity);
        List<ArchitectureApplicabilityReason> reasons = BuildReasons(topology, classifications, staleNodes, staleEdges, provenance);
        ArchitectureApplicabilityRecordState state = reasons.Count == 0
            ? ArchitectureApplicabilityRecordState.Evaluable
            : ArchitectureApplicabilityRecordState.Unassessable;
        var record = new ArchitectureApplicabilityRecord(ControlIdentity, Family, state, reasons, provenance)
        {
            TopologyEvidence = evidence,
        };
        var expected = new ArchitectureApplicabilityExpectedEntry(ControlIdentity, Family, membership, provenance);

        List<ArchitectureViolation> violations = BuildViolations(
            topology, classifications, relationships, allowedEdges, staleNodes, staleEdges);
        return new Result(violations, new[] { expected }, new[] { record })
        {
            FactProjection = projection,
        };
    }

    internal static Projection Project(
        ArchitectureAnalysisSession? session,
        ArchitectureTopology topology,
        IReadOnlyList<ArchitectureTopologyObservedSubject> observedSubjects,
        IReadOnlyList<ArchitectureTopologyObservedDependency> observedDependencies,
        IReadOnlySet<string>? incompleteDependencySourceIdentities = null)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(observedSubjects);
        ArgumentNullException.ThrowIfNull(observedDependencies);

        List<SubjectClassification> classifications = observedSubjects
            .Where(subject => topology.Scope.Selectors.Any(selector => Matches(session, topology, selector, subject)))
            .OrderBy(subject => subject.Identity, StringComparer.Ordinal)
            .Select(subject => Classify(session, topology, subject))
            .ToList();
        return new Projection(
            topology,
            observedSubjects,
            classifications,
            observedDependencies,
            incompleteDependencySourceIdentities ?? new HashSet<string>(StringComparer.Ordinal));
    }

    private static SubjectClassification Classify(
        ArchitectureAnalysisSession? session,
        ArchitectureTopology topology,
        ArchitectureTopologyObservedSubject subject)
    {
        ArchitectureTopologyOutOfScopeDeclaration? exclusion = topology.OutOfScope
            .OrderBy(entry => entry.Id, StringComparer.Ordinal)
            .FirstOrDefault(entry => Matches(session, topology, entry.Selector, subject));
        if (exclusion is not null)
        {
            return new SubjectClassification(subject, Disposition.ReviewedOutOfScope, Array.Empty<string>(), exclusion.Id);
        }

        string[] nodes = topology.Nodes
            .Where(node => node.Mappings.Any(selector => Matches(session, topology, selector, subject)))
            .Select(node => node.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        return nodes.Length switch
        {
            0 => new SubjectClassification(subject, Disposition.Unmapped, nodes, null),
            1 => new SubjectClassification(subject, Disposition.Mapped, nodes, null),
            _ => new SubjectClassification(subject, Disposition.Ambiguous, nodes, null),
        };
    }

    private static bool Matches(
        ArchitectureAnalysisSession? session,
        ArchitectureTopology topology,
        ArchitectureTopologySubjectSelector selector,
        ArchitectureTopologyObservedSubject subject)
    {
        if (!string.IsNullOrEmpty(selector.Layer))
        {
            return session is not null
                && subject.Type is not null
                && session.Document.Layers.TryGetValue(selector.Layer, out ArchitectureLayer? layer)
                && session.Facts.MatchesLayer(layer, subject.Type);
        }

        if (!string.IsNullOrEmpty(selector.Namespace))
        {
            string namespaceName = topology.SubjectKind == "type"
                ? ArchitectureTypeNames.SafeNamespace(subject.Type!)
                : subject.Subject;
            return MatchesNamespace(selector, namespaceName);
        }

        if (!string.IsNullOrEmpty(selector.Project))
        {
            return string.Equals(
                selector.Project,
                subject.ProjectSelectorIdentity ?? subject.Project,
                StringComparison.Ordinal);
        }

        if (!string.IsNullOrEmpty(selector.Assembly))
        {
            return string.Equals(selector.Assembly, subject.Assembly, StringComparison.Ordinal);
        }

        return selector.Context is not null
            && session is not null
            && subject.Type is not null
            && ArchitectureContextSelectorMatcher.Matches(
                selector.Context, subject.Type, session.RoleIndex, sourceDescriptor: null,
                session.ExpressionFacts, sourceType: null);
    }

    private static bool MatchesNamespace(ArchitectureTopologySubjectSelector selector, string namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return false;
        }

        NamespaceGlobPattern pattern = selector.NamespacePattern;
        bool prefixMatches = pattern.IsGlob
            ? pattern.Match(namespaceName).Matched
            : ArchitectureLayerResolver.MatchesPrefix(namespaceName, selector.Namespace);
        if (!prefixMatches || string.IsNullOrEmpty(selector.NamespaceSuffix))
        {
            return prefixMatches;
        }

        if (!pattern.IsGlob)
        {
            return namespaceName.EndsWith("." + selector.NamespaceSuffix, StringComparison.Ordinal);
        }

        string[] namespaceSegments = namespaceName.Split('.');
        string[] selectorSegments = selector.Namespace.Split('.');
        string[] suffixSegments = selector.NamespaceSuffix.Split('.');
        if (namespaceSegments.Length < selectorSegments.Length + suffixSegments.Length)
        {
            return false;
        }

        return suffixSegments.Select((segment, index) =>
                string.Equals(namespaceSegments[selectorSegments.Length + index], segment, StringComparison.Ordinal))
            .All(matches => matches);
    }

    private static List<Relationship> BuildRelationships(
        IReadOnlyList<ArchitectureTopologyObservedDependency> dependencies,
        IReadOnlyDictionary<string, string> nodeBySubject)
    {
        return dependencies
            .Where(dependency => nodeBySubject.ContainsKey(dependency.SourceIdentity)
                && nodeBySubject.ContainsKey(dependency.TargetIdentity))
            .Select(dependency => new
            {
                SourceNode = nodeBySubject[dependency.SourceIdentity],
                TargetNode = nodeBySubject[dependency.TargetIdentity],
                dependency.Witness,
            })
            .Where(dependency => !string.Equals(dependency.SourceNode, dependency.TargetNode, StringComparison.Ordinal))
            .GroupBy(dependency => (dependency.SourceNode, dependency.TargetNode))
            .Select(group => group
                .OrderBy(dependency => dependency.Witness, StringComparer.Ordinal)
                .Select(dependency => new Relationship(dependency.SourceNode, dependency.TargetNode, dependency.Witness))
                .First())
            .OrderBy(relationship => relationship.SourceNode, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetNode, StringComparer.Ordinal)
            .ToList();
    }

    private static List<ArchitectureApplicabilityReason> BuildReasons(
        ArchitectureTopology topology,
        IReadOnlyList<SubjectClassification> classifications,
        IReadOnlyList<string> staleNodes,
        IReadOnlyList<ArchitectureTopologyStaleEdgeEvidence> staleEdges,
        ArchitectureApplicabilityProvenance provenance)
    {
        var reasonCodes = new HashSet<string>(StringComparer.Ordinal);
        bool exhaustive = string.Equals(topology.Mode, "exhaustive", StringComparison.Ordinal);
        if (exhaustive && classifications.Count == 0 && !topology.Scope.AllowEmpty)
        {
            reasonCodes.Add(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput);
        }

        if (exhaustive && classifications.Any(classification => classification.Disposition == Disposition.Unmapped))
        {
            reasonCodes.Add(ArchitectureApplicabilityReasonCodes.UnmappedSubject);
        }

        if (classifications.Any(classification => classification.Disposition == Disposition.Ambiguous))
        {
            reasonCodes.Add(ArchitectureApplicabilityReasonCodes.AmbiguousSubject);
        }

        if (staleNodes.Count > 0 || staleEdges.Count > 0)
        {
            reasonCodes.Add(ArchitectureApplicabilityReasonCodes.StaleDeclaration);
        }

        return reasonCodes.OrderBy(code => code, StringComparer.Ordinal)
            .Select(code => new ArchitectureApplicabilityReason(code, provenance))
            .ToList();
    }

    private static List<ArchitectureViolation> BuildViolations(
        ArchitectureTopology topology,
        IReadOnlyList<SubjectClassification> classifications,
        IReadOnlyList<Relationship> relationships,
        IReadOnlySet<(string Source, string Target)> allowedEdges,
        IReadOnlyList<string> staleNodes,
        IReadOnlyList<ArchitectureTopologyStaleEdgeEvidence> staleEdges)
    {
        var violations = new List<ArchitectureViolation>();
        foreach (SubjectClassification classification in classifications.Where(item => item.Disposition == Disposition.Ambiguous))
        {
            violations.Add(new ArchitectureViolation(
                "topology structural mapping", ControlIdentity, classification.Subject.Identity,
                "ambiguous topology component mapping", classification.NodeIds));
        }

        foreach (Relationship relationship in relationships.Where(relationship =>
                     !allowedEdges.Contains((relationship.SourceNode, relationship.TargetNode))))
        {
            violations.Add(new ArchitectureViolation(
                "topology declared relationship", ControlIdentity, relationship.SourceNode,
                relationship.TargetNode, new[] { relationship.Witness }));
        }

        foreach (string node in staleNodes)
        {
            violations.Add(new ArchitectureViolation(
                "topology declaration drift", ControlIdentity, node,
                "stale topology node", new[] { node }));
        }

        foreach (ArchitectureTopologyStaleEdgeEvidence edge in staleEdges)
        {
            violations.Add(new ArchitectureViolation(
                "topology declaration drift", ControlIdentity, edge.SourceNode,
                "stale topology edge", new[] { edge.TargetNode }));
        }

        return violations;
    }

    private static ArchitectureTopologySubjectEvidence ToEvidence(SubjectClassification classification) => new(
        classification.Subject.Identity,
        classification.Subject.Project,
        classification.Subject.Assembly,
        classification.Subject.Subject,
        classification.Disposition switch
        {
            Disposition.Mapped => "mapped",
            Disposition.ReviewedOutOfScope => "reviewed_out_of_scope",
            Disposition.Unmapped => "unmapped",
            Disposition.Ambiguous => "ambiguous",
            _ => throw new ArgumentOutOfRangeException(),
        },
        classification.NodeIds,
        classification.ReviewedOutOfScopeId);

    internal sealed record Result(
        IReadOnlyList<ArchitectureViolation> Violations,
        IReadOnlyList<ArchitectureApplicabilityExpectedEntry> ExpectedEntries,
        IReadOnlyList<ArchitectureApplicabilityRecord> Records)
    {
        internal Projection? FactProjection { get; init; }

        public static Result Empty { get; } = new(
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<ArchitectureApplicabilityExpectedEntry>(),
            Array.Empty<ArchitectureApplicabilityRecord>());
    }

    internal sealed record SubjectClassification(
        ArchitectureTopologyObservedSubject Subject,
        Disposition Disposition,
        IReadOnlyList<string> NodeIds,
        string? ReviewedOutOfScopeId);

    private sealed record Relationship(string SourceNode, string TargetNode, string Witness);

    internal enum Disposition
    {
        Mapped,
        ReviewedOutOfScope,
        Unmapped,
        Ambiguous,
    }

    internal sealed record Projection(
        ArchitectureTopology Topology,
        IReadOnlyList<ArchitectureTopologyObservedSubject> ObservedSubjects,
        IReadOnlyList<SubjectClassification> Classifications,
        IReadOnlyList<ArchitectureTopologyObservedDependency> Dependencies,
        IReadOnlySet<string> IncompleteDependencySourceIdentities);
}
