using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Category("E2E")]
[Category("ReleaseGate")]
[CancelAfter(300_000)]
public sealed class CheckpointBReleaseGateTests
{
    private const string CandidateVersionEnvironmentVariable = "CHECKPOINT_B_CANDIDATE_VERSION";
    private const string DefaultCandidateVersion = "0.5.1";
    private static readonly string[] _packageIds =
        ["ArchLinterNet.CEL", "ArchLinterNet.Cli", "ArchLinterNet.Core", "ArchLinterNet.Testing"];

    [Test]
    public void PackedCandidate_InstallsFromAnIsolatedFeedAndPassesTheSyntheticAdopterMatrix()
    {
        using CandidatePackageFeed candidate = CandidatePackageFeed.Create();

        candidate.AssertPackageSet();
        candidate.InstallTool();
        candidate.AssertOfflineSchemaRegistry();
        candidate.AssertExternalTestingConsumer();

        foreach (string fixtureId in new[] { "small", "multi-project", "multi-host", "migration" })
        {
            using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create(fixtureId);
            fixture.Build();

            CommandResult sequential = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json",
                "--max-parallelism", "1");
            CommandResult defaultParallelism = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json");
            string cachePath = Path.Combine(fixture.Root, ".checkpoint-b-cache");
            CommandResult cachedFirst = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json",
                "--cache", cachePath);
            CommandResult cachedSecond = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json",
                "--cache", cachePath);
            string[] cacheEntries = Directory.Exists(cachePath)
                ? Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories)
                : [];
            CommandResult cacheAfterCorruption = cachedSecond;
            if (cacheEntries.Length > 0)
            {
                File.WriteAllText(cacheEntries[0], "corrupt-checkpoint-b-entry");
                cacheAfterCorruption = candidate.RunTool(fixture.Root,
                    "--policy", fixture.PolicyPath,
                    "--strict",
                    "--format", "json",
                    "--cache", cachePath);
            }
            string profilePath = Path.Combine(fixture.Root, "checkpoint-b-profile.json");
            CommandResult profiled = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json",
                "--profile", profilePath);
            CommandResult cacheInspection = candidate.RunTool(fixture.Root, "cache", "inspect", "--cache", cachePath);

            Assert.Multiple(() =>
            {
                Assert.That(sequential.ExitCode, Is.EqualTo(defaultParallelism.ExitCode), fixtureId);
                Assert.That(CanonicalJson(sequential.StandardOutput),
                    Is.EqualTo(CanonicalJson(defaultParallelism.StandardOutput)), fixtureId);
                Assert.That(cachedFirst.ExitCode, Is.EqualTo(sequential.ExitCode), fixtureId);
                Assert.That(CanonicalJson(cachedFirst.StandardOutput),
                    Is.EqualTo(CanonicalJson(sequential.StandardOutput)), fixtureId);
                Assert.That(cachedSecond.ExitCode, Is.EqualTo(sequential.ExitCode), fixtureId);
                Assert.That(CanonicalJson(cachedSecond.StandardOutput),
                    Is.EqualTo(CanonicalJson(sequential.StandardOutput)), fixtureId);
                Assert.That(cacheAfterCorruption.ExitCode, Is.EqualTo(sequential.ExitCode), fixtureId);
                Assert.That(CanonicalJson(cacheAfterCorruption.StandardOutput),
                    Is.EqualTo(CanonicalJson(sequential.StandardOutput)), fixtureId);
                Assert.That(profiled.ExitCode, Is.EqualTo(sequential.ExitCode), fixtureId);
                Assert.That(CanonicalJson(profiled.StandardOutput),
                    Is.EqualTo(CanonicalJson(sequential.StandardOutput)), fixtureId);
                Assert.That(File.Exists(profilePath), Is.True, profilePath);
                Assert.That(cacheInspection.ExitCode, Is.EqualTo(0), cacheInspection.CombinedOutput);
                Assert.That(sequential.StandardError, Does.Not.Contain("\u001b["), fixtureId);
            });
        }

        candidate.WriteEvidence(["offline-schema-registry", "external-testing-consumer", "cancellation", "small", "multi-project", "multi-host", "migration", "sequential-default-parity", "cache-disabled-population-hit", "profile-generation"]);
    }

    private static string CanonicalJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement);
    }

    private sealed class CandidatePackageFeed : IDisposable
    {
        private readonly string _root;
        private readonly string _feed;
        private readonly string _toolPath;
        private readonly string _candidateVersion;
        private readonly string _repositoryRoot;

        private CandidatePackageFeed(string root, string candidateVersion, string repositoryRoot)
        {
            _root = root;
            _feed = Path.Combine(root, "feed");
            _toolPath = Path.Combine(root, "tool");
            _candidateVersion = candidateVersion;
            _repositoryRoot = repositoryRoot;
            Directory.CreateDirectory(_feed);
        }

        public static CandidatePackageFeed Create()
        {
            string root = Path.Combine(Path.GetTempPath(), $"arch-linter-checkpoint-b-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
            string candidateVersion = Environment.GetEnvironmentVariable(CandidateVersionEnvironmentVariable)
                ?? DefaultCandidateVersion;
            var candidate = new CandidatePackageFeed(root, candidateVersion, repositoryRoot);
            candidate.Pack();
            return candidate;
        }

        public void AssertPackageSet()
        {
            string[] packagePaths = Directory.GetFiles(_feed, "*.nupkg")
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(packagePaths, Has.Length.EqualTo(4));

            foreach (string packageId in _packageIds)
            {
                string packagePath = PackagePath(packageId);
                using ZipArchive package = ZipFile.OpenRead(packagePath);
                string nuspec = ReadEntry(package, $"{packageId}.nuspec");
                Assert.Multiple(() =>
                {
                    Assert.That(nuspec, Does.Contain($"<id>{packageId}</id>"));
                    Assert.That(nuspec, Does.Contain($"<version>{_candidateVersion}</version>"));
                    Assert.That(Sha256(packagePath), Has.Length.EqualTo(64));
                });
            }

            using ZipArchive core = ZipFile.OpenRead(PackagePath("ArchLinterNet.Core"));
            Assert.That(ReadEntry(core, "ArchLinterNet.Core.nuspec"), Does.Contain("ArchLinterNet.CEL"));
            foreach (string schema in new[]
                     {
                         "analysis-build-state.schema.json", "analysis-cache.schema.json",
                         "analysis-profile.schema.json", "api-snapshot.schema.json", "baseline.schema.json",
                         "compatibility-manifest.json", "dependencies.arch.fragment.schema.json",
                         "dependencies.arch.schema.json", "normalized-finding.schema.json",
                     })
            {
                Assert.That(core.GetEntry($"contentFiles/any/any/schema/0.5.1/{schema}"), Is.Not.Null, schema);
            }
        }

        public void InstallTool()
        {
            CommandResult result = RunDotnet(_root,
                "tool", "install", "ArchLinterNet.Cli",
                "--tool-path", _toolPath,
                "--add-source", _feed,
                "--version", _candidateVersion,
                "--ignore-failed-sources");
            Assert.That(result.ExitCode, Is.EqualTo(0), result.CombinedOutput);
            Assert.That(File.Exists(ToolPath()), Is.True, ToolPath());
        }

        public void AssertOfflineSchemaRegistry()
        {
            string offlineDirectory = Path.Combine(_root, "offline");
            Directory.CreateDirectory(offlineDirectory);

            CommandResult list = RunTool(offlineDirectory, "schema", "list");
            CommandResult print = RunTool(offlineDirectory, "schema", "print", "analysis-cache");

            Assert.Multiple(() =>
            {
                Assert.That(list.ExitCode, Is.EqualTo(0), list.CombinedOutput);
                Assert.That(list.StandardOutput, Does.Contain("analysis-cache\tv1"));
                Assert.That(list.StandardOutput, Does.Contain("analysis-profile\tv1"));
                Assert.That(print.ExitCode, Is.EqualTo(0), print.CombinedOutput);
                Assert.That(print.StandardOutput, Does.Contain("analysis-cache/v1"));
            });
        }

        public void AssertExternalTestingConsumer()
        {
            string consumerDirectory = Path.Combine(_root, "testing-consumer");
            Directory.CreateDirectory(consumerDirectory);
            File.WriteAllText(Path.Combine(consumerDirectory, "NuGet.Config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="candidate" value="{_feed}" />
                  </packageSources>
                </configuration>
                """);
            File.WriteAllText(Path.Combine(consumerDirectory, "CheckpointBConsumer.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="ArchLinterNet.Testing" Version="{_candidateVersion}" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(consumerDirectory, "Program.cs"), """
                using ArchLinterNet.Testing;

                Console.WriteLine(typeof(ArchitectureValidationBuilder).Assembly.FullName);
                string policyPath = Path.Combine(AppContext.BaseDirectory, "checkpoint-b-policy.yml");
                File.WriteAllText(policyPath, "version: 1\\nname: Synthetic Checkpoint B cancellation consumer\\n\\nlayers:\\n  consumer:\\n    namespace: CheckpointB.Consumer\\n\\nanalysis:\\n  target_assemblies: [CheckpointBConsumer]\\n");
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                try
                {
                    _ = new ArchitectureValidationBuilder(policyPath)
                        .WithCancellation(cancellation.Token)
                        .ValidateStrict();
                    return 1;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("cancelled");
                }
                return 0;
                """);

            CommandResult restore = RunDotnet(consumerDirectory, "restore", "--configfile", "NuGet.Config");
            CommandResult result = RunDotnet(consumerDirectory, "run", "--no-restore");
            Assert.Multiple(() =>
            {
                Assert.That(restore.ExitCode, Is.EqualTo(0), restore.CombinedOutput);
                Assert.That(result.ExitCode, Is.EqualTo(0), result.CombinedOutput);
                Assert.That(result.StandardOutput, Does.Contain("ArchLinterNet.Testing"));
                Assert.That(result.StandardOutput, Does.Contain("cancelled"));
                Assert.That(result.StandardOutput, Does.Not.Contain(_repositoryRoot));
            });
        }

        public CommandResult RunTool(string workingDirectory, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo(ToolPath())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };
            startInfo.Environment["DOTNET_CLI_DISABLE_COLOR"] = "1";
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            return Run(startInfo);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        public void WriteEvidence(IReadOnlyList<string> scenarios)
        {
            string? directory = Environment.GetEnvironmentVariable("CHECKPOINT_B_EVIDENCE_DIRECTORY");
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            var evidence = new
            {
                checkpoint = "B",
                result = "passed",
                candidate_version = _candidateVersion,
                source_commit = Environment.GetEnvironmentVariable("ARCH_LINTER_SOURCE_SHA") ?? "unknown",
                platform_id = Environment.GetEnvironmentVariable("CHECKPOINT_B_PLATFORM") ?? "unknown",
                platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                shell = Environment.GetEnvironmentVariable("CHECKPOINT_B_SHELL") ?? "unknown",
                synthetic_identities_only = true,
                packages = _packageIds.Select(packageId => new
                {
                    id = packageId,
                    version = _candidateVersion,
                    sha256 = Sha256(PackagePath(packageId)),
                }),
                scenarios = scenarios.OrderBy(static scenario => scenario, StringComparer.Ordinal),
            };
            string fileName = $"checkpoint-b-{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}.json";
            File.WriteAllText(Path.Combine(directory, fileName), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void Pack()
        {
            CommandResult result = RunDotnet(_repositoryRoot,
                "pack", "ArchLinterNet.slnx",
                "--configuration", "Release",
                "--output", _feed,
                "--no-restore",
                $"-p:Version={_candidateVersion}",
                $"-p:PackageVersion={_candidateVersion}",
                "--nologo");
            Assert.That(result.ExitCode, Is.EqualTo(0), result.CombinedOutput);
        }

        private string PackagePath(string packageId)
        {
            string packagePath = Path.Combine(_feed, $"{packageId}.{_candidateVersion}.nupkg");
            Assert.That(File.Exists(packagePath), Is.True, packagePath);
            return packagePath;
        }

        private string ToolPath()
        {
            string executable = OperatingSystem.IsWindows() ? "arch-linter-net.exe" : "arch-linter-net";
            return Path.Combine(_toolPath, executable);
        }

        private static string ReadEntry(ZipArchive archive, string entryName)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryName)
                ?? throw new AssertionException($"Package archive is missing '{entryName}'.");
            using StreamReader reader = new(entry.Open(), Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static string Sha256(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    }

    private static CommandResult RunDotnet(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Run(startInfo);
    }

    private static CommandResult Run(ProcessStartInfo startInfo)
    {
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{startInfo.FileName}'.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, standardOutput, standardError);
    }

    private sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => $"stdout:{Environment.NewLine}{StandardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{StandardError}";
    }
}
