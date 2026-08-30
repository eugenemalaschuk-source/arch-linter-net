using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Validate;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class ValidateCommandHandlerReportModeTests
{
    [TestCase(ArchitectureAssessmentCompletionState.Pass, true, CliExitCodes.Success)]
    [TestCase(ArchitectureAssessmentCompletionState.Pass, false, CliExitCodes.ValidationFailure)]
    [TestCase(ArchitectureAssessmentCompletionState.Fail, false, CliExitCodes.ValidationFailure)]
    [TestCase(ArchitectureAssessmentCompletionState.Unassessable, false, CliExitCodes.InvalidArgumentsOrRuntimeError)]
    public void ValidateHandler_CompletedAssessmentMapsCompletionStateToExitCategory(
        ArchitectureAssessmentCompletionState state,
        bool passed,
        int expectedExitCode)
    {
        FakeCliRuntime runtime = new() { ForcedOutcome = CreateCompletionOutcome(passed, state) };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(new ValidateCommandOptions(
            "policy.yml", "strict", "human", [], null, false, null, false, false));

        Assert.That(exitCode, Is.EqualTo(expectedExitCode));
    }

    [Test]
    public void ValidateHandler_CombinedUnassessableCompletionWinsOverTrustedFailure()
    {
        ValidationOutcome trustedFailure = CreateCompletionOutcome(
            passed: false, ArchitectureAssessmentCompletionState.Fail);
        ValidationOutcome unassessable = CreateCompletionOutcome(
            passed: false, ArchitectureAssessmentCompletionState.Unassessable);

        int exitCode = ValidateCommandHandler.ResolveCombinedValidationExitCode(
            [
                ("strict", trustedFailure),
                ("audit", unassessable),
            ],
            allPassed: false);

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
    }

    [TestCase("human")]
    [TestCase("json")]
    [TestCase("sarif")]
    public void ValidateHandler_UnassessableCompletionIsRenderedAcrossFormats(string format)
    {
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = CreateCompletionOutcome(
                passed: false, ArchitectureAssessmentCompletionState.Unassessable),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(new ValidateCommandOptions(
            "policy.yml", "strict", format, [], null, false, null, false, false));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        switch (format)
        {
            case "human":
                Assert.That(console.StdOut, Does.Contain("Assessment completion: unassessable"));
                Assert.That(console.StdOut, Does.Contain(ArchitectureApplicabilityReasonCodes.MissingRequiredInput));
                break;
            case "json":
                using (JsonDocument json = JsonDocument.Parse(console.StdOut))
                {
                    JsonElement completion = json.RootElement.GetProperty("assessment_completion");
                    Assert.That(completion.GetProperty("state").GetString(), Is.EqualTo("unassessable"));
                    Assert.That(completion.GetProperty("reasons")[0].GetProperty("code").GetString(),
                        Is.EqualTo(ArchitectureApplicabilityReasonCodes.MissingRequiredInput));
                }
                break;
            case "sarif":
                using (JsonDocument sarif = JsonDocument.Parse(console.StdOut))
                {
                    JsonElement completion = sarif.RootElement.GetProperty("runs").GetArrayLength() > 0
                        ? sarif.RootElement.GetProperty("runs")[0]
                            .GetProperty("properties")
                            .GetProperty("arch_linter_net.assessment_completion")
                        : sarif.RootElement.GetProperty("properties")
                            .GetProperty("arch_linter_net.assessment_completion");
                    Assert.That(completion.GetProperty("state").GetString(), Is.EqualTo("unassessable"));
                    Assert.That(completion.GetProperty("reasons")[0].GetProperty("code").GetString(),
                        Is.EqualTo(ArchitectureApplicabilityReasonCodes.MissingRequiredInput));
                }
                break;
        }
    }

    [TestCase("human", "strict")]
    [TestCase("json", "strict")]
    [TestCase("sarif", "strict")]
    [TestCase("human", "audit")]
    [TestCase("json", "audit")]
    [TestCase("sarif", "audit")]
    public void ValidateHandler_ApplicabilityProjectionPreservesSummaryControlsAndFinding(
        string format,
        string mode)
    {
        ValidationOutcome outcome = CreateProjectedOutcome(mode);
        FakeCliRuntime runtime = new() { ForcedOutcome = outcome };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(new ValidateCommandOptions(
            "policy.yml", mode, format, [], null, false, null, false, false));

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
        switch (format)
        {
            case "human":
                Assert.Multiple(() =>
                {
                    Assert.That(console.StdOut, Does.Contain("Assessment completeness transparency"));
                    Assert.That(console.StdOut, Does.Contain("required=2"));
                    Assert.That(console.StdOut, Does.Contain("control=required-unassessable"));
                    Assert.That(console.StdOut, Does.Contain("family=topology"));
                    Assert.That(console.StdOut, Does.Contain(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput));
                    Assert.That(console.StdOut, Does.Contain("policy=policy-v08"));
                    Assert.That(console.StdOut, Does.Contain("topology=(declared_components=2, observed_subjects=2"));
                });
                break;
            case "json":
                using (JsonDocument json = JsonDocument.Parse(console.StdOut))
                {
                    JsonElement root = json.RootElement;
                    JsonElement completion = root.GetProperty("assessment_completion");
                    JsonElement summary = completion.GetProperty("summary");
                    JsonElement control = completion.GetProperty("controls")
                        .EnumerateArray()
                        .Single(element => element.GetProperty("control_identity").GetString() == "required-unassessable");
                    JsonElement finding = root.GetProperty("applicability_findings")[0];
                    Assert.Multiple(() =>
                    {
                        Assert.That(summary.GetProperty("interpretation").GetString(),
                            Does.Contain("not an architecture quality score"));
                        Assert.That(summary.GetProperty("required_count").GetInt32(), Is.EqualTo(2));
                        Assert.That(summary.GetProperty("required_evaluable_count").GetInt32(), Is.EqualTo(1));
                        Assert.That(summary.GetProperty("required_unassessable_count").GetInt32(), Is.EqualTo(1));
                        Assert.That(control.GetProperty("membership").GetString(), Is.EqualTo("required"));
                        Assert.That(control.GetProperty("state").GetString(), Is.EqualTo("unassessable"));
                        Assert.That(control.GetProperty("is_integrity_valid").GetBoolean(), Is.True);
                        Assert.That(control.GetProperty("record").GetProperty("provenance")
                            .GetProperty("policy_identity").GetString(), Is.EqualTo("policy-v08"));
                        Assert.That(control.GetProperty("record").GetProperty("topology_evidence")
                            .GetProperty("declared_component_count").GetInt32(), Is.EqualTo(2));
                        Assert.That(control.GetProperty("record").GetProperty("topology_evidence")
                            .GetProperty("relationships")[0].GetProperty("witness").GetString(),
                            Is.EqualTo("Example.App.Service -> Example.Domain.Entity"));
                        Assert.That(finding.GetProperty("kind").GetString(), Is.EqualTo("applicability"));
                        Assert.That(finding.GetProperty("details").GetProperty("control_identity").GetString(),
                            Is.EqualTo("required-unassessable"));
                        Assert.That(finding.GetProperty("details").GetProperty("reason_code").GetString(),
                            Is.EqualTo(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput));
                        Assert.That(finding.GetProperty("severity").GetString(),
                            Is.EqualTo(mode == "strict" ? "error" : "warning"));
                    });
                }
                break;
            case "sarif":
                using (JsonDocument sarif = JsonDocument.Parse(console.StdOut))
                {
                    JsonElement run = sarif.RootElement.GetProperty("runs")[0];
                    JsonElement completion = run.GetProperty("properties")
                        .GetProperty("arch_linter_net.assessment_completion");
                    JsonElement topologyControl = completion.GetProperty("controls")
                        .EnumerateArray()
                        .Single(element => element.GetProperty("control_identity").GetString() == "required-unassessable");
                    JsonElement result = run.GetProperty("results")[0];
                    JsonElement finding = result.GetProperty("properties")
                        .GetProperty("arch_linter_net");
                    Assert.Multiple(() =>
                    {
                        Assert.That(completion.GetProperty("summary").GetProperty("required_count").GetInt32(),
                            Is.EqualTo(2));
                        Assert.That(completion.GetProperty("controls").GetArrayLength(), Is.EqualTo(4));
                        Assert.That(topologyControl.GetProperty("record")
                            .GetProperty("topology_evidence").GetProperty("mapped_subject_count").GetInt32(),
                            Is.EqualTo(1));
                        Assert.That(result.GetProperty("level").GetString(),
                            Is.EqualTo(mode == "strict" ? "error" : "warning"));
                        Assert.That(finding.GetProperty("kind").GetString(), Is.EqualTo("applicability"));
                        Assert.That(finding.GetProperty("canonical_identity").GetString(),
                            Is.EqualTo(outcome.ApplicabilityProjection!.Findings[0].CanonicalIdentity));
                        Assert.That(finding.GetProperty("details").GetProperty("reason_code").GetString(),
                            Is.EqualTo(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput));
                    });
                }
                break;
        }
    }

    [Test]
    public void ValidateHandler_InvalidModeDoesNotClaimUnassessableAssessment()
    {
        FakeCliRuntime runtime = new()
        {
            ForcedOutcome = CreateCompletionOutcome(
                passed: false, ArchitectureAssessmentCompletionState.Unassessable),
        };
        FakeCliConsole console = new();
        ValidateCommandHandler handler = new(runtime, console, new FakeFileSystem(exists: true));

        int exitCode = handler.Execute(new ValidateCommandOptions(
            "policy.yml", "unknown", "human", [], null, false, null, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(runtime.ValidationCallCount, Is.EqualTo(0));
            Assert.That(console.StdErr, Does.Contain("Invalid mode"));
            Assert.That(console.StdErr, Does.Not.Contain("Assessment completion"));
        });
    }

    private static ValidationOutcome CreateCompletionOutcome(
        bool passed,
        ArchitectureAssessmentCompletionState state)
    {
        ArchitectureApplicabilityReason reason = new(
            ArchitectureApplicabilityReasonCodes.MissingRequiredInput,
            new ArchitectureApplicabilityProvenance("topology", "topology-control", "policy-v08"));
        ArchitectureAssessmentCompletionEvidence completion = new(
            state,
            Array.Empty<ArchitectureApplicabilityAssessment>(),
            state == ArchitectureAssessmentCompletionState.Unassessable ? [reason] : []);

        return new ValidationOutcome(
            passed,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(), "off",
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off",
            Array.Empty<PolicyConsistencyDiagnostic>(), "off",
            Array.Empty<ArchitectureCoverageSummary>(),
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            AssessmentCompletionEvidence = completion,
        };
    }

    private static ValidationOutcome CreateProjectedOutcome(string mode)
    {
        ArchitectureApplicabilityReason reason = new(
            ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput,
            new ArchitectureApplicabilityProvenance("topology", "required-unassessable", "policy-v08"));
        ArchitectureApplicabilityExpectedEntry[] expected =
        [
            new("required-evaluable", "topology", ArchitectureApplicabilityMembership.Required,
                new ArchitectureApplicabilityProvenance("topology", "required-evaluable", "policy-v08")),
            new("required-unassessable", "topology", ArchitectureApplicabilityMembership.Required,
                new ArchitectureApplicabilityProvenance("topology", "required-unassessable", "policy-v08")),
            new("optional", "topology", ArchitectureApplicabilityMembership.Optional,
                new ArchitectureApplicabilityProvenance("topology", "optional", "policy-v08")),
            new("not-applicable", "topology", ArchitectureApplicabilityMembership.NotApplicable,
                new ArchitectureApplicabilityProvenance("topology", "not-applicable", "policy-v08")),
        ];
        ArchitectureApplicabilityRecord[] records =
        [
            new("required-evaluable", "topology", ArchitectureApplicabilityRecordState.Evaluable,
                Array.Empty<ArchitectureApplicabilityReason>(),
                new ArchitectureApplicabilityProvenance("topology", "required-evaluable", "policy-v08")),
            new("required-unassessable", "topology", ArchitectureApplicabilityRecordState.Unassessable, [reason],
                new ArchitectureApplicabilityProvenance("topology", "required-unassessable", "policy-v08"))
            {
                TopologyEvidence = CreateTopologyEvidence(),
            },
            new("optional", "topology", ArchitectureApplicabilityRecordState.NotApplicable,
                Array.Empty<ArchitectureApplicabilityReason>(),
                new ArchitectureApplicabilityProvenance("topology", "optional", "policy-v08")),
            new("not-applicable", "topology", ArchitectureApplicabilityRecordState.NotApplicable,
                Array.Empty<ArchitectureApplicabilityReason>(),
                new ArchitectureApplicabilityProvenance("topology", "not-applicable", "policy-v08")),
        ];
        ArchitectureAssessmentCompletionEvidence completion = ArchitectureApplicabilityEvaluator.Evaluate(
            expected, records, conformancePassed: true)!;
        ArchitectureApplicabilityProjection projection = ArchitectureApplicabilityProjector.Project(completion, mode)!;

        return new ValidationOutcome(
            false,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureViolation>(), "off",
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), "off",
            Array.Empty<PolicyConsistencyDiagnostic>(), "off",
            Array.Empty<ArchitectureCoverageSummary>(),
            Array.Empty<ArchitectureClassificationConflict>(),
            Array.Empty<ArchitectureClassificationMetadataFailure>())
        {
            AssessmentCompletionEvidence = completion,
            ApplicabilityProjection = projection,
        };
    }

    private static ArchitectureTopologyMappingEvidence CreateTopologyEvidence() => new(
        "exhaustive",
        "namespace",
        2,
        [
            new ArchitectureTopologySubjectEvidence(
                "namespace|project=Example|assembly=Example|subject=Example.App",
                "Example",
                "Example",
                "Example.App",
                "mapped",
                ["application"]),
            new ArchitectureTopologySubjectEvidence(
                "namespace|project=Example|assembly=Example|subject=Example.Domain",
                "Example",
                "Example",
                "Example.Domain",
                "unmapped"),
        ],
        [new ArchitectureTopologyRelationEvidence(
            "application", "domain", "Example.App.Service -> Example.Domain.Entity", IsAllowed: false)],
        Array.Empty<string>(),
        Array.Empty<ArchitectureTopologyStaleEdgeEvidence>());
}
