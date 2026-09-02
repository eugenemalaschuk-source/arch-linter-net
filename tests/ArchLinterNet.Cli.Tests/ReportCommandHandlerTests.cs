using System.CommandLine;
using System.Text;
using System.Text.Json.Nodes;
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
    public void FindOutputCollision_UsesCurrentPlatformCaseSemantics()
    {
        string? collision = ReportCommandHandler.FindOutputCollision(
            new PrReportCommandOptions("health.json", "change.json", "HEALTH.json", 20, false));

        if (OperatingSystem.IsWindows())
        {
            Assert.That(collision, Does.Contain("--health"));
        }
        else
        {
            Assert.That(collision, Is.Null);
        }
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
    public void Execute_NullChangeArrayElement_ReturnsInvalidArtifactErrorCode()
    {
        JsonNode change = JsonNode.Parse(EmptyChange())!;
        change["added"] = new JsonArray((JsonNode?)null);
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(
            ("health.json", CorrelatableHealth()),
            ("change.json", change.ToJsonString()));

        int exitCode = CreateHandler(console, fileSystem).Execute(
            new PrReportCommandOptions("health.json", "change.json", null, 20, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.ErrorOutput, Does.Contain("invalid entry"));
            Assert.That(console.ErrorOutput, Does.Not.Contain("NullReferenceException"));
        });
    }

    [Test]
    public void Execute_LegacyHealthAndCanonicalChange_RendersUnavailableEvidence()
    {
        const string Health =
            "{\"schema_id\":\"architecture-health/v1\",\"gate\":\"pass\",\"health\":\"healthy\",\"dimensions\":[]}";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(
            ("health.json", Health),
            ("change.json", EmptyChange()));

        int exitCode = CreateHandler(console, fileSystem).Execute(
            new PrReportCommandOptions("health.json", "change.json", null, 20, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("Report availability: `unavailable`"));
            Assert.That(console.Output, Does.Contain("Effective policy controls: `unavailable`"));
            Assert.That(console.Output, Does.Contain("Explicit waiver debt: `unavailable`"));
            Assert.That(console.Output, Does.Not.Contain("Effective policy controls: `0`"));
            Assert.That(console.ErrorOutput, Is.Empty);
        });
    }

    [Test]
    public void Definition_PrSubcommand_ParsesOptionsForLegacyHealth()
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
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("Report availability: `unavailable`"));
            Assert.That(console.ErrorOutput, Is.Empty);
        });
    }

    [Test]
    public void Execute_OutputPath_StagesThenRenamesRenderedReport()
    {
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(
            ("health.json", CorrelatableHealth()),
            ("change.json", EmptyChange()));

        int exitCode = CreateHandler(console, fileSystem).Execute(
            new PrReportCommandOptions("health.json", "change.json", "report.md", 20, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(fileSystem.DirectWrites, Is.Empty);
            Assert.That(fileSystem.Staged, Contains.Key("report.md.tmp"));
            Assert.That(fileSystem.Renames, Is.EqualTo(new[] { ("report.md.tmp", "report.md") }));
            Assert.That(fileSystem.Written["report.md"], Does.Contain("# Architecture PR report"));
        });
    }

    [Test]
    public void Execute_OutputRenameFailure_PreservesExistingTarget()
    {
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(
            ("health.json", CorrelatableHealth()),
            ("change.json", EmptyChange()))
        {
            ThrowOnRename = true,
        };
        fileSystem.Written["report.md"] = "existing report";

        int exitCode = CreateHandler(console, fileSystem).Execute(
            new PrReportCommandOptions("health.json", "change.json", "report.md", 20, false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(fileSystem.DirectWrites, Is.Empty);
            Assert.That(fileSystem.Staged, Contains.Key("report.md.tmp"));
            Assert.That(fileSystem.Written["report.md"], Is.EqualTo("existing report"));
        });
    }

    private static ReportCommandHandler CreateHandler(FakeConsole console, FakeFileSystem fileSystem) =>
        new(console, fileSystem);

    private static string EmptyChange() => ArchitectureChangeReports.FormatJson(
        new ArchitectureChangeReport([], [], [], [], [])
        {
            ExecutionContext = new ArchitectureChangeReportContext("run", "strict", string.Empty),
        });

    private static string CorrelatableHealth() =>
        """
        {
          "schema_id": "architecture-health/v1",
          "gate": "pass",
          "health": "healthy",
          "dimensions": [],
          "report_evidence": {
            "schema_version": 2,
            "kind": "architecture-health-report-evidence",
            "gate": "pass",
            "health": "healthy",
            "execution_context": { "execution_id": "run", "condition_set": "" },
            "validation_outcomes": [
              {
                "mode": "strict",
                "availability": {
                  "applicability": "unavailable",
                  "external_evidence": "not_configured",
                  "findings": "available",
                  "policy_inventory": "unavailable",
                  "topology": "not_configured",
                  "waiver_lifecycle": "unavailable"
                },
                "findings": [],
                "provenance": {
                  "repository_root": "/repo",
                  "policy_import_paths": [],
                  "resolved_assembly_paths": [],
                  "discovered_project_paths": []
                }
              }
            ],
            "debt_gate": {
              "succeeded": true,
              "passed": true,
              "evaluation": {
                "completed": true,
                "mode": "strict",
                "reused_analysis_snapshot": true,
                "preflight_diagnostics": []
              },
              "persistent_debt": {
                "succeeded": true,
                "in_sync": true,
                "entries": [],
                "configuration_violations": []
              },
              "policy_weakening": { "requested": false }
            }
          }
        }
        """;

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

        public Dictionary<string, string> DirectWrites { get; } = new();

        public Dictionary<string, string> Staged { get; } = new();

        public List<(string TempPath, string TargetPath)> Renames { get; } = [];

        public bool ThrowOnRename { get; init; }

        public bool FileExists(string path) => _files.ContainsKey(path);

        public string ReadAllText(string path) =>
            _files.TryGetValue(path, out string? content) ? content : throw new FileNotFoundException(path);

        public void WriteAllText(string path, string contents) => DirectWrites[path] = contents;

        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            string tempPath = targetPath + ".tmp";
            Staged[tempPath] = contents;
            return tempPath;
        }

        public void RenameTempToTarget(string tempPath, string targetPath)
        {
            if (ThrowOnRename)
            {
                throw new IOException("The staged report could not be renamed.");
            }

            Renames.Add((tempPath, targetPath));
            Written[targetPath] = Staged[tempPath];
        }

        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => true;

        public void DeleteFile(string path) { }

        public bool TryCreateNewFile(string path) => true;

        public bool DirectoryExists(string path) => true;

        public void DeleteDirectoryIfEmpty(string path) { }

        public bool CanWriteToDirectory(string path) => true;
    }
}
