using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;

namespace ArchLinterNet.Core.Execution;

// The sole measurement authority. It selects definitions, gates incomplete universes, normalizes
// raw calculator evidence, and builds applicability and immutable measurement outcomes.
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

    // Assembly footprint and assembly component relations use assembly metadata, whose native
    // universe is independent of Assembly.GetTypes(). Other topology metrics need a complete type
    // and reflection universe because a load failure cannot retain a trusted known subset.
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
            MetricResult result = Unassessable(
                definition,
                definition.TopologyNode ?? definition.PublicApiSurface ?? string.Empty,
                provenance,
                reasonCode);
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
            ArchitectureMetricRawEvidence publicRaw = ArchitecturePublicContractMetricCalculator.Calculate(session, definition);
            return Finish(definition, publicRaw, Array.Empty<string>(), provenance);
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

        ArchitectureTopologyEvaluator.SubjectClassification[] scoped = topology.Classifications
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
                !ArchitectureTopologyMetricCalculator.HasCanonicalProjectOwner(session, classification.Subject)))
        {
            reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
        }

        if (topology.Topology.SubjectKind == "project"
            && ArchitectureTopologyMetricCalculator.HasAmbiguousProjectSelectorIdentity(
                topology.Topology, scope, scoped))
        {
            // Project selectors retain their simple-name spelling for topology compatibility, but
            // metrics must not merge owners that that spelling cannot distinguish.
            reasons.Add(ArchitectureApplicabilityReasonCodes.MissingRequiredInput);
        }

        ArchitectureMetricRawEvidence topologyRaw = definition.Kind == ArchitectureMetricKinds.ExternalDependencyGroupCount
            ? ArchitectureExternalDependencyMetricCalculator.ExternalGroups(
                session,
                topology,
                scope,
                session.ExternalDependencyFacts.Facts,
                session.ExternalDependencyFacts.IncompleteSourceTypes)
            : ArchitectureTopologyMetricCalculator.Calculate(session, definition, topology, scope, scoped);
        return Finish(definition, topologyRaw, reasons, provenance);
    }

    private static MetricResult Unassessable(
        ArchitectureMetricDefinition definition,
        string scope,
        ArchitectureApplicabilityProvenance provenance,
        string reasonCode) =>
        Finish(
            definition,
            new ArchitectureMetricRawEvidence(
                scope,
                definition.Unit,
                [reasonCode],
                Array.Empty<string>()),
            Array.Empty<string>(),
            provenance);

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
            new ArchitectureMetricRawEvidence(
                scope,
                definition.Kind == ArchitectureMetricKinds.ComponentFootprintCount ? definition.Unit : null,
                Array.Empty<string>(),
                Array.Empty<string>()),
            Array.Empty<string>(),
            provenance);
    }

    // Every reason and contributor enters this method as raw evidence. Ordering and duplicate
    // removal happen here once, before the immutable applicability and measurement models are made.
    private static MetricResult Finish(
        ArchitectureMetricDefinition definition,
        ArchitectureMetricRawEvidence raw,
        IEnumerable<string> additionalReasons,
        ArchitectureApplicabilityProvenance provenance)
    {
        string[] orderedReasons = raw.ReasonCodes
            .Concat(additionalReasons)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        string[] orderedContributors = raw.Contributors
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
            raw.Unit,
            raw.Scope,
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
            raw.Unit,
            raw.Scope,
            state,
            value,
            state == ArchitectureApplicabilityRecordState.Evaluable ? orderedContributors : null);
        return new MetricResult(measurement, record);
    }

    private sealed record MetricResult(
        ArchitectureMetricMeasurement Measurement,
        ArchitectureApplicabilityRecord Record);
}
