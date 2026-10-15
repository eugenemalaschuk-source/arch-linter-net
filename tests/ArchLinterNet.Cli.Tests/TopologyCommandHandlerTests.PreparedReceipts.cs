using System.CommandLine;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.BuildState;
using ArchLinterNet.Core.Topology;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class TopologyCommandHandlerTests
{
    [Test]
    public void Capture_PropagatesPreparedReceiptSelection()
    {
        FakeRuntime runtime = new();
        Command command = new TopologyCommandModule().CreateCommand(runtime, new FakeConsole(), new FakeFileSystem());

        int exitCode = command.Parse(["capture", "--subject-kind", "assembly", "--use-prepared-receipts"]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.LastCaptureRequest, Is.Not.Null);
            Assert.That(ReadUsePreparedArtifacts(runtime.LastCaptureRequest!), Is.True);
            Assert.That(runtime.LastCaptureRequest!.PreparationMode, Is.EqualTo(BuildPreparationMode.Ordinary));
        });
    }

    [TestCase("diff")]
    [TestCase("verify")]
    public void TopologySubcommands_PropagatePreparedReceiptSelection(string subcommand)
    {
        FakeRuntime runtime = new() { ValidationResult = Outcome(new("partial", "assembly", 1, [], [], [], []), true) };
        Command command = new TopologyCommandModule().CreateCommand(runtime, new FakeConsole(), new FakeFileSystem());

        int exitCode = command.Parse([subcommand, "--use-prepared-receipts"]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(runtime.LastValidationRequest, Is.Not.Null);
            Assert.That(ReadUsePreparedArtifacts(runtime.LastValidationRequest!), Is.True);
            Assert.That(runtime.LastValidationRequest!.PreparationMode, Is.EqualTo(BuildPreparationMode.Ordinary));
        });
    }

    private static bool ReadUsePreparedArtifacts(object request) =>
        (bool)(request.GetType().GetProperty(
            "UsePreparedArtifacts",
            System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)!.GetValue(request)!);
}
