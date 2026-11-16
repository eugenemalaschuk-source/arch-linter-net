using System.Diagnostics;
using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.IO.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Profiling;
using ArchLinterNet.Core.Scanning;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
[Category("E2E")]
public sealed class ArchitectureMethodBodyPreparationReuseTests
{
    private static readonly string[] _targetFrameworks = ["net10.0"];

    private string _fixtureRoot = null!;

    private ProjectDiscoveryResult _projectDiscovery = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixtureRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-method-body-preparation-{Guid.NewGuid():N}");
        string consumerDirectory = Path.Combine(_fixtureRoot, "src", "Consumer");
        string otherDirectory = Path.Combine(_fixtureRoot, "src", "Other");
        Directory.CreateDirectory(consumerDirectory);
        Directory.CreateDirectory(otherDirectory);

        string consumerProjectPath = Path.Combine(consumerDirectory, "Consumer.csproj");
        string otherProjectPath = Path.Combine(otherDirectory, "Other.csproj");
        File.WriteAllText(consumerProjectPath, ProjectFile("Fixture.Consumer"));
        File.WriteAllText(otherProjectPath, ProjectFile("Fixture.Other"));
        File.WriteAllText(Path.Combine(consumerDirectory, "Caller.cs"), """
            namespace ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer;

            public sealed class Caller
            {
                public void Write() => System.Console.WriteLine("consumer");
            }
            """);
        File.WriteAllText(Path.Combine(otherDirectory, "Caller.cs"), """
            namespace ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Other;

            public sealed class Caller
            {
                public void Write() => System.Console.WriteLine("other");
            }
            """);

        RunDotnet(_fixtureRoot, "build", consumerProjectPath);
        RunDotnet(_fixtureRoot, "build", otherProjectPath);

