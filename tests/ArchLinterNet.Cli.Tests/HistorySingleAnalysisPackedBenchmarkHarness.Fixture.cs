using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace ArchLinterNet.Cli.Tests;

public sealed partial class HistorySingleAnalysisPackedBenchmarkHarness
{
    private sealed class Fixture : IDisposable
    {
        public const string PackedToolVersion = "0.0.0-history1016";

        private Fixture(string rootPath, string repositoryPath, string from, string to, int commitCount)
        {
            RootPath = rootPath;
            RepositoryPath = repositoryPath;
            From = from;
            To = to;
            CommitCount = commitCount;
            OutputDirectory = Path.Combine(repositoryPath, ".benchmark-output");
            Directory.CreateDirectory(OutputDirectory);
        }

        private string RootPath { get; }
        public string RepositoryPath { get; }
        public string OutputDirectory { get; }
        public string From { get; }
        public string To { get; }
        public int CommitCount { get; }
        public string PackageSha256 { get; private set; } = string.Empty;
        public string SdkVersion { get; private set; } = string.Empty;

        public static Fixture Create()
        {
            string rootPath = Path.Combine(Path.GetTempPath(), $"arch-linter-history-benchmark-{Guid.NewGuid():N}");
            string repositoryPath = Path.Combine(rootPath, "repository");
            Directory.CreateDirectory(repositoryPath);
            Git(repositoryPath, "init", "-q", "-b", "main");
            Git(repositoryPath, "config", "user.name", "ArchLinterNet Benchmark");
            Git(repositoryPath, "config", "user.email", "benchmark@example.invalid");
            Git(repositoryPath, "config", "commit.gpgsign", "false");
            for (int index = 0; index < 5; index++)
            {
                string sourceDirectory = Path.Combine(repositoryPath, "src");
                Directory.CreateDirectory(sourceDirectory);
                File.WriteAllText(
                    Path.Combine(sourceDirectory, $"Fixture{index}.cs"),
                    $"namespace Fixture; public sealed class Fixture{index} {{ }}\n");
                Git(repositoryPath, "add", ".");
                Git(repositoryPath, "commit", "-q", "-m", $"fixture {index}");
            }

            string from = Git(repositoryPath, "rev-list", "--max-parents=0", "HEAD").Trim();
            string to = Git(repositoryPath, "rev-parse", "HEAD").Trim();
            return new Fixture(rootPath, repositoryPath, from, to, 5);
        }

        public string PackAndInstall()
        {
            string feedPath = Path.Combine(RootPath, "feed");
            string toolPath = Path.Combine(RootPath, "tool");
            Directory.CreateDirectory(feedPath);
            Directory.CreateDirectory(toolPath);
            ProcessResult sdk = RunProcess("dotnet", FindRepositoryRoot(), "--version");
            AssertProcessSucceeded(sdk, "read .NET SDK version");
            SdkVersion = sdk.StandardOutput.Trim();
            ProcessResult pack = RunProcess("dotnet", FindRepositoryRoot(),
                "pack", "src/ArchLinterNet.Cli/ArchLinterNet.Cli.csproj",
                "--configuration", "Release", "--no-restore", "--output", feedPath,
                $"/p:PackageVersion={PackedToolVersion}", $"/p:Version={PackedToolVersion}",
                "/p:UseSharedCompilation=false", "/p:NodeReuse=false", "--disable-build-servers");
            AssertProcessSucceeded(pack, "pack CLI");
            string packagePath = Directory.EnumerateFiles(feedPath, "ArchLinterNet.Cli.*.nupkg").Single();
            PackageSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(packagePath))).ToLowerInvariant();

            ProcessResult install = RunProcess("dotnet", RepositoryPath,
                "tool", "install", "ArchLinterNet.Cli", "--tool-path", toolPath,
                "--add-source", feedPath, "--ignore-failed-sources", "--version", PackedToolVersion);
            AssertProcessSucceeded(install, "install packed CLI");

            string executableName = OperatingSystem.IsWindows() ? "arch-linter-net.exe" : "arch-linter-net";
            string executablePath = Path.Combine(toolPath, executableName);
            Assert.That(File.Exists(executablePath), Is.True, executablePath);
            return executablePath;
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

    private static ProcessResult RunProcess(string executable, string workingDirectory, params string[] arguments)
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

    private static void AssertProcessSucceeded(ProcessResult result, string operation) =>
        Assert.That(result.ExitCode, Is.Zero, $"Could not {operation}: {result.StandardOutput}{result.StandardError}");

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
