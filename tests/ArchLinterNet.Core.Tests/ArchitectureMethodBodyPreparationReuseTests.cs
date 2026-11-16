using System.Diagnostics;
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
public sealed partial class ArchitectureMethodBodyPreparationReuseTests
{
    private static readonly string[] _targetFrameworks = ["net10.0"];

    private string _fixtureRoot = null!;

    private ProjectDiscoveryResult _projectDiscovery = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixtureRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-method-body-preparation-{Guid.NewGuid():N}");
        string consumerDirectory = Path.Combine(_fixtureRoot, "src", "Consumer");
        string secretDirectory = Path.Combine(consumerDirectory, "Secret");
        string otherDirectory = Path.Combine(_fixtureRoot, "src", "Other");
        Directory.CreateDirectory(consumerDirectory);
        Directory.CreateDirectory(secretDirectory);
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
        File.WriteAllText(Path.Combine(secretDirectory, "SecretCaller.cs"), """
            namespace ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer.Secret;

            public sealed class SecretCaller
            {
                public void Write() => System.Diagnostics.Debug.Fail("secret");
            }
            """);

        Action consumerIlTarget = MethodBodyPreparationTargets.Consumer.MethodBodyPreparationIlTarget.Write;
        Action otherIlTarget = MethodBodyPreparationTargets.Other.MethodBodyPreparationIlTarget.Write;
        Assert.That(consumerIlTarget.Method.DeclaringType, Is.Not.Null);
        Assert.That(otherIlTarget.Method.DeclaringType, Is.Not.Null);

        RunDotnet(_fixtureRoot, "build", consumerProjectPath);
        RunDotnet(_fixtureRoot, "build", otherProjectPath);