        _projectDiscovery = new ProjectDiscoveryResult(
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "src/Consumer", "src/Other" },
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject("src/Consumer/Consumer.csproj", "Fixture.Consumer", _targetFrameworks),
                new ArchitectureDiscoveredProject("src/Other/Other.csproj", "Fixture.Other", _targetFrameworks),
            },
        };
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (Directory.Exists(_fixtureRoot))
        {
            Directory.Delete(_fixtureRoot, recursive: true);
        }
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(20)]
    public void CheckMethodBodyContract_SameInputRules_PreparesRoslynAndProjectContextOnce(int ruleCount)
    {
        BenchmarkWorkObserver observer = new();
        ArchitectureAnalysisSession session = CreateSession(observer);
        try
        {
            for (int index = 0; index < ruleCount; index++)
            {
                string id = $"consumer-{index}";
                bool shouldFindViolation = index % 2 == 0;
                List<ArchitectureViolation> violations = session.CheckMethodBodyContract(CreateContract(
                    id,
                    "consumer",
                    shouldFindViolation ? "System.Console.WriteLine" : "System.Diagnostics.Debug.Fail"));

                Assert.That(
                    violations.Any(violation => violation.ContractId == id
                        && violation.ForbiddenReferences.Any(reference => reference.Contains(
                            shouldFindViolation ? "Console.WriteLine" : "Debug.Fail", StringComparison.Ordinal))),
                    Is.EqualTo(shouldFindViolation),
                    $"Rule {id} must retain its own pattern evaluation and findings.");
            }

            Assert.That(observer.MethodBodySourceDirectoriesEnumerated, Is.EqualTo(2));
            (int enumeratedFiles, int fileReads) = ExpectedInventoryWork(
                "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer");
            Assert.That(observer.MethodBodySourceFilesEnumerated, Is.EqualTo(enumeratedFiles));
            Assert.That(observer.MethodBodySourceFileReads, Is.EqualTo(fileReads));
            Assert.That(observer.MethodBodyOwnershipLookups, Is.EqualTo(1));
            Assert.That(observer.MethodBodyOwnershipProjectDirectories, Is.EqualTo(2));
            Assert.That(observer.MethodBodyOwnershipComparisons, Is.EqualTo(2));
            Assert.That(observer.MethodBodyBuildContextResolutions, Is.EqualTo(1));
            Assert.That(observer.MethodBodySyntaxTreeParseCalls, Is.EqualTo(1));
            Assert.That(observer.MethodBodyCompilationCalls, Is.EqualTo(1));
            Assert.That(observer.MethodBodySyntaxTrees, Is.EqualTo(1));

            // The reflection/IL path remains per rule because its pattern matching and ignore
            // side effects are rule-specific. Its fixed input work therefore scales with Q.
            Assert.That(observer.MethodBodyIlTypeVisits, Is.EqualTo(ruleCount));
            Assert.That(observer.MethodBodyIlMethodVisits, Is.EqualTo(ruleCount));
        }
        finally
        {
            session.Context.Dispose();
        }
    }

    [Test]
    public void CheckMethodBodyContract_DifferentSourceAndProjectContexts_PreparesSeparately()
    {
        BenchmarkWorkObserver observer = new();
        ArchitectureAnalysisSession session = CreateSession(observer);
        try
        {
            List<ArchitectureViolation> consumerViolations = session.CheckMethodBodyContract(
                CreateContract("consumer", "consumer", "System.Console.WriteLine"));
            List<ArchitectureViolation> otherViolations = session.CheckMethodBodyContract(
                CreateContract("other", "other", "System.Console.WriteLine"));

            Assert.That(consumerViolations.Any(violation => violation.ContractId == "consumer"), Is.True);
            Assert.That(otherViolations.Any(violation => violation.ContractId == "other"), Is.True);
            Assert.That(observer.MethodBodySourceDirectoriesEnumerated, Is.EqualTo(4));
            (int consumerFiles, int consumerReads) = ExpectedInventoryWork(
                "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer");
            (int otherFiles, int otherReads) = ExpectedInventoryWork(
                "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Other");
            Assert.That(observer.MethodBodySourceFilesEnumerated, Is.EqualTo(consumerFiles + otherFiles));
            Assert.That(observer.MethodBodySourceFileReads, Is.EqualTo(consumerReads + otherReads));
            Assert.That(observer.MethodBodyOwnershipLookups, Is.EqualTo(2));
            Assert.That(observer.MethodBodyOwnershipProjectDirectories, Is.EqualTo(4));
            Assert.That(observer.MethodBodyOwnershipComparisons, Is.EqualTo(4));
            Assert.That(observer.MethodBodyBuildContextResolutions, Is.EqualTo(2));
            Assert.That(observer.MethodBodySyntaxTreeParseCalls, Is.EqualTo(2));
            Assert.That(observer.MethodBodyCompilationCalls, Is.EqualTo(2));
            Assert.That(observer.MethodBodySyntaxTrees, Is.EqualTo(2));
            Assert.That(observer.MethodBodyIlTypeVisits, Is.EqualTo(2));
            Assert.That(observer.MethodBodyIlMethodVisits, Is.EqualTo(2));
        }
        finally
        {
            session.Context.Dispose();
        }
    }

    [Test]
    public void PreparationCache_ConcurrentSameContextAccess_IsSingleFlightAndDisposalClearsIt()
    {
        using ArchitectureMethodBodyPreparationCache cache = new();
        BenchmarkWorkObserver observer = new();
        ArchitectureLayer layer = new() { Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer" };
        ArchitectureMethodBodyPreparation[] preparations = new ArchitectureMethodBodyPreparation[12];

        Parallel.For(0, preparations.Length, index =>
        {
            preparations[index] = cache.Prepare(
                _fixtureRoot,
                layer,
                ["src/Consumer", "src/Other"],
                preprocessorSymbols: null,
                projectDiscovery: null,
                buildConfiguration: "Debug",
                cancellationToken: CancellationToken.None,
                benchmarkWorkObserver: observer);
        });

        Assert.That(preparations.Select(preparation => preparation.Compilation).Distinct().Count(), Is.EqualTo(1));
        Assert.That(observer.MethodBodySourceDirectoriesEnumerated, Is.EqualTo(2));
        Assert.That(observer.MethodBodySyntaxTreeParseCalls, Is.EqualTo(1));
        Assert.That(observer.MethodBodyCompilationCalls, Is.EqualTo(1));
        cache.Dispose();

        Assert.Throws<ObjectDisposedException>(() => cache.Prepare(
            _fixtureRoot,
            layer,
            ["src/Consumer", "src/Other"],
            preprocessorSymbols: null,
            projectDiscovery: null,
            buildConfiguration: "Debug",
            cancellationToken: CancellationToken.None,
            benchmarkWorkObserver: observer));
    }

    [Test]
    public void PreparationCache_CancelledInventoryIsEvictedAndCanBeRetried()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        CancelingFileSystem fileSystem = new(cancellationTokenSource);
        using ArchitectureMethodBodyPreparationCache cache = new(fileSystem);
        BenchmarkWorkObserver observer = new();
        ArchitectureLayer layer = new() { Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer" };
        string repositoryRoot = Path.Combine(Path.GetTempPath(), "arch-linter-method-body-cache-retry");

        Assert.Throws<OperationCanceledException>(() => cache.Prepare(
            repositoryRoot,
            layer,
            ["src"],
            preprocessorSymbols: null,
            projectDiscovery: null,
            buildConfiguration: "Debug",
            cancellationToken: cancellationTokenSource.Token,
            benchmarkWorkObserver: observer));

        ArchitectureMethodBodyPreparation retry = cache.Prepare(
            repositoryRoot,
            layer,
            ["src"],
            preprocessorSymbols: null,
            projectDiscovery: null,
            buildConfiguration: "Debug",
            cancellationToken: CancellationToken.None,
            benchmarkWorkObserver: observer);

        Assert.That(retry.SourceFilePaths, Has.Count.EqualTo(2));
        Assert.That(observer.MethodBodyCompilationCalls, Is.EqualTo(1));
        Assert.That(observer.MethodBodySyntaxTreeParseCalls, Is.EqualTo(2));
    }

    [Test]
    public void PreparationCache_DifferentPreprocessorSymbols_PreparesSeparateSyntaxTreesAndCompilations()
    {
        using ArchitectureMethodBodyPreparationCache cache = new();
        BenchmarkWorkObserver observer = new();
        ArchitectureLayer layer = new() { Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer" };

        ArchitectureMethodBodyPreparation defaultSymbols = cache.Prepare(
            _fixtureRoot,
            layer,
            ["src/Consumer", "src/Other"],
            preprocessorSymbols: null,
            projectDiscovery: null,
            buildConfiguration: "Debug",
            cancellationToken: CancellationToken.None,
            benchmarkWorkObserver: observer);
        ArchitectureMethodBodyPreparation conditionalSymbols = cache.Prepare(
            _fixtureRoot,
            layer,
            ["src/Consumer", "src/Other"],
            preprocessorSymbols: ["UNITY_EDITOR"],
            projectDiscovery: null,
            buildConfiguration: "Debug",
            cancellationToken: CancellationToken.None,
            benchmarkWorkObserver: observer);

        Assert.That(conditionalSymbols.Compilation, Is.Not.SameAs(defaultSymbols.Compilation));
        CSharpParseOptions parseOptions = (CSharpParseOptions)conditionalSymbols.Compilation!.SyntaxTrees.Single().Options;
        Assert.That(parseOptions.PreprocessorSymbolNames, Does.Contain("UNITY_EDITOR"));
        Assert.That(observer.MethodBodySourceDirectoriesEnumerated, Is.EqualTo(2));
        (int enumeratedFiles, int fileReads) = ExpectedInventoryWork(
            "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer");
        Assert.That(observer.MethodBodySourceFilesEnumerated, Is.EqualTo(enumeratedFiles));
        Assert.That(observer.MethodBodySourceFileReads, Is.EqualTo(fileReads));
        Assert.That(observer.MethodBodySyntaxTreeParseCalls, Is.EqualTo(2));
        Assert.That(observer.MethodBodyCompilationCalls, Is.EqualTo(2));
    }

    [Test]
    public void AnalysisContextDispose_ReleasesSessionPreparationCache()
    {
        ArchitectureAnalysisSession session = CreateSession(new BenchmarkWorkObserver());
        _ = session.CheckMethodBodyContract(CreateContract("dispose", "consumer", "System.Console.WriteLine"));
        session.Context.Dispose();

        Assert.Throws<ObjectDisposedException>(() => session.MethodBodyPreparationCache.Prepare(
            _fixtureRoot,
            new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer" },
            ["src/Consumer", "src/Other"],
            preprocessorSymbols: null,
            projectDiscovery: _projectDiscovery,
            buildConfiguration: "Debug",
            cancellationToken: CancellationToken.None,
            benchmarkWorkObserver: null));
    }

    private ArchitectureAnalysisSession CreateSession(BenchmarkWorkObserver observer)
    {
        ArchitectureContractDocument document = new()
        {
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["consumer"] = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer" },
                ["other"] = new ArchitectureLayer { Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Other" },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                SourceRoots = new List<string> { "src/Consumer", "src/Other" },
            },
        };

        ArchitectureAnalysisContext context = new(
            _fixtureRoot,
            new[] { typeof(ArchitectureMethodBodyPreparationReuseTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: _projectDiscovery)
        {
            BenchmarkWorkObserver = observer,
        };

        return new ArchitectureAnalysisSession(context, document, null, false, null);
    }

    private static ArchitectureMethodBodyContract CreateContract(string id, string source, string forbiddenCall)
    {
        return new ArchitectureMethodBodyContract
        {
            Name = id,
            Id = id,
            Source = source,
            ForbiddenCalls = new List<string> { forbiddenCall },
        };
    }

    private (int EnumeratedFiles, int FileReads) ExpectedInventoryWork(string namespaceName)
    {
        int enumeratedFiles = 0;
        int fileReads = 0;
        foreach (string root in new[] { "src/Consumer", "src/Other" })
        {
            string fullRoot = Path.Combine(_fixtureRoot, root);
            foreach (string filePath in Directory.EnumerateFiles(fullRoot, "*.cs", SearchOption.AllDirectories))
            {
                enumeratedFiles++;
                string relativeToRoot = Path.GetRelativePath(fullRoot, filePath);
                if (ArchitectureGeneratedFileFilter.IsExcluded(relativeToRoot)) continue;

                fileReads++;
                if (File.ReadAllText(filePath).Contains($"namespace {namespaceName};", StringComparison.Ordinal))
                {
                    fileReads++;
                }
            }
        }

        return (enumeratedFiles, fileReads);
    }

    private static string ProjectFile(string assemblyName)
    {
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>{assemblyName}</AssemblyName>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """;
    }

    private static void RunDotnet(string workingDirectory, params string[] arguments)
    {
        ProcessStartInfo processStartInfo = new("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'dotnet' failed with exit code {process.ExitCode}.\n{stdout}\n{stderr}");
        }
    }

    private sealed class CancelingFileSystem : IArchitectureFileSystem
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        public CancelingFileSystem(CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
        }

        public bool FileExists(string path) => false;

        public string ReadAllText(string path) => "namespace ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer; internal sealed class Source { }";

        public IEnumerable<string> ReadLines(string path) => ["namespace ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer;"];

        public bool DirectoryExists(string path) => true;

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            yield return Path.Combine(path, "First.cs");
            _cancellationTokenSource.Cancel();
            yield return Path.Combine(path, "Second.cs");
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) =>
            Array.Empty<string>();

        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UnixEpoch;

        public string GetCurrentDirectory() => Path.GetTempPath();
    }
}
