using System.Diagnostics;
using System.Text;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using ArchLinterNet.Cli.Commands.History.Application;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
public sealed class HistoryIngestCommandHandlerTests
{
    [Test]
    public void MissingOperandsFailWithoutTouchingTheRepository()
    {
        FakeConsole console = new();

        int exitCode = new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem()).Execute(
            new HistoryIngestCommandOptions(".", string.Empty, "HEAD", "json", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Is.Empty);
            Assert.That(console.ErrorOutput, Does.Contain("--from"));
        });
    }

    [Test]
    public void AnUnsupportedFormatIsRejected()
    {
        FakeConsole console = new();

        int exitCode = new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem()).Execute(
            new HistoryIngestCommandOptions(".", "HEAD", "HEAD", "text", false));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Is.Empty);
        });
    }

    // The fail-closed rule at the command boundary: a diagnostic goes to the error stream and the
    // output stream stays completely empty.
    [Test]
    public void AFailClosedRunWritesADiagnosticAndNoResult()
    {
        FakeConsole console = new();
        string outsideAnyRepository = Path.Combine(Path.GetTempPath(), "arch-linter-history-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideAnyRepository);
        try
        {
            int exitCode = new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem()).Execute(
                new HistoryIngestCommandOptions(outsideAnyRepository, "HEAD", "HEAD", "json", false));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(CliExitCodes.Success));
                Assert.That(console.Output, Is.Empty);
                Assert.That(console.ErrorOutput, Does.Contain("\"kind\": \"repository_not_found\""));
            });
        }
        finally
        {
            Directory.Delete(outsideAnyRepository, recursive: true);
        }
    }

    [Test]
    public void HelpWritesUsageAndSucceeds()
    {
        FakeConsole console = new();

        int exitCode = new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem()).Execute(
            new HistoryIngestCommandOptions(".", string.Empty, string.Empty, "json", true));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.Output, Does.Contain("arch-linter-net history analyze"));
            Assert.That(console.Output, Does.Contain("--report"));
        });
    }

    [Test]
    public void MarkdownIsASupportedReportFormat()
    {
        FakeConsole console = new();
        string outsideAnyRepository = Path.Combine(Path.GetTempPath(), "arch-linter-history-markdown-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideAnyRepository);
        try
        {
            int exitCode = new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem()).Execute(
                new HistoryIngestCommandOptions(outsideAnyRepository, "HEAD", "HEAD", "markdown", false));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(CliExitCodes.Success));
                Assert.That(console.Output, Is.Empty);
                Assert.That(console.ErrorOutput, Does.Contain("\"kind\": \"repository_not_found\""));
            });
        }
        finally
        {
            Directory.Delete(outsideAnyRepository, recursive: true);
        }
    }

    [Test]
    public void InvalidSelectedPolicyFailsBeforeRepositoryIngestion()
    {
        FakeConsole console = new();
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-history-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string policyPath = Path.Combine(directory, "policy.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: invalid history policy
            layers: {}
            analysis:
              target_assemblies: [App]
            contracts:
              strict: []
            history_analysis:
              thresholds:
                co_change_significance: 1.000000001
            """);

        try
        {
            int exitCode = new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem()).Execute(
                new HistoryIngestCommandOptions(directory, "HEAD", "HEAD", "json", false, policyPath));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
                Assert.That(console.Output, Is.Empty);
                Assert.That(console.ErrorOutput, Does.Contain("\"kind\": \"configuration_invalid\""));
                Assert.That(console.ErrorOutput, Does.Contain("co_change_significance"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SerializationFailureWritesOnlyTheDedicatedDiagnostic()
    {
        FakeConsole console = new();

        bool succeeded = HistoryReportOutputWriter.TryWriteJson(
            console,
            static () => "bad\uD800");

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.False);
            Assert.That(console.Output, Is.Empty);
            Assert.That(console.ErrorOutput, Does.StartWith("{\n  \"kind\": \"report_serialization_invalid\""));
            Assert.That(console.ErrorOutput, Does.Not.Contain("candidates"));
        });
    }

    [Test]
    public void AnInvalidReportValueFailsBeforeRepositoryIngestion()
    {
        FakeConsole console = new();

        int exitCode = new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem()).Execute(
            new HistoryIngestCommandOptions(
                Path.GetTempPath(), "HEAD", "HEAD", "json", false,
                ReportSinks: Array.Empty<HistoryReportSink>(),
                ReportParseError: "Invalid --report value: 'json'. Use format=destination (e.g. json=report.json)."));

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
            Assert.That(console.Output, Is.Empty);
            Assert.That(console.ErrorOutput, Does.Contain("Invalid --report value"));
        });
    }

    [Test]
    public void AReportDestinationCollidingWithThePolicyPathFailsBeforeIngestion()
    {
        FakeConsole console = new();
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-history-collision-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string policyPath = Path.Combine(directory, "policy.yml");
        try
        {
            int exitCode = new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem()).Execute(
                new HistoryIngestCommandOptions(
                    directory, "HEAD", "HEAD", "json", false, policyPath,
                    ReportSinks: new[] { new HistoryReportSink("json", HistoryReportDestinationType.File, policyPath) }));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
                Assert.That(console.Output, Is.Empty);
                Assert.That(console.ErrorOutput, Does.Contain("matches --policy input"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void ACaseDifferentReportDestinationCollidesWithThePolicyPath()
    {
        FakeConsole console = new();
        string directory = Path.Combine(Path.GetTempPath(), "arch-linter-history-case-collision-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string policyPath = Path.Combine(directory, "policy.yml");
        string differentlyCasedDestination = Path.Combine(directory, "POLICY.YML");
        try
        {
            int exitCode = new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem()).Execute(
                new HistoryIngestCommandOptions(
                    directory, "HEAD", "HEAD", "json", false, policyPath,
                    ReportSinks: new[] { new HistoryReportSink("json", HistoryReportDestinationType.File, differentlyCasedDestination) }));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidArgumentsOrRuntimeError));
                Assert.That(console.Output, Is.Empty);
                Assert.That(console.ErrorOutput, Does.Contain("matches --policy input"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void AnInvalidFormatIsIgnoredWhenReportSinksArePresent()
    {
        FakeConsole console = new();
        string outsideAnyRepository = Path.Combine(Path.GetTempPath(), "arch-linter-history-format-ignored-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideAnyRepository);
        try
        {
            int exitCode = new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem()).Execute(
                new HistoryIngestCommandOptions(
                    outsideAnyRepository, "HEAD", "HEAD", "not-a-real-format", false,
                    ReportSinks: new[] { new HistoryReportSink("json", HistoryReportDestinationType.File, "report.json") }));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(CliExitCodes.Success));
                Assert.That(console.ErrorOutput, Does.Not.Contain("Unsupported --format"));
                Assert.That(console.ErrorOutput, Does.Contain("\"kind\": \"repository_not_found\""));
            });
        }
        finally
        {
            Directory.Delete(outsideAnyRepository, recursive: true);
        }
    }

    [Test]
    public void AFailClosedRunWithReportSinksWritesADiagnosticAndNoResult()
    {
        FakeConsole console = new();
        string outsideAnyRepository = Path.Combine(Path.GetTempPath(), "arch-linter-history-sinks-fail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideAnyRepository);
        try
        {
            int exitCode = new HistoryIngestCommandHandler(console, new ScaffoldTestFileSystem()).Execute(
                new HistoryIngestCommandOptions(
                    outsideAnyRepository, "HEAD", "HEAD", "json", false,
                    ReportSinks: new[]
                    {
                        new HistoryReportSink("json", HistoryReportDestinationType.File, "report.json"),
                        new HistoryReportSink("markdown", HistoryReportDestinationType.File, "report.md"),
                    }));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(CliExitCodes.Success));
                Assert.That(console.Output, Is.Empty);
                Assert.That(console.ErrorOutput, Does.Contain("\"kind\": \"repository_not_found\""));
            });
        }
        finally
        {
            Directory.Delete(outsideAnyRepository, recursive: true);
        }
    }

    [Test]
    public void AWriteFailureOnOneSinkCommitsNoSinkAndExitsNonZero()
    {
        string repository = CreateRepositoryWithOneCommit();
        try
        {
            FakeConsole console = new();
            ScaffoldTestFileSystem fileSystem = new();
            FailOnWriteFileSystem failingFileSystem = new(fileSystem, "report.md");

            int exitCode = new HistoryIngestCommandHandler(console, failingFileSystem).Execute(
                new HistoryIngestCommandOptions(
                    repository, "HEAD", "HEAD", "json", false,
                    ReportSinks: new[]
                    {
                        new HistoryReportSink("json", HistoryReportDestinationType.File, "report.json"),
                        new HistoryReportSink("markdown", HistoryReportDestinationType.File, "report.md"),
                    }));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.EqualTo(CliExitCodes.Success));
                Assert.That(console.Output, Is.Empty);
                Assert.That(console.ErrorOutput, Does.Contain("report.md"));
                Assert.That(fileSystem.CommittedPaths, Is.Empty);
            });
        }
        finally
        {
            DeleteRepositoryDirectory(repository);
        }
    }

    [Test]
    public void OneIngestionProducesBothFormatsMatchingSingleFormatRuns()
    {
        string repository = CreateRepositoryWithOneCommit();
        try
        {
            FakeConsole jsonOnlyConsole = new();
            new HistoryIngestCommandHandler(jsonOnlyConsole, new ScaffoldTestFileSystem()).Execute(
                new HistoryIngestCommandOptions(repository, "HEAD", "HEAD", "json", false));

            FakeConsole markdownOnlyConsole = new();
            new HistoryIngestCommandHandler(markdownOnlyConsole, new ScaffoldTestFileSystem()).Execute(
                new HistoryIngestCommandOptions(repository, "HEAD", "HEAD", "markdown", false));

            FakeConsole sinkConsole = new();
            ScaffoldTestFileSystem fileSystem = new();
            int exitCode = new HistoryIngestCommandHandler(sinkConsole, fileSystem).Execute(
                new HistoryIngestCommandOptions(
                    repository, "HEAD", "HEAD", "json", false,
                    ReportSinks: new[]
                    {
                        new HistoryReportSink("json", HistoryReportDestinationType.File, "report.json"),
                        new HistoryReportSink("markdown", HistoryReportDestinationType.File, "report.md"),
                    }));

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
                Assert.That(sinkConsole.Output, Is.Empty);
                Assert.That(sinkConsole.ErrorOutput, Is.Empty);
                Assert.That(fileSystem.Contents["report.json"], Is.EqualTo(jsonOnlyConsole.Output));
                Assert.That(fileSystem.Contents["report.md"], Is.EqualTo(markdownOnlyConsole.Output));
            });
        }
        finally
        {
            DeleteRepositoryDirectory(repository);
        }
    }

    private static string CreateRepositoryWithOneCommit()
    {
        string path = Path.Combine(Path.GetTempPath(), "arch-linter-history-sinks-ok-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        RunGit(path, "init", "-q", "-b", "main");
        RunGit(path, "config", "user.name", "Fixture Author");
        RunGit(path, "config", "user.email", "fixture@example.com");
        RunGit(path, "config", "commit.gpgsign", "false");
        RunGit(path, "commit", "--allow-empty", "-q", "-m", "initial");
        return path;
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
        }
    }

    private static void DeleteRepositoryDirectory(string path)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class FailOnWriteFileSystem(IFileSystem inner, string failingTargetPath) : IFileSystem
    {
        public bool FileExists(string path) => inner.FileExists(path);

        public string ReadAllText(string path) => inner.ReadAllText(path);

        public void WriteAllText(string path, string contents) => inner.WriteAllText(path, contents);

        public string WriteAllTextToTemp(string targetPath, string contents)
        {
            if (string.Equals(targetPath, failingTargetPath, StringComparison.Ordinal))
            {
                throw new IOException($"Cannot write to {targetPath}");
            }

            return inner.WriteAllTextToTemp(targetPath, contents);
        }

        public void RenameTempToTarget(string tempPath, string targetPath) => inner.RenameTempToTarget(tempPath, targetPath);

        public bool TryRenameTempToNewTarget(string tempPath, string targetPath) => inner.TryRenameTempToNewTarget(tempPath, targetPath);

        public void DeleteFile(string path) => inner.DeleteFile(path);

        public bool TryCreateNewFile(string path) => inner.TryCreateNewFile(path);

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);

        public void DeleteDirectoryIfEmpty(string path) => inner.DeleteDirectoryIfEmpty(path);

        public bool CanWriteToDirectory(string path) => inner.CanWriteToDirectory(path);
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
}
