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

        CheckpointScenarioResult packageProvenance = candidate.AssertPackageProvenance();
        candidate.InstallTool();
        candidate.AssertOfflineSchemaRegistry();
        CheckpointScenarioResult cancellation = candidate.AssertExternalTestingConsumer();
        AssertCleanCheckoutOracle(candidate);

        var scenarios = new List<CheckpointScenarioResult>
        {
            packageProvenance,
            candidate.AssertOfflineSchemaRegistry(),
            cancellation,
            AssertCleanCheckoutOracle(candidate),
            candidate.AssertGenericCiNeutralInvocation(),
            candidate.AssertDocumentedEntrypoint(),
            candidate.AssertNonTtyInvocation(),
        };
        foreach (string fixtureId in new[] { "small", "multi-project", "multi-host", "migration" })
        {
            using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create(fixtureId);
            fixture.Build();

            CommandResult sequential = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json",
                "--ensure-built",
                "--max-parallelism", "1");
            AssertFixtureOracle(fixtureId, sequential);
            CommandResult defaultParallelism = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json",
                "--ensure-built");
            string profilePath = Path.Combine(fixture.Root, "checkpoint-b-profile.json");
            CommandResult profiled = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath,
                "--strict",
                "--format", "json",
                "--ensure-built",
                "--profile", profilePath);

            Assert.Multiple(() =>
            {
                Assert.That(sequential.ExitCode, Is.EqualTo(defaultParallelism.ExitCode), fixtureId);
                Assert.That(CanonicalJson(sequential.StandardOutput),
                    Is.EqualTo(CanonicalJson(defaultParallelism.StandardOutput)), fixtureId);
                Assert.That(profiled.ExitCode, Is.EqualTo(sequential.ExitCode), fixtureId);
                Assert.That(CanonicalJson(profiled.StandardOutput),
                    Is.EqualTo(CanonicalJson(sequential.StandardOutput)), fixtureId);
                Assert.That(File.Exists(profilePath), Is.True, profilePath);
                Assert.That(sequential.StandardError, Does.Not.Contain("\u001b["), fixtureId);
            });
        }

        AssertCacheLifecycleOracle(candidate);
        scenarios.Add(Passed("sequential-default-parity"));
        scenarios.Add(Passed("profile-generation"));
        scenarios.Add(Passed("cache-miss-population-hit"));
        scenarios.Add(Passed("cache-corruption-recompute"));
        scenarios.AddRange(candidate.ShellScenarios());
        scenarios.Add(candidate.AssertCliInFlightCancellation());
        candidate.WriteEvidence(scenarios);
    }

    private static CheckpointScenarioResult Passed(string id) => new(id, "passed", null);

    private static void AssertFixtureOracle(string fixtureId, CommandResult result)
    {
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        JsonElement root = document.RootElement;
        JsonElement findings = root.TryGetProperty("violations", out JsonElement violations)
            ? violations
            : root.GetProperty("findings");
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(0), $"{fixtureId} must complete successfully.{Environment.NewLine}{result.CombinedOutput}");
            Assert.That(findings.ValueKind, Is.EqualTo(JsonValueKind.Array), fixtureId);
            Assert.That(findings.GetArrayLength(), Is.Zero, $"{fixtureId} must have no findings.");
        });
    }

    private static CheckpointScenarioResult AssertCleanCheckoutOracle(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("clean-checkout");
        CommandResult result = candidate.RunTool(fixture.Root,
            "--policy", fixture.PolicyPath,
            "--strict",
            "--format", "json");
        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.EqualTo(1), result.CombinedOutput);
            Assert.That(result.CombinedOutput, Does.Contain("MissingArtifact"));
            Assert.That(Directory.GetDirectories(fixture.Root, "bin", SearchOption.AllDirectories), Is.Empty);
            Assert.That(Directory.GetDirectories(fixture.Root, "obj", SearchOption.AllDirectories), Is.Empty);
        });
        return Passed("clean-checkout");
    }

    private static void AssertCacheLifecycleOracle(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("multi-project");
        fixture.Build();
        string cachePath = Path.Combine(fixture.Root, ".checkpoint-b-cache");
        string firstProfile = Path.Combine(fixture.Root, "cache-first-profile.json");
        string secondProfile = Path.Combine(fixture.Root, "cache-second-profile.json");
        CommandResult first = candidate.RunTool(fixture.Root, "--policy", fixture.PolicyPath, "--strict", "--format", "json", "--ensure-built", "--cache", cachePath, "--profile", firstProfile);
        CommandResult second = candidate.RunTool(fixture.Root, "--policy", fixture.PolicyPath, "--strict", "--format", "json", "--ensure-built", "--cache", cachePath, "--profile", secondProfile);
        string[] entries = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories);
        Assert.Multiple(() =>
        {
            AssertFixtureOracle(fixture.Id, first);
            AssertFixtureOracle(fixture.Id, second);
            Assert.That(ProfileCounter(firstProfile, "Misses"), Is.GreaterThan(0));
            Assert.That(ProfileCounter(firstProfile, "Writes"), Is.GreaterThan(0));
            Assert.That(ProfileCounter(secondProfile, "Hits"), Is.GreaterThan(0));
            Assert.That(entries, Is.Not.Empty, "A cache-eligible fixture must create a cache entry.");
        });
        string entry = entries.Single(path => Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase));
        File.WriteAllText(entry, "corrupt-checkpoint-b-entry");
        string corruptedProfile = Path.Combine(fixture.Root, "cache-corruption-profile.json");
        CommandResult afterCorruption = candidate.RunTool(fixture.Root, "--policy", fixture.PolicyPath, "--strict", "--format", "json", "--ensure-built", "--cache", cachePath, "--profile", corruptedProfile);
        Assert.Multiple(() =>
        {
            AssertFixtureOracle(fixture.Id, afterCorruption);
            Assert.That(ProfileCounter(corruptedProfile, "CorruptionEvents"), Is.GreaterThan(0));
            Assert.That(ProfileCounter(corruptedProfile, "Hits"), Is.Zero);
        });
    }

    private static int ProfileCounter(string path, string counter)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("Counters").GetProperty("Cache").GetProperty(counter).GetInt32();
    }

    private static string CanonicalJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement);
    }

    private static string CanonicalFindingsJson(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement findings = root.TryGetProperty("violations", out JsonElement violations)
            ? violations
            : root.GetProperty("findings");
        return JsonSerializer.Serialize(findings);
    }

    private sealed class CandidatePackageFeed : IDisposable
    {
        private readonly string _root;
        private readonly string _feed;
        private readonly string _toolPath;
        private readonly string _candidateVersion;
        private readonly string _repositoryRoot;
        private readonly string _shell;
        private readonly IReadOnlyList<PackageEvidence> _packages;
        private readonly string _manifestSha256;

        private CandidatePackageFeed(string root, string candidateVersion, string repositoryRoot, string feed,
            string shell, IReadOnlyList<PackageEvidence> packages, string manifestSha256)
        {
            _root = root;
            _feed = feed;
            _toolPath = Path.Combine(root, "tool");
            _candidateVersion = candidateVersion;
            _repositoryRoot = repositoryRoot;
            _shell = shell;
            _packages = packages;
            _manifestSha256 = manifestSha256;
        }

        public static CandidatePackageFeed Create()
        {
            string root = Path.Combine(Path.GetTempPath(), $"arch-linter-checkpoint-b-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            string repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
            string candidateVersion = Environment.GetEnvironmentVariable(CandidateVersionEnvironmentVariable)
                ?? DefaultCandidateVersion;
            string? suppliedFeed = Environment.GetEnvironmentVariable("CHECKPOINT_B_PACKAGE_FEED");
            bool packCandidate = string.IsNullOrWhiteSpace(suppliedFeed);
            string feed = packCandidate ? Path.Combine(root, "feed") : Path.GetFullPath(suppliedFeed!);
            if (packCandidate)
            {
                Directory.CreateDirectory(feed);
                Pack(repositoryRoot, feed, candidateVersion);
            }
            else if (!Directory.Exists(feed))
            {
                throw new InvalidOperationException($"Checkpoint B candidate feed '{feed}' does not exist.");
            }

            (IReadOnlyList<PackageEvidence> packages, string manifestSha256) = LoadManifest(feed, candidateVersion, packCandidate);
            string shell = Environment.GetEnvironmentVariable("CHECKPOINT_B_SHELL") ?? NativeShell();
            var candidate = new CandidatePackageFeed(root, candidateVersion, repositoryRoot, feed, shell, packages, manifestSha256);
            candidate.PopulateIsolatedDependencyCache();
            return candidate;
        }

        public CheckpointScenarioResult AssertPackageProvenance()
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

            Assert.That(_packages.Select(package => package.Id), Is.EqualTo(_packageIds));

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

            return Passed("packed-package-provenance");
        }

        public void InstallTool()
        {
            string configPath = WriteIsolatedNuGetConfig(_root);
            CommandResult result = RunIsolatedDotnet(_root,
                "tool", "install", "ArchLinterNet.Cli",
                "--tool-path", _toolPath,
                "--configfile", configPath,
                "--version", _candidateVersion,
                "--no-cache",
                "--ignore-failed-sources");
            Assert.That(result.ExitCode, Is.EqualTo(0), result.CombinedOutput);
            Assert.That(File.Exists(ToolPath()), Is.True, ToolPath());
        }

        public CheckpointScenarioResult AssertOfflineSchemaRegistry()
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
            return Passed("offline-schema-registry");
        }

        public CheckpointScenarioResult AssertExternalTestingConsumer()
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
                using ArchLinterNet.Core.Caching;
                using ArchLinterNet.Testing;

                Console.WriteLine(typeof(ArchitectureValidationBuilder).Assembly.FullName);
                Console.WriteLine(typeof(ArchitectureValidationBuilder).Assembly.Location);
                string policyPath = Path.Combine(AppContext.BaseDirectory, "checkpoint-b-policy.yml");
                File.WriteAllText(policyPath, "version: 1\\nname: Synthetic Checkpoint B cancellation consumer\\n\\nlayers:\\n  consumer:\\n    namespace: CheckpointB.Consumer\\n\\nanalysis:\\n  target_assemblies: [CheckpointBConsumer]\\n");
                using var cancellation = new CancellationTokenSource();
                using var enteredValidation = new ManualResetEventSlim();
                using var releaseValidation = new ManualResetEventSlim();
                string cachePath = Path.Combine(AppContext.BaseDirectory, "checkpoint-b-cancelled-cache");
                var builder = new ArchitectureValidationBuilder(policyPath)
                    .WithCancellation(cancellation.Token)
                    .WithCache(AnalysisCacheOptions.AtPath(cachePath))
                    .WithValidationEntryBarrier(() =>
                    {
                        enteredValidation.Set();
                        releaseValidation.Wait(TimeSpan.FromSeconds(10));
                    });
                Task task = Task.Run(() =>
                {
                    _ = builder.ValidateStrict();
                });
                if (!enteredValidation.Wait(TimeSpan.FromSeconds(10)))
                {
                    return 2;
                }
                cancellation.Cancel();
                releaseValidation.Set();
                try
                {
                    task.GetAwaiter().GetResult();
                    return 1;
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("cancelled");
                }
                if (Directory.Exists(cachePath) && Directory.EnumerateFiles(cachePath, "*", SearchOption.AllDirectories).Any())
                {
                    return 3;
                }
                Console.WriteLine("cancelled-in-flight-no-cache-or-output");
                return 0;
                """);

            CommandResult restore = RunIsolatedDotnet(consumerDirectory, "restore", "--configfile", "NuGet.Config", "--no-cache");
            CommandResult result = RunIsolatedDotnet(consumerDirectory, "run", "--no-restore");
            string assemblyLocation = result.StandardOutput.Split(Environment.NewLine)
                .Single(line => line.EndsWith("ArchLinterNet.Testing.dll", StringComparison.OrdinalIgnoreCase));
            using ZipArchive testingPackage = ZipFile.OpenRead(PackagePath("ArchLinterNet.Testing"));
            ZipArchiveEntry packageAssembly = testingPackage.Entries.Single(entry =>
                entry.FullName.EndsWith("/ArchLinterNet.Testing.dll", StringComparison.OrdinalIgnoreCase));
            using Stream packageAssemblyStream = packageAssembly.Open();
            string packageAssemblySha256 = Convert.ToHexStringLower(SHA256.HashData(packageAssemblyStream));
            string assets = File.ReadAllText(Path.Combine(consumerDirectory, "obj", "project.assets.json"));
            Assert.Multiple(() =>
            {
                Assert.That(restore.ExitCode, Is.EqualTo(0), restore.CombinedOutput);
                Assert.That(result.ExitCode, Is.EqualTo(0), result.CombinedOutput);
                Assert.That(result.StandardOutput, Does.Contain("ArchLinterNet.Testing"));
                Assert.That(result.StandardOutput, Does.Contain("cancelled"));
                Assert.That(result.StandardOutput, Does.Not.Contain(_repositoryRoot));
                Assert.That(assets, Does.Contain($"ArchLinterNet.Testing/{_candidateVersion}"));
                Assert.That(assets, Does.Contain(Path.Combine(_root, "nuget-packages")));
                Assert.That(Sha256(assemblyLocation), Is.EqualTo(packageAssemblySha256));
                Assert.That(result.StandardOutput, Does.Not.Contain("successful"));
                Assert.That(result.StandardOutput, Does.Contain("cancelled-in-flight-no-cache-or-output"));
            });
            return Passed("external-testing-consumer");
        }

        public CheckpointScenarioResult AssertGenericCiNeutralInvocation()
        {
            using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("small");
            fixture.Build();
            CommandResult result = RunTool(fixture.Root, "--policy", fixture.PolicyPath, "--mode", "strict", "--format", "json", "--ensure-built");
            AssertFixtureOracle(fixture.Id, result);
            return Passed("generic-ci-neutral");
        }

        public CheckpointScenarioResult AssertDocumentedEntrypoint()
        {
            string documentation = File.ReadAllText(Path.Combine(_repositoryRoot, "docs", "guides", "reference-entrypoints.md"));
            Assert.That(documentation, Does.Contain("arch-linter-net --policy"));
            using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("small");
            fixture.Build();
            CommandResult result = RunTool(fixture.Root, "--policy", fixture.PolicyPath, "--mode", "strict", "--format", "json", "--ensure-built");
            AssertFixtureOracle(fixture.Id, result);
            return Passed("documented-entrypoints");
        }

        public CheckpointScenarioResult AssertNonTtyInvocation()
        {
            using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("small");
            fixture.Build();
            CommandResult result = RunTool(fixture.Root, "--policy", fixture.PolicyPath, "--strict", "--format", "json", "--ensure-built");
            Assert.Multiple(() =>
            {
                AssertFixtureOracle(fixture.Id, result);
                Assert.That(result.StandardError, Does.Not.Contain("\u001b["));
            });
            return Passed("non-tty");
        }

        public CheckpointScenarioResult AssertCliInFlightCancellation()
        {
            if (!OperatingSystem.IsLinux())
            {
                return new CheckpointScenarioResult("in-flight-cancellation", "not_applicable",
                    "The process-observation CLI interruption oracle runs on Linux.");
            }

            using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("small");
            long cancellationArtifactBytes = fixture.AddLargeEmbeddedResource(
                "checkpoint-b-cancellation.bin", 96 * 1024 * 1024);
            fixture.Build();
            string output = Path.Combine(fixture.Root, "cancelled-report.json");
            string cache = Path.Combine(fixture.Root, "cancelled-cache");
            ProcessStartInfo startInfo = CreateDirectToolStartInfo(fixture.Root,
                ["--policy", fixture.PolicyPath, "--strict", "--ensure-built", "--cache", cache, "--report", $"json={output}"]);
            startInfo.Environment["DOTNET_CLI_DISABLE_COLOR"] = "1";
            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the CLI.");
            try
            {
                Assert.That(SpinWait.SpinUntil(
                        () => !process.HasExited && CheckpointBReleaseGateProcessTree.HasReadAtLeast(
                            process.Id, cancellationArtifactBytes / 2),
                        TimeSpan.FromSeconds(30)),
                    Is.True, "CLI did not read the deliberately oversized target assembly before cancellation.");
                SendTermination(process.Id);
                bool exited = process.WaitForExit(15_000);
                if (!exited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                string[] cacheFiles = Directory.Exists(cache)
                    ? Directory.EnumerateFiles(cache, "*", SearchOption.AllDirectories).ToArray()
                    : [];
                Assert.Multiple(() =>
                {
                    Assert.That(exited, Is.True, "Interrupted CLI did not exit.");
                    Assert.That(process.ExitCode, Is.EqualTo(2), $"stdout:{standardOutput}{Environment.NewLine}stderr:{standardError}");
                    Assert.That(standardOutput + standardError, Does.Contain("cancelled"));
                    Assert.That(File.Exists(output), Is.False, "Cancellation must not publish a final report.");
                    Assert.That(cacheFiles, Is.Empty);
                    Assert.That(Directory.GetFiles(fixture.Root, "*.tmp", SearchOption.AllDirectories), Is.Empty);
                });
                return Passed("in-flight-cancellation");
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
        }

        public IReadOnlyList<CheckpointScenarioResult> ShellScenarios()
        {
            return _shell switch
            {
                "bash" or "zsh" =>
                [Passed("posix-entrypoint"), new CheckpointScenarioResult("powershell-entrypoint", "not_applicable", "PowerShell is exercised by the Windows platform job.")],
                "pwsh" =>
                [Passed("powershell-entrypoint"), new CheckpointScenarioResult("posix-entrypoint", "not_applicable", "POSIX shells are exercised by Linux and macOS platform jobs.")],
                _ => throw new AssertionException($"Unsupported Checkpoint B shell adapter '{_shell}'."),
            };
        }

        public CommandResult RunTool(string workingDirectory, params string[] arguments)
        {
            ProcessStartInfo startInfo = CreateShellStartInfo(workingDirectory, arguments);
            startInfo.Environment["DOTNET_CLI_DISABLE_COLOR"] = "1";
            return Run(startInfo);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        public void WriteEvidence(IReadOnlyList<CheckpointScenarioResult> scenarios)
        {
            string? directory = Environment.GetEnvironmentVariable("CHECKPOINT_B_EVIDENCE_DIRECTORY");
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
            var evidence = new
            {
                schema = "checkpoint-b-platform-evidence/v1",
                checkpoint = "B",
                result = "passed",
                candidate_version = _candidateVersion,
                source_commit = Environment.GetEnvironmentVariable("ARCH_LINTER_SOURCE_SHA") ?? "unknown",
                platform_id = Environment.GetEnvironmentVariable("CHECKPOINT_B_PLATFORM") ?? "unknown",
                platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                shell = _shell,
                synthetic_identities_only = true,
                candidate_manifest_sha256 = _manifestSha256,
                packages = _packages,
                scenarios = scenarios.OrderBy(static scenario => scenario.Id, StringComparer.Ordinal),
            };
            string fileName = "checkpoint-b-platform-evidence.json";
            File.WriteAllText(Path.Combine(directory, fileName), JsonSerializer.Serialize(evidence, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            }));
        }

        private CommandResult RunIsolatedDotnet(string workingDirectory, params string[] arguments)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };
            startInfo.Environment["NUGET_PACKAGES"] = Path.Combine(_root, "nuget-packages");
            startInfo.Environment["NUGET_HTTP_CACHE_PATH"] = Path.Combine(_root, "nuget-http-cache");
            startInfo.Environment["NUGET_PLUGINS_CACHE_PATH"] = Path.Combine(_root, "nuget-plugins-cache");
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            return Run(startInfo);
        }

        private void PopulateIsolatedDependencyCache()
        {
            string bootstrapDirectory = Path.Combine(_root, "dependency-bootstrap");
            Directory.CreateDirectory(bootstrapDirectory);
            File.WriteAllText(Path.Combine(bootstrapDirectory, "CheckpointBDependencies.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Buildalyzer" Version="9.0.0" />
                    <PackageReference Include="JsonSchema.Net" Version="7.3.4" />
                    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0" />
                    <PackageReference Include="Microsoft.CodeAnalysis.VisualBasic" Version="4.14.0" />
                    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
                    <PackageReference Include="System.CommandLine" Version="2.0.9" />
                    <PackageReference Include="System.Security.Cryptography.Xml" Version="9.0.18" />
                    <PackageReference Include="YamlDotNet" Version="16.3.0" />
                  </ItemGroup>
                </Project>
                """);
            CommandResult result = RunIsolatedDotnet(bootstrapDirectory, "restore");
            Assert.That(result.ExitCode, Is.EqualTo(0), result.CombinedOutput);
        }

        private string WriteIsolatedNuGetConfig(string directory)
        {
            string path = Path.Combine(directory, "NuGet.Config");
            File.WriteAllText(path, $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="checkpoint-b-candidate" value="{_feed}" />
                  </packageSources>
                </configuration>
                """);
            return path;
        }

        private ProcessStartInfo CreateShellStartInfo(string workingDirectory, IReadOnlyList<string> arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };
            if (_shell is "bash" or "zsh")
            {
                startInfo.FileName = _shell;
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add("exec \"$@\"");
                startInfo.ArgumentList.Add("checkpoint-b-shell-adapter");
            }
            else if (_shell == "pwsh")
            {
                startInfo.FileName = "pwsh";
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-NonInteractive");
                startInfo.ArgumentList.Add("-Command");
                startInfo.ArgumentList.Add("& $args[0] @($args[1..($args.Length - 1)]); exit $LASTEXITCODE");
            }
            else
            {
                throw new AssertionException($"Unsupported Checkpoint B shell adapter '{_shell}'.");
            }

            startInfo.ArgumentList.Add(ToolPath());
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            return startInfo;
        }

        private ProcessStartInfo CreateDirectToolStartInfo(string workingDirectory, IReadOnlyList<string> arguments)
        {
            var startInfo = new ProcessStartInfo(ToolPath())
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

            return startInfo;
        }

        private static void Pack(string repositoryRoot, string feed, string candidateVersion)
        {
            CommandResult result = RunDotnet(repositoryRoot,
                "pack", "ArchLinterNet.slnx",
                "--configuration", "Release",
                "--output", feed,
                "--no-restore",
                $"-p:Version={candidateVersion}",
                $"-p:PackageVersion={candidateVersion}",
                "--nologo");
            Assert.That(result.ExitCode, Is.EqualTo(0), result.CombinedOutput);
        }

        private static (IReadOnlyList<PackageEvidence> Packages, string ManifestSha256) LoadManifest(
            string feed, string candidateVersion, bool createdLocally)
        {
            string? manifestPath = Environment.GetEnvironmentVariable("CHECKPOINT_B_PACKAGE_MANIFEST");
            if (!createdLocally && string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new InvalidOperationException("Checkpoint B requires CHECKPOINT_B_PACKAGE_MANIFEST for supplied packages.");
            }

            if (!string.IsNullOrWhiteSpace(manifestPath))
            {
                string path = Path.GetFullPath(manifestPath);
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement root = document.RootElement;
                Assert.That(root.GetProperty("schema").GetString(), Is.EqualTo("checkpoint-b-candidate-manifest/v1"));
                Assert.That(root.GetProperty("version").GetString(), Is.EqualTo(candidateVersion));
                PackageEvidence[] packages = root.GetProperty("packages").EnumerateArray()
                    .Select(package => new PackageEvidence(
                        package.GetProperty("id").GetString()!,
                        package.GetProperty("version").GetString()!,
                        package.GetProperty("file").GetString()!,
                        package.GetProperty("size").GetInt64(),
                        package.GetProperty("sha256").GetString()!))
                    .ToArray();
                foreach (PackageEvidence package in packages)
                {
                    string packagePath = Path.Combine(feed, package.File);
                    Assert.Multiple(() =>
                    {
                        Assert.That(new FileInfo(packagePath).Length, Is.EqualTo(package.Size), package.File);
                        Assert.That(Sha256(packagePath), Is.EqualTo(package.Sha256), package.File);
                    });
                }

                return (packages, Sha256(path));
            }

            PackageEvidence[] localPackages = _packageIds.Select(packageId =>
            {
                string path = Path.Combine(feed, $"{packageId}.{candidateVersion}.nupkg");
                return new PackageEvidence(packageId, candidateVersion, Path.GetFileName(path), new FileInfo(path).Length, Sha256(path));
            }).ToArray();
            string localManifest = JsonSerializer.Serialize(localPackages);
            return (localPackages, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(localManifest))));
        }

        private static string NativeShell() => OperatingSystem.IsWindows() ? "pwsh" : "bash";

        private static void SendTermination(int processId)
        {
            CommandResult result = Run(new ProcessStartInfo("kill")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                ArgumentList = { "-TERM", processId.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            });
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

    private sealed record PackageEvidence(string Id, string Version, string File, long Size, string Sha256);

    private sealed record CheckpointScenarioResult(string Id, string Result, string? Reason);
}
