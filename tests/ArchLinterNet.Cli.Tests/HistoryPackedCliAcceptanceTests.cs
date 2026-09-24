using System.Diagnostics;
using System.Text;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

[TestFixture]
[NonParallelizable]
[Category("E2E")]
public sealed class HistoryPackedCliAcceptanceTests
{
    private const string PackageVersion = "0.0.0-history1016";

    [Test]
    [CancelAfter(300_000)]
    public void FreshlyPackedCliProducesBothFormatsFromOneCoreIngestion()
    {
        using PackedCliFixture fixture = PackedCliFixture.Create();
        fixture.PackAndInstall();

        ProcessResult jsonOnly = fixture.RunTool(
            "history", "analyze", "--repository", fixture.RepositoryPath,
            "--from", fixture.From, "--to", fixture.To, "--format", "json", "--timings");
        ProcessResult markdownOnly = fixture.RunTool(
            "history", "analyze", "--repository", fixture.RepositoryPath,
            "--from", fixture.From, "--to", fixture.To, "--format", "markdown", "--timings");

        string jsonPath = Path.Combine(fixture.OutputDirectory, "history.json");
        string markdownPath = Path.Combine(fixture.OutputDirectory, "history.md");
        ProcessResult packed = fixture.RunTool(
            "history", "analyze", "--repository", fixture.RepositoryPath,
            "--from", fixture.From, "--to", fixture.To,
            "--report", $"json={jsonPath}",
            "--report", $"markdown={markdownPath}",
            "--timings");

        Assert.Multiple(() =>
        {
            AssertSuccessfulSingleIngestion(jsonOnly, "single-format JSON");
            AssertSuccessfulSingleIngestion(markdownOnly, "single-format Markdown");
            AssertSuccessfulSingleIngestion(packed, "packed multi-output invocation");
            Assert.That(packed.StandardOutput, Is.Empty, "File sinks must not contaminate machine stdout.");
            Assert.That(File.ReadAllBytes(jsonPath), Is.EqualTo(Utf8WithoutBom(jsonOnly.StandardOutput)));
            Assert.That(File.ReadAllBytes(markdownPath), Is.EqualTo(Utf8WithoutBom(markdownOnly.StandardOutput)));
        });
    }

    private static void AssertSuccessfulSingleIngestion(ProcessResult result, string operation)
    {
        Assert.That(result.ExitCode, Is.Zero, $"{operation} failed: {result.StandardError}");
        Assert.That(ReadIngestionCallCount(result.StandardError), Is.EqualTo(1),
            $"{operation} must execute the actual Core ingestion service exactly once.");
    }

    private static int ReadIngestionCallCount(string standardError)
    {
        string line = standardError.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Single(value => value.StartsWith("History timings (ms):", StringComparison.Ordinal));
        string count = line.Split(';', StringSplitOptions.TrimEntries)
            .Single(part => part.StartsWith("ingestion_calls=", StringComparison.Ordinal))
            .Split('=', 2)[1];
        return int.Parse(count, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Utf8WithoutBom(string content) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);

    private sealed class PackedCliFixture : IDisposable
    {
        private const string FixtureMessage = "history issue 1016 acceptance fixture";

        private PackedCliFixture(string rootPath, string repositoryPath, string from, string to)
        {
            RootPath = rootPath;
            RepositoryPath = repositoryPath;
            From = from;
            To = to;
            FeedPath = Path.Combine(rootPath, "feed");
            ToolPath = Path.Combine(rootPath, "tool");
            OutputDirectory = Path.Combine(rootPath, "output");
            Directory.CreateDirectory(FeedPath);
            Directory.CreateDirectory(ToolPath);
            Directory.CreateDirectory(OutputDirectory);
        }

        private string RootPath { get; }
        private string FeedPath { get; }
        private string ToolPath { get; }
        public string RepositoryPath { get; }
        public string OutputDirectory { get; }
        public string From { get; }
        public string To { get; }

        public static PackedCliFixture Create()
        {
            string root = Path.Combine(Path.GetTempPath(), $"arch-linter-history-packed-{Guid.NewGuid():N}");
            string repository = Path.Combine(root, "repository");
            Directory.CreateDirectory(repository);
            Run("git", repository, "init", "-q", "-b", "main");
            Run("git", repository, "config", "user.name", "ArchLinterNet History Acceptance");
            Run("git", repository, "config", "user.email", "history-acceptance@example.invalid");
            Run("git", repository, "config", "commit.gpgsign", "false");
            File.WriteAllText(Path.Combine(repository, "history.txt"), "initial\n");
            Run("git", repository, "add", "history.txt");
            Run("git", repository, "commit", "-q", "-m", "base");
            string from = Run("git", repository, "rev-parse", "HEAD").StandardOutput.Trim();
            File.AppendAllText(Path.Combine(repository, "history.txt"), FixtureMessage + "\n");
            Run("git", repository, "commit", "-a", "-q", "-m", FixtureMessage);
            string to = Run("git", repository, "rev-parse", "HEAD").StandardOutput.Trim();
            return new PackedCliFixture(root, repository, from, to);
        }

        public void PackAndInstall()
        {
            string repositoryRoot = FindRepositoryRoot();
            AssertSuccessfulProcess(Run("dotnet", repositoryRoot,
                "pack", "src/ArchLinterNet.Cli/ArchLinterNet.Cli.csproj",
                "--configuration", "Release", "--no-restore", "--output", FeedPath,
                $"/p:PackageVersion={PackageVersion}", $"/p:Version={PackageVersion}",
                "/p:UseSharedCompilation=false", "/p:NodeReuse=false", "--disable-build-servers"), "pack CLI");

            string package = Directory.EnumerateFiles(FeedPath, "ArchLinterNet.Cli.*.nupkg").Single();
            Assert.That(new FileInfo(package).Length, Is.GreaterThan(0), "The packed CLI package must not be empty.");

            AssertSuccessfulProcess(Run("dotnet", repositoryRoot,
                "tool", "install", "ArchLinterNet.Cli", "--tool-path", ToolPath,
                "--add-source", FeedPath, "--ignore-failed-sources", "--version", PackageVersion), "install packed CLI");
        }

        public ProcessResult RunTool(params string[] arguments)
        {
            string executable = Path.Combine(ToolPath, OperatingSystem.IsWindows() ? "arch-linter-net.exe" : "arch-linter-net");
            Assert.That(File.Exists(executable), Is.True, $"The packed CLI shim was not installed at {executable}.");
            return Run(executable, RepositoryPath, arguments);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static ProcessResult Run(string executable, string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo startInfo = new(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{executable}'.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(240_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"'{executable} {string.Join(' ', arguments)}' did not exit within four minutes.");
        }

        Task.WaitAll(stdout, stderr);
        return new ProcessResult(process.ExitCode, stdout.Result, stderr.Result);
    }

    private static void AssertSuccessfulProcess(ProcessResult result, string operation) =>
        Assert.That(result.ExitCode, Is.Zero, $"Could not {operation}: {result.StandardOutput}{result.StandardError}");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArchLinterNet.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
