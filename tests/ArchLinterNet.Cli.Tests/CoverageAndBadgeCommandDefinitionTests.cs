using System.CommandLine;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands.Badge.Application;
using ArchLinterNet.Cli.Commands.Coverage.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class CoverageAndBadgeCommandDefinitionTests
{
    [Test]
    public void CoverageDefinition_ReportSubcommand_InvokesHandlerWithParsedOptions()
    {
        const string Report = """{"mode":"strict","passed":true,"coverage_summary":[]}""";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", Report));
        RootCommand root = new();
        root.Subcommands.Add(new CoverageCommandDefinition(new CoverageCommandHandler(console, fileSystem)).Create());

        int exitCode = root.Parse(["coverage", "report", "--input", "input.json", "--max-failure-diagnostics", "3"]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(console.Output, Does.Contain("## Architecture coverage"));
        });
    }

    [Test]
    public void CoverageDefinition_ReportSubcommand_HelpFlagShortCircuits()
    {
        FakeConsole console = new();
        RootCommand root = new();
        root.Subcommands.Add(new CoverageCommandDefinition(new CoverageCommandHandler(console, new FakeFileSystem())).Create());

        int exitCode = root.Parse(["coverage", "report", "-h"]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(console.Output, Does.Contain("arch-linter-net coverage report"));
        });
    }

    [Test]
    public void CoverageDefinition_ExtractSubcommand_InvokesHandlerAndWritesOutput()
    {
        const string Combined = """{"results":[{"mode":"strict","passed":true}]}""";
        FakeFileSystem fileSystem = new(("input.json", Combined));
        RootCommand root = new();
        root.Subcommands.Add(new CoverageCommandDefinition(new CoverageCommandHandler(new FakeConsole(), fileSystem)).Create());

        int exitCode = root.Parse(["coverage", "extract", "--input", "input.json", "--mode", "strict", "--output", "out.json"]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(fileSystem.Written["out.json"], Does.Contain("\"mode\":\"strict\""));
        });
    }

    [Test]
    public void BadgeDefinition_ArchitecturePolicySubcommand_InvokesHandlerWithParsedOptions()
    {
        const string Report = """{"mode":"strict","passed":true}""";
        FakeConsole console = new();
        FakeFileSystem fileSystem = new(("input.json", Report));
        RootCommand root = new();
        root.Subcommands.Add(new BadgeCommandDefinition(new BadgeCommandHandler(console, fileSystem)).Create());

        int exitCode = root.Parse(["badge", "architecture-policy", "--input", "input.json"]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(console.Output, Does.Contain("\"message\":\"passing\""));
        });
    }

    [Test]
    public void BadgeDefinition_ArchitecturePolicySubcommand_HelpFlagShortCircuits()
    {
        FakeConsole console = new();
        RootCommand root = new();
        root.Subcommands.Add(new BadgeCommandDefinition(new BadgeCommandHandler(console, new FakeFileSystem())).Create());

        int exitCode = root.Parse(["badge", "architecture-policy", "-h"]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(console.Output, Does.Contain("arch-linter-net badge architecture-policy"));
        });
    }

    [Test]
    public void BadgeDefinition_ArchitectureHealthSubcommand_WritesRequestedOutput()
    {
        const string Health =
            """
            {"schema_id":"architecture-health/v1","gate":"pass","health":"debt","dimensions":[],"report_evidence":{"schema_version":2,"kind":"architecture-health-report-evidence","gate":"pass","health":"debt","validation_outcomes":[{"mode":"strict","availability":{"policy_inventory":"available"},"findings":[],"provenance":{},"policy_inventory":{"schema":"architecture-policy-inventory/v1","effective_rule_count":42,"ignore_debt":{"total":7}}}],"debt_gate":{}}}
            """;
        FakeFileSystem fileSystem = new(("input.json", Health));
        RootCommand root = new();
        root.Subcommands.Add(new BadgeCommandDefinition(new BadgeCommandHandler(new FakeConsole(), fileSystem)).Create());

        int exitCode = root.Parse(["badge", "architecture-health", "--input", "input.json", "--output", "badge.json"]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            using JsonDocument badge = JsonDocument.Parse(fileSystem.Written["badge.json"]);
            Assert.That(badge.RootElement.GetProperty("message").GetString(), Is.EqualTo("DEBT · 7 ignores · 42 rules"));
        });
    }

    [Test]
    public void BadgeDefinition_ArchitectureHealthSubcommand_HelpFlagShortCircuits()
    {
        FakeConsole console = new();
        RootCommand root = new();
        root.Subcommands.Add(new BadgeCommandDefinition(new BadgeCommandHandler(console, new FakeFileSystem())).Create());

        int exitCode = root.Parse(["badge", "architecture-health", "-h"]).Invoke();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(console.Output, Does.Contain("arch-linter-net badge architecture-health"));
        });
    }

    private sealed class FakeConsole : ICliConsole
    {
        private readonly StringBuilder _output = new();
        public TextWriter Out => new StringWriter(_output);
        public TextWriter Error => TextWriter.Null;
        public string Output => _output.ToString();
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
        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            string temporaryPath = targetPath + ".tmp";
            Written[temporaryPath] = contents;
            return temporaryPath;
        }

        public void RenameTempToTarget(string tempPath, string targetPath) => Written[targetPath] = Written[tempPath];
        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => true;
        public void DeleteFile(string path) { }
        public bool TryCreateNewFile(string path) => true;
        public bool DirectoryExists(string path) => true;
        public void DeleteDirectoryIfEmpty(string path) { }
        public bool CanWriteToDirectory(string path) => true;
    }
}
