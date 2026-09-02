using System.CommandLine;
using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Core.Change;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class ReportCommandHandlerTests
{
    [Test]
    public void Execute_ShowHelp_WritesUsageAndSucceeds()
    {
        FakeConsole console = new();

        int exitCode = CreateHandler(console, new FakeFileSystem()).Execute(
            new PrReportCommandOptions(string.Empty, string.Empty, null, 20, true));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("arch-linter-net report pr"));
        });
    }

    [Test]
    public void Execute_InvalidOptions_FailsWithoutReadingInputs()
    {
        FakeConsole console = new();

        int exitCode = CreateHandler(console, new FakeFileSystem()).Execute(
            new PrReportCommandOptions("health.json", "change.json", null, 0, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("positive --max-details"));
        });
    }

    [Test]
    public void Execute_OutputPathMatchingInput_FailsClosed()
    {
        FakeConsole console = new();

        int exitCode = CreateHandler(console, new FakeFileSystem()).Execute(
            new PrReportCommandOptions("health.json", "change.json", "health.json", 20, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("matches --health input"));
        });
    }

    [Test]
    public void Execute_MalformedArtifacts_ReturnsEstablishedErrorCode()
    {
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("health.json", "{}"), ("change.json", "{}"));

        int exitCode = CreateHandler(console, fileSystem).Execute(
            new PrReportCommandOptions("health.json", "change.json", null, 20, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("Could not render architecture PR report"));
        });
    }

    [Test]
    public void Execute_LegacyHealthAndCanonicalChange_FailsClosed()
    {
        const string Health =
            "{\"schema_id\":\"architecture-health/v1\",\"gate\":\"pass\",\"health\":\"healthy\",\"dimensions\":[]}";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(
            ("health.json", Health),
            ("change.json", EmptyChange()));

        int exitCode = CreateHandler(console, fileSystem).Execute(
            new PrReportCommandOptions("health.json", "change.json", "report.md", 20, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Is.Empty);
            Assert.That(fileSystem.Written.ContainsKey("report.md"), Is.False);
            Assert.That(console.ErrorOutput, Does.Contain("report_evidence"));
        });
    }

    [Test]
    public void Definition_PrSubcommand_ParsesOptionsBeforeRejectingLegacyHealth()
    {
        const string Health =
            "{\"schema_id\":\"architecture-health/v1\",\"gate\":\"pass\",\"health\":\"healthy\",\"dimensions\":[]}";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(
            ("health.json", Health),
            ("change.json", EmptyChange()));
        RootCommand root = new();
        root.Subcommands.Add(new ReportCommandDefinition(CreateHandler(console, fileSystem)).Create());

        int exitCode = root.Parse(["report", "pr", "--health", "health.json", "--change", "change.json", "--max-details", "3"])
            .Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("report_evidence"));
        });
    }

    private static ReportCommandHandler CreateHandler(FakeConsole console, FakeFileSystem fileSystem) =>
        new(console, fileSystem);

    private static string EmptyChange() => ArchitectureChangeReports.FormatJson(
        new ArchitectureChangeReport([], [], [], [], [])
        {
            ExecutionContext = new ArchitectureChangeReportContext("run", "strict", string.Empty),
        });

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

        public FakeFileSystem(params (string Path, string Content)[] files) =>
            _files = files.ToDictionary(static file => file.Path, static file => file.Content, StringComparer.Ordinal);

        public Dictionary<string, string> Written { get; } = new();

        public bool FileExists(string path) => _files.ContainsKey(path);

        public string ReadAllText(string path) =>
            _files.TryGetValue(path, out string? content) ? content : throw new FileNotFoundException(path);

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