        _projectDiscovery = new ProjectDiscoveryResult(
            Array.Empty<string>(),
            Array.Empty<string>(),
            ["src/Consumer", "src/Other"],
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects =
            [
                new ArchitectureDiscoveredProject("src/Consumer/Consumer.csproj", "Fixture.Consumer", _targetFrameworks),
                new ArchitectureDiscoveredProject("src/Other/Other.csproj", "Fixture.Other", _targetFrameworks),
            ],
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
                    shouldFindViolation ? "System.Console.WriteLine" : "System.Diagnostics.Debug.Assert"));

                Assert.That(
                    violations.Any(violation => violation.ContractId == id
                        && violation.ForbiddenReferences.Any(reference => reference.Contains(
                            shouldFindViolation ? "Console.WriteLine" : "Debug.Assert", StringComparison.Ordinal))),
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
            Assert.That(observer.MethodBodyOwnershipComparisons, Is.EqualTo(4));
            Assert.That(observer.MethodBodyBuildContextResolutions, Is.EqualTo(1));
            Assert.That(observer.MethodBodySyntaxTreeParseCalls, Is.EqualTo(2));
            Assert.That(observer.MethodBodyCompilationCalls, Is.EqualTo(1));
            Assert.That(observer.MethodBodySyntaxTrees, Is.EqualTo(2));

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
            Assert.That(observer.MethodBodyOwnershipComparisons, Is.EqualTo(6));
            Assert.That(observer.MethodBodyBuildContextResolutions, Is.EqualTo(2));
            Assert.That(observer.MethodBodySyntaxTreeParseCalls, Is.EqualTo(3));
            Assert.That(observer.MethodBodyCompilationCalls, Is.EqualTo(2));
            Assert.That(observer.MethodBodySyntaxTrees, Is.EqualTo(3));
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
        ArchitectureMethodBodyPreparationCache cache = new();
        BenchmarkWorkObserver observer = new();
        ArchitectureLayer layer = new() { Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer" };
        var preparations = new ArchitectureMethodBodyPreparation[12];
        try
        {
            ConcurrentPreparationWork[] workItems = Enumerable.Range(0, preparations.Length)
                .Select(index => new ConcurrentPreparationWork(
                    index, cache, _fixtureRoot, layer, preparations, observer))
                .ToArray();
            Parallel.ForEach(workItems, static workItem => workItem.Prepare());

            Assert.That(preparations.Select(preparation => preparation.Compilation).Distinct().Count(), Is.EqualTo(1));
            Assert.That(observer.MethodBodySourceDirectoriesEnumerated, Is.EqualTo(2));
            Assert.That(observer.MethodBodySyntaxTreeParseCalls, Is.EqualTo(2));
            Assert.That(observer.MethodBodyCompilationCalls, Is.EqualTo(1));
            cache.Dispose();

            ObjectDisposedException? disposedException = null;
            try
            {
                cache.Prepare(
                    _fixtureRoot,
                    layer,
                    ["src/Consumer", "src/Other"],
                    preprocessorSymbols: null,
                    projectDiscovery: null,
                    buildConfiguration: "Debug",
                    cancellationToken: CancellationToken.None,
                    benchmarkWorkObserver: observer);
            }
            catch (ObjectDisposedException exception)
            {
                disposedException = exception;
            }

            Assert.That(disposedException, Is.Not.Null);
        }
        finally
        {
            cache.Dispose();
        }
    }

    [Test]
    public void PreparationCache_CancelledInventoryIsEvictedAndCanBeRetried()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        CancelingFileSystem fileSystem = new(cancellationTokenSource);
        using ArchitectureMethodBodyPreparationCache cache = new(fileSystem);
        BenchmarkWorkObserver observer = new();
        ArchitectureLayer layer = new() { Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer" };
        var repositoryRoot = Path.Combine(Path.GetTempPath(), "arch-linter-method-body-cache-retry");
        OperationCanceledException? cancellationException = null;
        try
        {
            cache.Prepare(
                repositoryRoot,
                layer,
                ["src"],
                preprocessorSymbols: null,
                projectDiscovery: null,
                buildConfiguration: "Debug",
                cancellationToken: cancellationTokenSource.Token,
                benchmarkWorkObserver: observer);
        }
        catch (OperationCanceledException exception)
        {
            cancellationException = exception;
        }

        Assert.That(cancellationException, Is.Not.Null);

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
        Assert.That(conditionalSymbols.Compilation!.SyntaxTrees.All(syntaxTree =>
            ((CSharpParseOptions)syntaxTree.Options).PreprocessorSymbolNames.Contains("UNITY_EDITOR")), Is.True);
        Assert.That(observer.MethodBodySourceDirectoriesEnumerated, Is.EqualTo(2));
        (int enumeratedFiles, int fileReads) = ExpectedInventoryWork(
            "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer");
        Assert.That(observer.MethodBodySourceFilesEnumerated, Is.EqualTo(enumeratedFiles));
        Assert.That(observer.MethodBodySourceFileReads, Is.EqualTo(fileReads));
        Assert.That(observer.MethodBodySyntaxTreeParseCalls, Is.EqualTo(4));
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
            new() { Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer" },
            ["src/Consumer", "src/Other"],
            preprocessorSymbols: null,
            projectDiscovery: _projectDiscovery,
            buildConfiguration: "Debug",
            cancellationToken: CancellationToken.None,
            benchmarkWorkObserver: null));
    }

    private ArchitectureAnalysisSession CreateSession(
        BenchmarkWorkObserver observer,
        Dictionary<string, ArchitectureLayer>? layers = null)
    {
        ArchitectureContractDocument document = new()
        {
            Layers = layers ?? new Dictionary<string, ArchitectureLayer>
            {
                ["consumer"] = new() { Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Consumer" },
                ["other"] = new() { Namespace = "ArchLinterNet.Core.Tests.MethodBodyPreparationTargets.Other" },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                SourceRoots = ["src/Consumer", "src/Other"],
            },
        };

        ArchitectureAnalysisContext context = new(
            _fixtureRoot,
            [typeof(ArchitectureMethodBodyPreparationReuseTests).Assembly],
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: _projectDiscovery)
        {
            BenchmarkWorkObserver = observer,
        };

        return new ArchitectureAnalysisSession(context, document, null, false, null);
    }

    private static ArchitectureMethodBodyContract CreateContract(string id, string source, params string[] forbiddenCalls)
    {
        return new ArchitectureMethodBodyContract
        {
            Name = id,
            Id = id,
            Source = source,
            ForbiddenCalls = [.. forbiddenCalls],
        };
    }

    private (int EnumeratedFiles, int FileReads) ExpectedInventoryWork(string namespaceName)
    {
        var enumeratedFiles = 0;
        var fileReads = 0;
        foreach (string root in new[] { "src/Consumer", "src/Other" })
        {
            string fullRoot = Path.Combine(_fixtureRoot, root);
            foreach (string filePath in Directory.EnumerateFiles(fullRoot, "*.cs", SearchOption.AllDirectories))
            {
                enumeratedFiles++;
                string relativeToRoot = Path.GetRelativePath(fullRoot, filePath);
                if (ArchitectureGeneratedFileFilter.IsExcluded(relativeToRoot)) continue;

                fileReads++;
                string source = File.ReadAllText(filePath);
                if (source.Contains($"namespace {namespaceName};", StringComparison.Ordinal)
                    || source.Contains($"namespace {namespaceName}.", StringComparison.Ordinal))
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

}
