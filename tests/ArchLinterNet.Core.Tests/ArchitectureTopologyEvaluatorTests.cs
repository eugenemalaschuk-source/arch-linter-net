using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureTopologyEvaluatorTests
{
    [Test]
    public void Evaluate_ExhaustiveMappedSubjectsWithAllowedRelation_IsEvaluable()
    {
        ArchitectureTopology topology = Topology(
            nodes:
            [
                Node("application", "App.Application"),
                Node("domain", "App.Domain"),
            ],
            allowedEdges: [new ArchitectureTopologyEdge { From = "application", To = "domain" }]);
        ArchitectureTopologyEvaluator.Result result = Evaluate(
            topology,
            [Subject("application", "App.Application"), Subject("domain", "App.Domain")],
            [Dependency("application", "domain", "App.Application.Service -> App.Domain.Entity")]);

        ArchitectureApplicabilityRecord record = result.Records.Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Evaluable));
            Assert.That(record.TopologyEvidence!.DeclaredComponentCount, Is.EqualTo(2));
            Assert.That(record.TopologyEvidence.ObservedSubjectCount, Is.EqualTo(2));
            Assert.That(record.TopologyEvidence.MappedSubjectCount, Is.EqualTo(2));
            Assert.That(record.TopologyEvidence.Relationships.Single().IsAllowed, Is.True);
            Assert.That(result.Violations, Is.Empty);
        });
    }

    [Test]
    public void Evaluate_ExhaustiveUnmappedSubject_IsUnassessableAndRetainsEvidence()
    {
        ArchitectureTopologyEvaluator.Result result = Evaluate(
            Topology(nodes: [Node("application", "App.Application")]),
            [Subject("application", "App.Application"), Subject("infrastructure", "App.Infrastructure")],
            []);

        ArchitectureApplicabilityRecord record = result.Records.Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(record.Reasons.Select(reason => reason.Code), Contains.Item(ArchitectureApplicabilityReasonCodes.UnmappedSubject));
            Assert.That(record.TopologyEvidence!.UnmappedSubjectCount, Is.EqualTo(1));
            Assert.That(record.TopologyEvidence.Subjects.Single(subject => subject.Disposition == "unmapped").Subject,
                Is.EqualTo("App.Infrastructure"));
        });
    }

    [Test]
    public void Evaluate_AmbiguousSubject_ReportsStructuralEvidenceWithoutChoosingNode()
    {
        ArchitectureTopologyEvaluator.Result result = Evaluate(
            Topology(nodes: [Node("first", "App.Feature"), Node("second", "App.Feature")]),
            [Subject("feature", "App.Feature")],
            []);

        ArchitectureApplicabilityRecord record = result.Records.Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(record.Reasons.Select(reason => reason.Code), Contains.Item(ArchitectureApplicabilityReasonCodes.AmbiguousSubject));
            Assert.That(record.TopologyEvidence!.AmbiguousSubjectCount, Is.EqualTo(1));
            Assert.That(record.TopologyEvidence.Subjects.Single().NodeIds, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(result.Violations.Single().ContractName, Is.EqualTo("topology structural mapping"));
        });
    }

    [Test]
    public void Evaluate_ReviewedOutOfScopeSubject_IsNotUnmapped()
    {
        ArchitectureTopology topology = Topology(nodes: [Node("application", "App.Application")]);
        topology.OutOfScope =
        [
            new ArchitectureTopologyOutOfScopeDeclaration
            {
                Id = "generated",
                Reason = "Generated at build time.",
                Selector = Namespace("App.Generated"),
            },
        ];

        ArchitectureTopologyEvaluator.Result result = Evaluate(
            topology,
            [Subject("application", "App.Application"), Subject("generated", "App.Generated")],
            []);

        ArchitectureTopologyMappingEvidence evidence = result.Records.Single().TopologyEvidence!;
        Assert.Multiple(() =>
        {
            Assert.That(result.Records.Single().State, Is.EqualTo(ArchitectureApplicabilityRecordState.Evaluable));
            Assert.That(evidence.ReviewedOutOfScopeSubjectCount, Is.EqualTo(1));
            Assert.That(evidence.UnmappedSubjectCount, Is.Zero);
            Assert.That(evidence.Subjects.Single(subject => subject.Subject == "App.Generated").ReviewedOutOfScopeId,
                Is.EqualTo("generated"));
        });
    }

    [Test]
    public void Evaluate_PartialUnmappedSubject_RemainsEvaluableEvidence()
    {
        ArchitectureTopology topology = Topology(nodes: [Node("application", "App.Application")], mode: "partial");
        ArchitectureTopologyEvaluator.Result result = Evaluate(
            topology,
            [Subject("infrastructure", "App.Infrastructure")],
            []);

        Assert.Multiple(() =>
        {
            Assert.That(result.ExpectedEntries.Single().Membership, Is.EqualTo(ArchitectureApplicabilityMembership.Optional));
            Assert.That(result.Records.Single().State, Is.EqualTo(ArchitectureApplicabilityRecordState.Evaluable));
            Assert.That(result.Records.Single().TopologyEvidence!.UnmappedSubjectCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Evaluate_RequiredEmptyUniverse_DoesNotInferDeclarationDrift()
    {
        ArchitectureTopology topology = Topology(
            nodes: [Node("application", "App.Application")],
            allowedEdges: [new ArchitectureTopologyEdge { From = "application", To = "application" }],
            staleDeclarations: true);
        ArchitectureTopologyEvaluator.Result result = Evaluate(topology, [], []);

        ArchitectureApplicabilityRecord record = result.Records.Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.Reasons.Select(reason => reason.Code), Is.EquivalentTo(
                new[] { ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput }));
            Assert.That(record.TopologyEvidence!.StaleNodes, Is.Empty);
            Assert.That(record.TopologyEvidence.StaleEdges, Is.Empty);
            Assert.That(result.Violations, Is.Empty);
        });
    }

    [Test]
    public void Evaluate_AllowedEmptyUniverse_CanInferDeclarationDrift()
    {
        ArchitectureTopology topology = Topology(
            nodes: [Node("application", "App.Application")],
            allowedEdges: [new ArchitectureTopologyEdge { From = "application", To = "application" }],
            staleDeclarations: true,
            allowEmpty: true);
        ArchitectureTopologyEvaluator.Result result = Evaluate(topology, [], []);

        ArchitectureApplicabilityRecord record = result.Records.Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.StaleDeclaration }));
            Assert.That(record.TopologyEvidence!.StaleNodes, Is.EqualTo(new[] { "application" }));
            Assert.That(record.TopologyEvidence.StaleEdges.Single(), Is.EqualTo(
                new ArchitectureTopologyStaleEdgeEvidence("application", "application")));
            Assert.That(result.Violations.Select(violation => violation.ContractName),
                Is.EqualTo(new[] { "topology declaration drift", "topology declaration drift" }));
        });
    }

    [Test]
    public void Evaluate_AmbiguousSubject_DoesNotProduceFalseStaleNodes()
    {
        ArchitectureTopology topology = Topology(
            nodes: [Node("first", "App.Feature"), Node("second", "App.Feature")],
            staleDeclarations: true);
        ArchitectureTopologyEvaluator.Result result = Evaluate(
            topology,
            [Subject("feature", "App.Feature")],
            []);

        ArchitectureApplicabilityRecord record = result.Records.Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.AmbiguousSubject }));
            Assert.That(record.TopologyEvidence!.StaleNodes, Is.Empty);
            Assert.That(record.TopologyEvidence.StaleEdges, Is.Empty);
            Assert.That(result.Violations.Select(violation => violation.ContractName),
                Is.EqualTo(new[] { "topology structural mapping" }));
        });
    }

    [Test]
    public void Evaluate_UnmappedSubject_DoesNotProduceFalseStaleNodesOrEdges()
    {
        ArchitectureTopology topology = Topology(
            nodes: [Node("source", "App.Source"), Node("target", "App.Target")],
            allowedEdges: [new ArchitectureTopologyEdge { From = "source", To = "target" }],
            staleDeclarations: true);
        ArchitectureTopologyEvaluator.Result result = Evaluate(
            topology,
            [Subject("source", "App.Source"), Subject("unmapped", "App.Unmapped")],
            [Dependency("source", "unmapped", "App.Source.Service -> App.Unmapped.Entity")]);

        ArchitectureApplicabilityRecord record = result.Records.Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.UnmappedSubject }));
            Assert.That(record.TopologyEvidence!.Relationships, Is.Empty);
            Assert.That(record.TopologyEvidence.StaleNodes, Is.Empty);
            Assert.That(record.TopologyEvidence.StaleEdges, Is.Empty);
            Assert.That(result.Violations, Is.Empty);
        });
    }

    [Test]
    public void Evaluate_IncompleteEndpointMapping_DoesNotProduceFalseStaleEdge()
    {
        ArchitectureTopology topology = Topology(
            nodes:
            [
                Node("source", "App.Source"),
                Node("target-first", "App.Target"),
                Node("target-second", "App.Target"),
            ],
            allowedEdges: [new ArchitectureTopologyEdge { From = "source", To = "target-first" }],
            staleDeclarations: true);
        ArchitectureTopologyEvaluator.Result result = Evaluate(
            topology,
            [Subject("source", "App.Source"), Subject("target", "App.Target")],
            [Dependency("source", "target", "App.Source.Service -> App.Target.Entity")]);

        ArchitectureApplicabilityRecord record = result.Records.Single();
        Assert.Multiple(() =>
        {
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Is.EqualTo(new[] { ArchitectureApplicabilityReasonCodes.AmbiguousSubject }));
            Assert.That(record.TopologyEvidence!.Relationships, Is.Empty);
            Assert.That(record.TopologyEvidence.StaleNodes, Is.Empty);
            Assert.That(record.TopologyEvidence.StaleEdges, Is.Empty);
            Assert.That(result.Violations.Select(violation => violation.ContractName),
                Is.EqualTo(new[] { "topology structural mapping" }));
        });
    }

    [Test]
    public void Evaluate_ForbiddenRelation_SelectsOrdinalWitnessRegardlessOfInputOrder()
    {
        ArchitectureTopology topology = Topology(nodes: [Node("application", "App.Application"), Node("domain", "App.Domain")]);
        ArchitectureTopologyEvaluator.Result first = Evaluate(
            topology,
            [Subject("application", "App.Application"), Subject("domain", "App.Domain")],
            [
                Dependency("application", "domain", "Z.Service -> Z.Entity"),
                Dependency("application", "domain", "A.Service -> A.Entity"),
            ]);
        ArchitectureTopologyEvaluator.Result second = Evaluate(
            topology,
            [Subject("domain", "App.Domain"), Subject("application", "App.Application")],
            [
                Dependency("application", "domain", "A.Service -> A.Entity"),
                Dependency("application", "domain", "Z.Service -> Z.Entity"),
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(first.Violations.Single().SourceType, Is.EqualTo("application"));
            Assert.That(first.Violations.Single().ForbiddenNamespace, Is.EqualTo("domain"));
            Assert.That(first.Violations.Single().ForbiddenReferences, Is.EqualTo(new[] { "A.Service -> A.Entity" }));
            Assert.That(second.Violations.Single().ContractName, Is.EqualTo(first.Violations.Single().ContractName));
            Assert.That(second.Violations.Single().SourceType, Is.EqualTo(first.Violations.Single().SourceType));
            Assert.That(second.Violations.Single().ForbiddenNamespace, Is.EqualTo(first.Violations.Single().ForbiddenNamespace));
            Assert.That(second.Violations.Single().ForbiddenReferences, Is.EqualTo(first.Violations.Single().ForbiddenReferences));
        });
    }

    [Test]
    public void ValidationService_ExecutesTopologyAndProjectsCanonicalEvidence()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-topology-evaluation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            string policyPath = Path.Combine(temporaryDirectory, "dependencies.arch.yml");
            File.WriteAllText(policyPath, """
                version: 1
                name: Topology execution
                analysis:
                  target_assemblies: [ArchLinterNet.Core]
                topology:
                  mode: exhaustive
                  subject_kind: namespace
                  scope:
                    allow_empty: false
                    selectors:
                      - namespace: ArchLinterNet.Core.Model
                      - namespace: ArchLinterNet.Core.Validation
                  nodes:
                    - id: model
                      mappings: [{ namespace: ArchLinterNet.Core.Model }]
                    - id: validation
                      mappings: [{ namespace: ArchLinterNet.Core.Validation }]
                  allowed_edges: [{ from: validation, to: model }]
                contracts: {}
                """);

            ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict",
            });

            ArchitectureApplicabilityRecord record = outcome.ApplicabilityRecords.Single();
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Passed, Is.True);
                Assert.That(record.TopologyEvidence, Is.Not.Null);
                Assert.That(record.TopologyEvidence!.MappedSubjectCount, Is.GreaterThan(0));
                Assert.That(record.TopologyEvidence.UnmappedSubjectCount, Is.Zero);
                Assert.That(record.TopologyEvidence.Subjects.All(subject =>
                    !subject.Identity.Contains("canonical_assembly=", StringComparison.Ordinal)), Is.True);
                Assert.That(record.TopologyEvidence.Relationships.Any(relationship =>
                    relationship.SourceNode == "validation"
                    && relationship.TargetNode == "model"
                    && relationship.IsAllowed), Is.True);
                Assert.That(outcome.ApplicabilityProjection!.Controls.Single().Record!.TopologyEvidence,
                    Is.SameAs(record.TopologyEvidence));
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static ArchitectureTopologyEvaluator.Result Evaluate(
        ArchitectureTopology topology,
        IReadOnlyList<Execution.ArchitectureTopologyObservedSubject> subjects,
        IReadOnlyList<Execution.ArchitectureTopologyObservedDependency> dependencies) =>
        ArchitectureTopologyEvaluator.Evaluate(
            session: null,
            topology: topology,
            observedSubjects: subjects,
            observedDependencies: dependencies);

    private static ArchitectureTopology Topology(
        IReadOnlyList<ArchitectureTopologyNode> nodes,
        IReadOnlyList<ArchitectureTopologyEdge>? allowedEdges = null,
        string mode = "exhaustive",
        bool staleDeclarations = false,
        bool allowEmpty = false) => new()
        {
            Mode = mode,
            SubjectKind = "namespace",
            Scope = new ArchitectureTopologyScope { AllowEmpty = allowEmpty, Selectors = [Namespace("App")] },
            Nodes = nodes.ToList(),
            AllowedEdges = allowedEdges?.ToList() ?? [],
            StaleDeclarations = staleDeclarations,
        };

    private static ArchitectureTopologyNode Node(string id, string @namespace) => new()
    {
        Id = id,
        Mappings = [Namespace(@namespace)],
    };

    private static ArchitectureTopologySubjectSelector Namespace(string value) => new() { Namespace = value };

    private static Execution.ArchitectureTopologyObservedSubject Subject(string id, string @namespace) => new(
        id,
        "Sample.Project",
        "Sample.Assembly",
        @namespace);

    private static Execution.ArchitectureTopologyObservedDependency Dependency(string source, string target, string witness) =>
        new(source, target, witness);
}
