using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.Measure.Application;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class CliArchitectureTests
{
    [TestCase("yaml", null, false, "Invalid format: yaml")]
    [TestCase("human", 0, false, "--max-contributors must be a positive integer.")]
    [TestCase("json", 1, true, "--max-contributors and --all-contributors cannot be used together.")]
    public void MeasureHandler_InvalidOptionsReturnTypedArgumentErrors(
        string format,
        int? maxContributors,
        bool allContributors,
        string expectedMessage)
    {
        FakeCliConsole console = new();
        var handler = new MeasureCommandHandler(new FakeCliRuntime(), console);

        int exitCode = handler.Execute(new MeasureCommandOptions(
            "policy.yml", format, [], null, maxContributors, allContributors, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdOut + console.StdErr, Does.Contain(expectedMessage));
        });
    }

    [Test]
    public void MeasureHandler_ShowHelpAvoidsRuntimeInvocation()
    {
        FakeCliConsole console = new();
        var handler = new MeasureCommandHandler(new FakeCliRuntime(), console);

        int exitCode = handler.Execute(new MeasureCommandOptions(
            "policy.yml", "human", [], null, null, false, true));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.StdOut, Does.Contain("arch-linter-net measure [options]"));
            Assert.That(console.StdErr, Is.Empty);
        });
    }

    [Test]
    public void MeasureHandler_RuntimeArgumentFailureWritesJsonArgumentError()
    {
        FakeCliConsole console = new();
        var runtime = new FakeCliRuntime { ExceptionToThrow = new ArgumentException("Unknown metric IDs: missing.") };
        var handler = new MeasureCommandHandler(runtime, console);

        int exitCode = handler.Execute(new MeasureCommandOptions(
            "policy.yml", "json", [], null, null, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Is.Empty);
            Assert.That(console.StdOut, Does.Contain("invalid-arguments"));
            Assert.That(console.StdOut, Does.Contain("Unknown metric IDs: missing."));
        });
    }

    [Test]
    public void MeasureHandler_UnexpectedRuntimeFailureWritesHumanErrorEnvelope()
    {
        FakeCliConsole console = new();
        var runtime = new FakeCliRuntime { ExceptionToThrow = new InvalidOperationException("fixture failure") };
        var handler = new MeasureCommandHandler(runtime, console);

        int exitCode = handler.Execute(new MeasureCommandOptions(
            "policy.yml", "human", [], null, null, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Does.Contain("Measure error: fixture failure"));
        });
    }

    [Test]
    public void MeasureHandler_UnexpectedRuntimeFailureWritesJsonErrorEnvelope()
    {
        FakeCliConsole console = new();
        var runtime = new FakeCliRuntime { ExceptionToThrow = new InvalidOperationException("fixture failure") };
        var handler = new MeasureCommandHandler(runtime, console);

        int exitCode = handler.Execute(new MeasureCommandOptions(
            "policy.yml", "json", [], null, null, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Is.Empty);
            Assert.That(console.StdOut, Does.Contain("unexpected-tool-failure"));
            Assert.That(console.StdOut, Does.Contain("Measure error: fixture failure"));
        });
    }

    [Test]
    public void MeasureHandler_TypedPolicyFailureWritesNormalizedJson()
    {
        ArchitecturePolicySourceDescriptor source = new(
            "architecture/root.yml",
            "architecture/root.yml",
            ArchitecturePolicyDocumentRole.Root,
            0,
            null,
            null,
            ["architecture/root.yml"]);
        var runtime = new FakeCliRuntime
        {
            ExceptionToThrow = new ArchitecturePolicyLoadException(
                "Invalid policy.",
                new ArchitecturePolicyDiagnostic(
                    ArchitecturePolicyDiagnosticKind.SourceShape,
                    new ArchitecturePolicySourceLocation(source, "$", 1, 1, null, null),
                    [],
                    source.ImportChain),
                ArchitecturePolicyImportErrorCategory.SourceShape.ToString()),
        };
        FakeCliConsole console = new();
        var handler = new MeasureCommandHandler(runtime, console);

        int exitCode = handler.Execute(new MeasureCommandOptions(
            "policy.yml", "json", [], null, null, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.StdErr, Is.Empty);
            Assert.That(console.StdOut, Does.Contain("architecture_policy_error"));
            Assert.That(console.StdOut, Does.Contain("policy_location"));
        });
    }

    [Test]
    public void MeasureHandler_CompleteOutcomeRendersAllContributorsInHumanFormat()
    {
        ArchitectureMetricMeasurement measurement = new(
            "application-outgoing",
            "outgoing_component_count",
            "application",
            null,
            "application",
            ArchitectureApplicabilityRecordState.Evaluable,
            3,
            ["alpha", "middle", "zeta"]);
        FakeCliConsole console = new();
        var runtime = new FakeCliRuntime
        {
            ForcedMeasurementOutcome = new ArchitectureMetricMeasurementOutcome([measurement], null, null),
        };
        var handler = new MeasureCommandHandler(runtime, console);

        int exitCode = handler.Execute(new MeasureCommandOptions(
            "policy.yml", "human", [], null, null, true, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.StdOut, Does.Contain("value: 3"));
            Assert.That(console.StdOut, Does.Contain("contributors: 3 (all)"));
            Assert.That(console.StdOut, Does.Contain("- alpha"));
            Assert.That(console.StdOut, Does.Contain("- middle"));
            Assert.That(console.StdOut, Does.Contain("- zeta"));
            Assert.That(console.StdOut, Does.Contain("applicability: none"));
        });
    }
}
