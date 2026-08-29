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
}
