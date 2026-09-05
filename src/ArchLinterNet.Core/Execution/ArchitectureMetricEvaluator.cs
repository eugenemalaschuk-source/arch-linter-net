using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Scanning;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Execution;

// The sole measurement authority. It consumes the session's cached type/reference/public-surface
// facts and the topology evaluator's projection; it never builds a second graph or scanner.
internal static class ArchitectureMetricEvaluator
{
    internal const string Family = "metrics";

    internal static ArchitectureMetricMeasurementOutcome Evaluate(
        ArchitectureAnalysisSession session,
        IReadOnlyCollection<ArchitectureMetricDefinition> definitions,
        IReadOnlyCollection<string>? selectedIds = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definitions);

        return Evaluate(session, definitions, ArchitectureTopologyMetricObserver.Evaluate(session), selectedIds);
    }

    // This narrow overload keeps the evaluator testable against the same topology projection that
    // production receives, including a dependency endpoint that cannot bind to one subject.
    internal static ArchitectureMetricMeasurementOutcome Evaluate(
        ArchitectureAnalysisSession session,
        IReadOnlyCollection<ArchitectureMetricDefinition> definitions,
        ArchitectureTopologyEvaluator.Result topologyResult,
        IReadOnlyCollection<string>? selectedIds = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(topologyResult);

        ArchitectureMetricDefinition[] selected = SelectDefinitions(definitions, selectedIds);
        if (selected.Length == 0)
        {
            return new ArchitectureMetricMeasurementOutcome(
                Array.Empty<ArchitectureMetricMeasurement>(), null, null);
        }

        var measurements = new List<ArchitectureMetricMeasurement>(selected.Length);
        var expected = new List<ArchitectureApplicabilityExpectedEntry>(selected.Length);
        var records = new List<ArchitectureApplicabilityRecord>(selected.Length);

        ArchitectureTopologyEvaluator.Projection? topology = topologyResult.FactProjection;
        ArchitectureTopologyMappingEvidence? topologyEvidence = topologyResult.Records.FirstOrDefault()?.TopologyEvidence;

        foreach (ArchitectureMetricDefinition definition in selected)
        {
            ArchitectureApplicabilityProvenance provenance =
                new(Family, definition.Id, session.Document.Name);
            MetricResult result = RequiresCompleteTypeUniverse(definition, topology)
                && !session.TypeIndex.HasCompleteTypeUniverse
                ? Unassessable(
                    definition,
                    definition.TopologyNode ?? definition.PublicApiSurface ?? string.Empty,
                    provenance,
                    ArchitectureApplicabilityReasonCodes.MissingRequiredInput)
                : EvaluateDefinition(session, definition, topology, topologyEvidence, provenance);
            records.Add(result.Record);
            expected.Add(new ArchitectureApplicabilityExpectedEntry(
                definition.Id, Family, ArchitectureApplicabilityMembership.Required, provenance));
            measurements.Add(result.Measurement);
        }

        ArchitectureAssessmentCompletionEvidence? completion = ArchitectureApplicabilityEvaluator.Evaluate(
            expected, records, conformancePassed: true);
        ArchitectureApplicabilityProjection? projection = ArchitectureApplicabilityProjector.Project(completion);
        return new ArchitectureMetricMeasurementOutcome(measurements, completion, projection);
    }

    // Assembly component relations and assembly footprint use the assembly-metadata graph, whose
    // native universe is independent of Assembly.GetTypes(). Every other topology metric consumes
    // type/reflection facts; those cannot retain a trusted known subset after a type-load failure.
    private static bool RequiresCompleteTypeUniverse(
        ArchitectureMetricDefinition definition,
        ArchitectureTopologyEvaluator.Projection? topology)
    {
        if (definition.Kind == ArchitectureMetricKinds.PublicContractSurfaceCount || topology is null)
        {
            return false;
        }

        return topology.Topology.SubjectKind != "assembly"
            || definition.Kind == ArchitectureMetricKinds.ExternalDependencyGroupCount;
    }

    internal static ArchitectureMetricMeasurementOutcome Unavailable(
        IReadOnlyCollection<ArchitectureMetricDefinition> definitions,
        IReadOnlyCollection<string>? selectedIds,
        string policyIdentity,
        string reasonCode)
    {
        ArchitectureMetricDefinition[] selected = SelectDefinitions(definitions, selectedIds);
        var expected = new List<ArchitectureApplicabilityExpectedEntry>(selected.Length);
        var records = new List<ArchitectureApplicabilityRecord>(selected.Length);
        var measurements = new List<ArchitectureMetricMeasurement>(selected.Length);
        foreach (ArchitectureMetricDefinition definition in selected)
        {
            ArchitectureApplicabilityProvenance provenance = new(Family, definition.Id, policyIdentity);
            MetricResult result = Unassessable(definition, definition.TopologyNode ?? definition.PublicApiSurface ?? string.Empty,
                provenance, reasonCode);
            expected.Add(new ArchitectureApplicabilityExpectedEntry(
                definition.Id, Family, ArchitectureApplicabilityMembership.Required, provenance));
            records.Add(result.Record);
            measurements.Add(result.Measurement);
        }

        ArchitectureAssessmentCompletionEvidence? completion = ArchitectureApplicabilityEvaluator.Evaluate(
            expected, records, conformancePassed: true);
        return new ArchitectureMetricMeasurementOutcome(
            measurements, completion, ArchitectureApplicabilityProjector.Project(completion));
    }

    internal static ArchitectureMetricDefinition[] SelectDefinitions(
        IReadOnlyCollection<ArchitectureMetricDefinition> definitions,
        IReadOnlyCollection<string>? selectedIds)
    {
        ArchitectureMetricDefinition[] ordered = definitions
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        if (selectedIds is not { Count: > 0 })
        {
            return ordered;
        }

        HashSet<string> requested = selectedIds.ToHashSet(StringComparer.Ordinal);
        string[] unknown = requested
            .Where(id => !ordered.Any(definition => string.Equals(definition.Id, id, StringComparison.Ordinal)))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException(
                $"Unknown metric IDs: {string.Join(", ", unknown)}.", nameof(selectedIds));
        }

        return ordered
            .Where(definition => requested.Contains(definition.Id))
            .ToArray();
    }

    private static MetricResult EvaluateDefinition(
        ArchitectureAnalysisSession session,
        ArchitectureMetricDefinition definition,
        ArchitectureTopologyEvaluator.Projection? topology,
        ArchitectureTopologyMappingEvidence? topologyEvidence,
        ArchitectureApplicabilityProvenance provenance)
    {
        if (definition.Kind == ArchitectureMetricKinds.PublicContractSurfaceCount)
        {
            return EvaluatePublicSurface(session, definition, provenance);
        }

        string scope = definition.TopologyNode ?? string.Empty;
        if (topology is null || topologyEvidence is null)
        {
            return Unassessable(definition, scope, provenance, ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
        }

        if (topology.Classifications.Count == 0)
        {
            return EmptyScopeResult(definition, scope, topologyEvidence, topology.Topology.Scope.AllowEmpty, provenance);
        }

        ArchitectureTopologyEvaluator.SubjectClassification[] classifications = topology.Classifications.ToArray();
        ArchitectureTopologyEvaluator.SubjectClassification[] scoped = classifications
            .Where(classification => classification.NodeIds.Contains(scope, StringComparer.Ordinal))
            .ToArray();
        if (scoped.Length == 0)
        {
            return EmptyScopeResult(definition, scope, topologyEvidence, topology.Topology.Scope.AllowEmpty, provenance);
        }

        List<string> reasons = new();
        if (scoped.Any(classification => classification.Disposition == ArchitectureTopologyEvaluator.Disposition.Ambiguous))
        {
            reasons.Add(ArchitectureApplicabilityReasonCodes.AmbiguousSubject);
        }

        if (topologyEvidence.StaleNodes.Contains(scope, StringComparer.Ordinal))
        {
            reasons.Add(ArchitectureApplicabilityReasonCodes.StaleDeclaration);
        }

        if (topology.Topology.SubjectKind == "project" && scoped.Any(classification =>
                !HasCanonicalProjectOwner(session, classification.Subject)))
        {
            reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
        }

        if (topology.Topology.SubjectKind == "project"
            && HasAmbiguousProjectSelectorIdentity(topology.Topology, scope, scoped))
        {
            // Topology policy keeps its backward-compatible project selector spelling (the output
            // assembly simple name), but a metric must not merge two artifact-derived owners that
            // that spelling cannot distinguish.
            reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
        }

        if (definition.Kind == ArchitectureMetricKinds.ComponentFootprintCount)
        {
            HashSet<string> contributors = new(StringComparer.Ordinal);
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

            return Finish(definition, scope, definition.Unit, reasons, contributors, provenance);
        }

        if (definition.Kind == ArchitectureMetricKinds.TopologyTypeCount)
        {
            HashSet<string> contributors = scoped
                .Where(classification => classification.Disposition == ArchitectureTopologyEvaluator.Disposition.Mapped)
                .Select(classification => classification.Subject.Identity)
                .ToHashSet(StringComparer.Ordinal);
            return Finish(definition, scope, null, reasons, contributors, provenance);
        }

        HashSet<string> relationContributors = definition.Kind == ArchitectureMetricKinds.ExternalDependencyGroupCount
            ? ExternalGroups(
                session,
                topology,
                scope,
                reasons,
                session.ExternalDependencyFacts.Facts,
                session.ExternalDependencyFacts.IncompleteSourceTypes)
            : ComponentRelations(session, topology, scope, definition.Kind, reasons);
        return Finish(definition, scope, null, reasons, relationContributors, provenance);
    }

    private static HashSet<string> ComponentRelations(
        ArchitectureAnalysisSession session,
        ArchitectureTopologyEvaluator.Projection topology,
        string node,
        string kind,
        ICollection<string> reasons)
    {
        IReadOnlyList<ArchitectureTopologyObservedDependency> dependencies = topology.Dependencies;

        Dictionary<string, ArchitectureTopologyEvaluator.SubjectClassification> classes =
            topology.Classifications.ToDictionary(classification => classification.Subject.Identity, StringComparer.Ordinal);
        HashSet<string> contributors = new(StringComparer.Ordinal);
        bool outgoing = kind == ArchitectureMetricKinds.OutgoingComponentCount;
        bool hasIncompleteRequiredSource = topology.IncompleteDependencySourceIdentities.Any(identity =>
            classes.TryGetValue(identity, out ArchitectureTopologyEvaluator.SubjectClassification? source)
            && source.Disposition == ArchitectureTopologyEvaluator.Disposition.Mapped
            && (!outgoing || source.NodeIds.Contains(node, StringComparer.Ordinal)));
        if (hasIncompleteRequiredSource)
        {
            // An omitted direct edge from a selected outgoing source or any mapped incoming
            // source can change this component's relation universe. Do not retain known edges as
            // contributors once their required evidence is incomplete.
            reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
            return contributors;
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
                // The synthetic identity for an ambiguous endpoint cannot enter the exact
                // classification map. If one of its retained candidates belongs to this node,
                // skipping it would manufacture a trusted zero for a relation that may belong
                // to the selected component.
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

            // A dependency endpoint excluded from the classification projection has not been
            // explicitly reviewed as out of scope. Treating it as absent would turn a partial
            // component count into a seemingly trustworthy lower value.
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

        return contributors;
    }

    private static bool CouldBeSelectedNode(
        ArchitectureTopologyEvaluator.Projection topology,
        string? assemblyName,
        string node) =>
        !string.IsNullOrEmpty(assemblyName)
        && topology.Classifications.Any(classification =>
            string.Equals(classification.Subject.Assembly, assemblyName, StringComparison.Ordinal)
            && classification.NodeIds.Contains(node, StringComparer.Ordinal));

    internal static HashSet<string> ExternalGroups(
        ArchitectureAnalysisSession session,
        ArchitectureTopologyEvaluator.Projection topology,
        string node,
        ICollection<string> reasons,
        IReadOnlyList<ArchitectureExternalDependencyFact> facts,
        IReadOnlySet<Type>? incompleteSourceTypes = null)
    {
        Dictionary<string, ArchitectureTopologyEvaluator.SubjectClassification[]> classificationsByIdentity =
            topology.Classifications
                .GroupBy(classification => classification.Subject.Identity, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        HashSet<string> contributors = new(StringComparer.Ordinal);
        if (incompleteSourceTypes != null && incompleteSourceTypes.Any(sourceType =>
                FindClassifications(classificationsByIdentity, topology, sourceType, session).Any(source =>
                    source.Disposition == ArchitectureTopologyEvaluator.Disposition.Mapped
                    && source.NodeIds.Contains(node, StringComparer.Ordinal))))
        {
            reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
            return contributors;
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
                        && !HasCanonicalProjectOwner(session, source.Subject))
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

        return contributors;
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
            topology.Topology.SubjectKind, project, assembly, canonicalAssemblyIdentity, subject);
    }

    private static bool HasCanonicalProjectOwner(
        ArchitectureAnalysisSession session,
        ArchitectureTopologyObservedSubject subject)
    {
        return subject.ResolvedAssembly is not null
               && session.Facts.TryGetProjectByResolvedAssembly(subject.ResolvedAssembly, out var project)
               && !session.Facts.HasAmbiguousProjectOutputAssemblyName(project.AssemblyName);
    }

    private static string? ResolveCanonicalProjectOwner(
        ArchitectureAnalysisSession session,
        ArchitectureTopologyObservedSubject subject) =>
        HasCanonicalProjectOwner(session, subject)
        && session.Facts.TryGetProjectByResolvedAssembly(subject.ResolvedAssembly!, out var project)
            ? ProjectPathNormalizer.Normalize(project.Path)
            : null;

    private static bool HasAmbiguousProjectSelectorIdentity(
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

    private static MetricResult EvaluatePublicSurface(
        ArchitectureAnalysisSession session,
        ArchitectureMetricDefinition definition,
        ArchitectureApplicabilityProvenance provenance)
    {
        string scope = definition.PublicApiSurface ?? string.Empty;
        ArchitecturePublicApiSurfaceContract[] candidates = session.Document.Contracts.StrictPublicApiSurface
            .Concat(session.Document.Contracts.AuditPublicApiSurface)
            .Where(candidate => string.Equals(candidate.Id, scope, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (candidates.Length != 1 || candidates[0].ApiSnapshotError is not null)
        {
            return Unassessable(definition, scope, provenance, ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
        }

        ArchitecturePublicApiSurfaceContract contract = candidates[0];

        if (!TryResolvePublicSurfaceAssemblyIdentities(session, contract, out IReadOnlyDictionary<string, string>? identities))
        {
            // The legacy public-surface configuration names assemblies by simple name. A metric
            // cannot safely turn that into a resolved contributor identity if the current target
            // set contains zero or multiple canonical assemblies for that name.
            return Unassessable(definition, scope, provenance, ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
        }

        IReadOnlyList<PublicApiSnapshotEntry> entries;
        IReadOnlyList<ArchitectureViolation> selectorSafety;
        IReadOnlyList<string> missing;
        bool isComplete;
        try
        {
            entries = session.CapturePublicApiSurface(contract, out missing, out selectorSafety, out isComplete);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Unassessable(definition, scope, provenance, ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
        }

        List<string> reasons = new();
        if (missing.Count > 0 || selectorSafety.Count > 0 || !isComplete)
        {
            reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
        }

        HashSet<string> contributors = new(StringComparer.Ordinal);
        foreach (PublicApiSnapshotEntry entry in entries)
        {
            if (!identities.TryGetValue(entry.AssemblyName, out string? identity))
            {
                reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
                continue;
            }

            contributors.Add($"{identity}|{entry.Signature}");
        }

        return Finish(definition, scope, null, reasons, contributors, provenance);
    }

    private static bool TryResolvePublicSurfaceAssemblyIdentities(
        ArchitectureAnalysisSession session,
        ArchitecturePublicApiSurfaceContract contract,
        out IReadOnlyDictionary<string, string> identities)
    {
        var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string assemblyName in contract.Assemblies.Distinct(StringComparer.Ordinal))
        {
            Assembly[] candidates = session.Context.TargetAssemblies
                .Where(candidate => string.Equals(candidate.GetName().Name, assemblyName, StringComparison.Ordinal))
                .ToArray();
            if (candidates.Length != 1)
            {
                identities = new Dictionary<string, string>(StringComparer.Ordinal);
                return false;
            }

            resolved.Add(assemblyName, ArchitectureTopologyMetricObserver.ResolveCanonicalAssemblyIdentity(candidates[0]));
        }

        identities = resolved;
        return true;
    }

    private static MetricResult Unassessable(
        ArchitectureMetricDefinition definition,
        string scope,
        ArchitectureApplicabilityProvenance provenance,
        string reasonCode)
    {
        return Finish(definition, scope, definition.Unit, [reasonCode], new HashSet<string>(StringComparer.Ordinal), provenance);
    }

    private static MetricResult EmptyScopeResult(
        ArchitectureMetricDefinition definition,
        string scope,
        ArchitectureTopologyMappingEvidence topologyEvidence,
        bool allowEmpty,
        ArchitectureApplicabilityProvenance provenance)
    {
        if (topologyEvidence.StaleNodes.Contains(scope, StringComparer.Ordinal))
        {
            return Unassessable(definition, scope, provenance, ArchitectureApplicabilityReasonCodes.StaleDeclaration);
        }

        if (!allowEmpty)
        {
            return Unassessable(definition, scope, provenance, ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput);
        }

        return Finish(
            definition,
            scope,
            definition.Kind == ArchitectureMetricKinds.ComponentFootprintCount ? definition.Unit : null,
            Array.Empty<string>(),
            Array.Empty<string>(),
            provenance);
    }

    private static MetricResult Finish(
        ArchitectureMetricDefinition definition,
        string scope,
        string? unit,
        IEnumerable<string> reasonCodes,
        IEnumerable<string> contributors,
        ArchitectureApplicabilityProvenance provenance)
    {
        string[] orderedReasons = reasonCodes
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        string[] orderedContributors = contributors
            .Distinct(StringComparer.Ordinal)
            .OrderBy(contributor => contributor, StringComparer.Ordinal)
            .ToArray();
        ArchitectureApplicabilityRecordState state = orderedReasons.Length == 0
            ? ArchitectureApplicabilityRecordState.Evaluable
            : ArchitectureApplicabilityRecordState.Unassessable;
        int? value = state == ArchitectureApplicabilityRecordState.Evaluable ? orderedContributors.Length : null;
        ArchitectureApplicabilityReason[] reasons = orderedReasons
            .Select(code => new ArchitectureApplicabilityReason(code, provenance))
            .ToArray();
        var evidence = new ArchitectureMetricEvidence(
            definition.Id,
            definition.Kind,
            definition.TopologyNode ?? definition.PublicApiSurface,
            unit,
            scope,
            value,
            state == ArchitectureApplicabilityRecordState.Evaluable ? orderedContributors : null);
        ArchitectureApplicabilityRecord record = new(
            definition.Id, Family, state, reasons, provenance)
        {
            MetricEvidence = evidence,
        };
        var measurement = new ArchitectureMetricMeasurement(
            definition.Id,
            definition.Kind,
            definition.TopologyNode ?? definition.PublicApiSurface,
            unit,
            scope,
            state,
            value,
            state == ArchitectureApplicabilityRecordState.Evaluable ? orderedContributors : null);
        return new MetricResult(measurement, record);
    }

    private sealed record MetricResult(
        ArchitectureMetricMeasurement Measurement,
        ArchitectureApplicabilityRecord Record);
}
