using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.Coverage.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class CoverageCommandHandlerTests
{
    private const string CombinedInput = """{"results":[{"mode":"audit","passed":true},{"mode":"strict","passed":false}]}""";
    private const string SingleModeInput = """{"mode":"strict","passed":true}""";

    [Test]
    public void Extract_CombinedInput_WritesMatchingModeResult()
    {
        FakeFileSystem fileSystem = new(("input.json", CombinedInput));
        int exitCode = new CoverageCommandHandler(new FakeConsole(), fileSystem).Extract("input.json", "strict", "out.json");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.Written["out.json"], Does.Contain("\"mode\":\"strict\""));
        });
    }

    [Test]
    public void Extract_SingleModeInput_WritesDocumentWhenModeMatches()
    {
        FakeFileSystem fileSystem = new(("input.json", SingleModeInput));
        int exitCode = new CoverageCommandHandler(new FakeConsole(), fileSystem).Extract("input.json", "strict", "out.json");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.Written["out.json"], Does.Contain("\"passed\":true"));
        });
    }

    [Test]
    public void Extract_ModeNotPresent_ReportsErrorAndFails()
    {
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", CombinedInput));
        int exitCode = new CoverageCommandHandler(console, fileSystem).Extract("input.json", "audit-only", "out.json");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("Could not extract architecture validation result"));
        });
    }

    [Test]
    public void Extract_MalformedJson_ReportsErrorAndFails()
    {
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", "not-json"));
        int exitCode = new CoverageCommandHandler(console, fileSystem).Extract("input.json", "strict", "out.json");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("Could not extract architecture validation result"));
        });
    }

    [Test]
    public void Extract_MissingFile_ReportsErrorAndFails()
    {
        FakeConsole console = new();
        FakeFileSystem fileSystem = new();
        int exitCode = new CoverageCommandHandler(console, fileSystem).Extract("missing.json", "strict", "out.json");

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("Could not extract architecture validation result"));
        });
    }

    [Test]
    public void Execute_ShowHelp_WritesUsageAndSucceeds()
    {
        FakeConsole console = new();
        int exitCode = new CoverageCommandHandler(console, new FakeFileSystem()).Execute(
            new CoverageReportCommandOptions("input.json", null, ".", null, null, "ok", true));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("arch-linter-net coverage report"));
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Execute_InvalidMaxFailureDiagnostics_ReportsErrorAndFails(int maxFailures)
    {
        FakeConsole console = new();
        int exitCode = new CoverageCommandHandler(console, new FakeFileSystem()).Execute(
            new CoverageReportCommandOptions("input.json", null, ".", null, maxFailures, "ok", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("Invalid coverage report options."));
        });
    }

    [Test]
    public void Execute_InvalidDiffStatus_ReportsErrorAndFails()
    {
        FakeConsole console = new();
        int exitCode = new CoverageCommandHandler(console, new FakeFileSystem()).Execute(
            new CoverageReportCommandOptions("input.json", null, ".", null, null, "bogus", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("Invalid coverage report options."));
        });
    }

    [Test]
    public void Execute_NoOutputPath_WritesMarkdownToConsole()
    {
        const string Report = """{"mode":"strict","passed":true,"coverage_summary":[]}""";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", Report));
        int exitCode = new CoverageCommandHandler(console, fileSystem).Execute(
            new CoverageReportCommandOptions("input.json", null, ".", null, null, "ok", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("## Architecture coverage"));
            Assert.That(fileSystem.Written, Is.Empty);
        });
    }

    [Test]
    public void Execute_WithOutputPath_WritesMarkdownToFile()
    {
        const string Report = """{"mode":"strict","passed":true,"coverage_summary":[]}""";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", Report));
        int exitCode = new CoverageCommandHandler(console, fileSystem).Execute(
            new CoverageReportCommandOptions("input.json", null, ".", "report.md", null, "ok", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Is.Empty);
            Assert.That(fileSystem.Written["report.md"], Does.Contain("## Architecture coverage"));
        });
    }

    [Test]
    public void Execute_CombinedResultsSelectsStrictEntry()
    {
        const string Report = """{"results":[{"mode":"audit","passed":false,"coverage_summary":[]},{"mode":"strict","passed":true,"coverage_summary":[]}]}""";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", Report));
        int exitCode = new CoverageCommandHandler(console, fileSystem).Execute(
            new CoverageReportCommandOptions("input.json", null, ".", null, null, "ok", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("**Status:** ✅ pass"));
        });
    }

    [Test]
    public void Execute_CombinedResultsMissingStrictEntry_ReportsErrorAndFails()
    {
        const string Report = """{"results":[{"mode":"audit","passed":true,"coverage_summary":[]}]}""";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", Report));
        int exitCode = new CoverageCommandHandler(console, fileSystem).Execute(
            new CoverageReportCommandOptions("input.json", null, ".", null, null, "ok", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("Could not render architecture coverage report"));
        });
    }

    [Test]
    public void Execute_MalformedInputJson_ReportsErrorAndFails()
    {
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", "not-json"));
        int exitCode = new CoverageCommandHandler(console, fileSystem).Execute(
            new CoverageReportCommandOptions("input.json", null, ".", null, null, "ok", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("Could not render architecture coverage report"));
        });
    }

    [Test]
    public void Execute_DiffStatusFailed_IgnoresChangedFilesPathAndReportsDiffUnavailable()
    {
        const string Report = """{"mode":"strict","passed":true,"coverage_summary":[]}""";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", Report), ("changed.txt", "src/Foo.cs"));
        int exitCode = new CoverageCommandHandler(console, fileSystem).Execute(
            new CoverageReportCommandOptions("input.json", "changed.txt", ".", null, null, "failed", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("**Unavailable:** the changed-files diff"));
        });
    }

    [Test]
    public void Execute_ChangedFilesPathMissingFromDisk_TreatsChangedFilesAsNull()
    {
        const string Report = """{"mode":"strict","passed":true,"coverage_summary":[]}""";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", Report));
        int exitCode = new CoverageCommandHandler(console, fileSystem).Execute(
            new CoverageReportCommandOptions("input.json", "changed.txt", ".", null, null, "ok", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Not.Contain("### New-code coverage"));
        });
    }

    [Test]
    public void Execute_ChangedFilesPathPresent_SplitsAndTrimsLines()
    {
        const string Report = """{"mode":"strict","passed":true,"coverage_summary":[]}""";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", Report), ("changed.txt", "Foo.cs\r\n \r\nBar.cs\n"));
        int exitCode = new CoverageCommandHandler(console, fileSystem).Execute(
            new CoverageReportCommandOptions("input.json", "changed.txt", TestContext.CurrentContext.TestDirectory, null, null, "ok", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("| Changed first-party files | 2 |"));
        });
    }

    private sealed class FakeConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _error = new();
        public TextWriter Out => new StringWriter(_output);
        public TextWriter Error => new StringWriter(_error);
        public string Output => _output.ToString();
        public string ErrorOutput => _error.ToString();
    }

    private sealed class FakeFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> _files;
        public Dictionary<string, string> Written { get; } = new();

        public FakeFileSystem(params (string Path, string Content)[] files) =>
            _files = files.ToDictionary(static file => file.Path, static file => file.Content, StringComparer.Ordinal);

        public bool FileExists(string path) => _files.ContainsKey(path);
        public string ReadAllText(string path) => _files.TryGetValue(path, out string? content) ? content : throw new FileNotFoundException(path);
        public void WriteAllText(string path, string contents) => Written[path] = contents;
        public string WriteAllTextToTemp(string targetPath, string contents) => targetPath;
        public void RenameTempToTarget(string tempPath, string targetPath) { }
        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => true;
        public void DeleteFile(string path) { }
        public bool TryCreateNewFile(string path) => true;
        public bool DirectoryExists(string path) => true;
        public void DeleteDirectoryIfEmpty(string path) { }
        public bool CanWriteToDirectory(string path) => true;
    }
}
