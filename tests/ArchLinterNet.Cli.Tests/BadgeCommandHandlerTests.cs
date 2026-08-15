using System.Text.Json;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.Badge.Application;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed partial class ValidateCommandHandlerReportModeTests
{
    [Test]
    public void BadgeHandler_EmitsShieldsPayloadForPassingAndFailingStrictValidation()
    {
        FakeCliRuntime passingRuntime = new();
        FakeCliConsole passingConsole = new();
        int passingExit = new BadgeCommandHandler(passingRuntime, passingConsole, default).Execute(new("policy.yml", true, false, null, false));

        FakeCliRuntime failingRuntime = new()
        {
            ForcedOutcome = new ValidationOutcome(false,
                [new ArchitectureViolation("rule-a", null, "source", "target", Array.Empty<string>())], [], [], "off", [], "off", [], "off", [], [], []),
        };
        FakeCliConsole failingConsole = new();
        int failingExit = new BadgeCommandHandler(failingRuntime, failingConsole, default).Execute(new("policy.yml", false, false, null, false));

        using JsonDocument passing = JsonDocument.Parse(passingConsole.StdOut);
        using JsonDocument failing = JsonDocument.Parse(failingConsole.StdOut);
        Assert.Multiple(() =>
        {
            Assert.That(passingExit, Is.EqualTo(CliExitCodes.Success));
            Assert.That(passing.RootElement.GetProperty("message").GetString(), Is.EqualTo("passing"));
            Assert.That(passing.RootElement.GetProperty("color").GetString(), Is.EqualTo("brightgreen"));
            Assert.That(failingExit, Is.EqualTo(CliExitCodes.ValidationFailure));
            Assert.That(failing.RootElement.GetProperty("message").GetString(), Is.EqualTo("failing"));
            Assert.That(failing.RootElement.GetProperty("color").GetString(), Is.EqualTo("red"));
        });
    }

    [Test]
    public void BadgeHandler_EmitsUnavailablePayloadWhenValidationCannotRun()
    {
        FakeCliConsole console = new();
        FakeCliRuntime runtime = new() { ExceptionToThrow = new InvalidOperationException("missing build receipt") };

        int exitCode = new BadgeCommandHandler(runtime, console, default).Execute(new("policy.yml", true, false, null, false));

        using JsonDocument payload = JsonDocument.Parse(console.StdOut);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(payload.RootElement.GetProperty("message").GetString(), Is.EqualTo("unavailable"));
            Assert.That(payload.RootElement.GetProperty("color").GetString(), Is.EqualTo("red"));
        });
    }
}
