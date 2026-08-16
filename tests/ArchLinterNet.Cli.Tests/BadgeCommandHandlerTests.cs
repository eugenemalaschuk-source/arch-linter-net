using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.Badge.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class BadgeCommandHandlerTests
{
    [TestCase("{\"mode\":\"strict\",\"passed\":true}", CliExitCodes.Success, "passing")]
    [TestCase("{\"mode\":\"strict\",\"passed\":false}", CliExitCodes.ValidationFailure, "failing")]
    [TestCase("{\"mode\":\"audit\",\"passed\":true}", CliExitCodes.InvalidArgumentsOrRuntimeError, "unavailable")]
    public void Handler_ProjectsOnlyStrictInput(string input, int expectedExitCode, string expectedMessage)
    {
        FakeConsole console = new();
        int exitCode = new BadgeCommandHandler(console, new FakeFileSystem(input)).Execute(new("input.json", false));
        using JsonDocument output = JsonDocument.Parse(console.Output);
        Assert.Multiple(() => { Assert.That(exitCode, Is.EqualTo(expectedExitCode)); Assert.That(output.RootElement.GetProperty("message").GetString(), Is.EqualTo(expectedMessage)); });
    }

    private sealed class FakeConsole : ICliConsole { private readonly StringBuilder _output = new(); public TextWriter Out => new StringWriter(_output); public TextWriter Error => TextWriter.Null; public string Output => _output.ToString(); }
    private sealed class FakeFileSystem(string input) : IFileSystem { public bool FileExists(string path) => true; public string ReadAllText(string path) => input; public void WriteAllText(string path, string contents) { } public string WriteAllTextToTemp(string targetPath, string contents) => targetPath; public void RenameTempToTarget(string tempPath, string targetPath) { } public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => true; public void DeleteFile(string path) { } public bool TryCreateNewFile(string path) => true; public bool DirectoryExists(string path) => true; public void DeleteDirectoryIfEmpty(string path) { } public bool CanWriteToDirectory(string path) => true; }
}
