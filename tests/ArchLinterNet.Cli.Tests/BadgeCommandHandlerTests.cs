using System.Text.Json;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.Badge.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed partial class ValidateCommandHandlerReportModeTests
{
    [Test]
    public void BadgeHandler_EmitsShieldsPayloadForPassingAndFailingStrictValidation()
    {
        FakeCliConsole passingConsole = new();
        FakeFileSystem passingFiles = new(exists: true);
        passingFiles.Contents["passing.json"] = "{\"passed\":true}";
        int passingExit = new BadgeCommandHandler(passingConsole, passingFiles).Execute(new("passing.json", false));

        FakeCliConsole failingConsole = new();
        FakeFileSystem failingFiles = new(exists: true);
        failingFiles.Contents["failing.json"] = "{\"passed\":false}";
        int failingExit = new BadgeCommandHandler(failingConsole, failingFiles).Execute(new("failing.json", false));

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
    public void BadgeHandler_EmitsUnavailablePayloadWhenInputCannotBeRead()
    {
        FakeCliConsole console = new();
        FakeFileSystem files = new(exists: false);

        int exitCode = new BadgeCommandHandler(console, files).Execute(new("missing.json", false));

        using JsonDocument payload = JsonDocument.Parse(console.StdOut);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(payload.RootElement.GetProperty("message").GetString(), Is.EqualTo("unavailable"));
            Assert.That(payload.RootElement.GetProperty("color").GetString(), Is.EqualTo("red"));
        });
    }
}
