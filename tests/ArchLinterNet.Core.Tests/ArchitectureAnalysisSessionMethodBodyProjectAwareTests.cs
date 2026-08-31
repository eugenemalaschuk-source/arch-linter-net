using System.Diagnostics;
using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureAnalysisSessionMethodBodyProjectAwareTests
{
    private static readonly string[] _value = { "net10.0" };
    private static readonly string[] _value1 = { "FIXTURE_SYMBOL" };
    private static readonly string[] _value2 = { "net10.0" };
    private static readonly string[] _value3 = { "net10.0" };
    private static readonly string[] _value4 = { "net10.0" };
    private string _fixtureRoot = null!;
    private string _consumerRelativePath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _fixtureRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-session-project-aware-{Guid.NewGuid():N}");

        string referencedDir = Path.Combine(_fixtureRoot, "Fixture.Referenced");
        Directory.CreateDirectory(referencedDir);
        File.WriteAllText(Path.Combine(referencedDir, "Fixture.Referenced.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(referencedDir, "Widgets.cs"), """
            namespace Fixture.Referenced;

            public static class Widgets
            {
                public static void Build() { }
            }
            """);

        string consumerDir = Path.Combine(_fixtureRoot, "Fixture.Consumer");
        Directory.CreateDirectory(consumerDir);
        string consumerProjectPath = Path.Combine(consumerDir, "Fixture.Consumer.csproj");
        _consumerRelativePath = Path.Combine("Fixture.Consumer", "Fixture.Consumer.csproj");
        File.WriteAllText(consumerProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Fixture.Referenced/Fixture.Referenced.csproj" />
                <PackageReference Include="YamlDotNet" Version="18.1.0" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(consumerDir, "Caller.cs"), """
            namespace Fixture.Consumer;

            public class Caller
            {
                public void CallReferencedProject()
                {
                    Fixture.Referenced.Widgets.Build();
                }

                public void CallPackageReference()
                {
                    new YamlDotNet.Serialization.DeserializerBuilder().Build();
                }

                public void CallFrameworkApi()
                {
                    System.Console.WriteLine("always resolvable via fallback");
                }

                public void CallUnderConditionSet()
                {
            #if FIXTURE_SYMBOL
                    System.Diagnostics.Debug.Fail("only reachable when FIXTURE_SYMBOL is defined");
            #endif
                }
            }
            """);

        RunDotnet(_fixtureRoot, "build", consumerProjectPath);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (Directory.Exists(_fixtureRoot))
        {
            Directory.Delete(_fixtureRoot, true);
        }
    }

    private static void RunDotnet(string workingDirectory, params string[] arguments)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"'dotnet' failed with exit code {process.ExitCode}.\n{stdout}\n{stderr}");
        }
    }

    private ArchitectureAnalysisSession CreateSession(
        ArchitectureMethodBodyContract contract,
        ProjectDiscoveryResult? projectDiscovery,
        IReadOnlyList<string>? preprocessorSymbols = null)
    {
        var document = new ArchitectureContractDocument
        {
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["consumer"] = new ArchitectureLayer { Namespace = "Fixture.Consumer" },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                SourceRoots = new List<string> { "Fixture.Consumer" },
            },
        };

        var context = new ArchitectureAnalysisContext(
            _fixtureRoot,
            Array.Empty<Assembly>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: projectDiscovery);

        return new ArchitectureAnalysisSession(context, document, null, false, preprocessorSymbols);
    }

    private ProjectDiscoveryResult DiscoveryPointingAtConsumer()
    {
        return new ProjectDiscoveryResult(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject(
                    _consumerRelativePath.Replace('\\', '/'), "Fixture.Consumer", _value),
            },
        };
    }

    [Test]
    public void CheckMethodBodyContract_ProjectAwareResolutionAvailable_ResolvesCrossProjectReference()
    {
        var contract = new ArchitectureMethodBodyContract
        {
            Name = "no-referenced-widgets",
            Id = "no-referenced-widgets",
            Source = "consumer",
            ForbiddenCalls = new List<string> { "Widgets.Build" },
        };

        ArchitectureAnalysisSession session = CreateSession(contract, DiscoveryPointingAtConsumer());

        List<ArchitectureViolation> violations = session.CheckMethodBodyContract(contract);

        Assert.That(violations.Any(v => v.ForbiddenReferences.Any(r => r.Contains("Widgets.Build"))), Is.True,
            "Expected the cross-project call to be resolved and reported via project-aware Roslyn analysis.");
    }

    [Test]
    public void CheckMethodBodyContract_ProjectAwareResolutionAvailable_ResolvesPackageReference()
    {
        var contract = new ArchitectureMethodBodyContract
        {
            Name = "no-yaml-deserializer",
            Id = "no-yaml-deserializer",
            Source = "consumer",
            ForbiddenCalls = new List<string> { "DeserializerBuilder.Build" },
        };

        ArchitectureAnalysisSession session = CreateSession(contract, DiscoveryPointingAtConsumer());

        List<ArchitectureViolation> violations = session.CheckMethodBodyContract(contract);

        Assert.That(violations.Any(v => v.ForbiddenReferences.Any(r => r.Contains("DeserializerBuilder.Build"))), Is.True,
            "Expected the package-provided call to be resolved and reported via project-aware Roslyn analysis.");
    }

    [Test]
    [CancelAfter(30_000)]
    public void CheckMethodBodyContract_ProjectAwareResolutionAvailable_ConditionSetSymbolDefined_IncludesConditionalBlock()
    {
        var contract = new ArchitectureMethodBodyContract
        {
            Name = "no-debug-fail",
            Id = "no-debug-fail",
            Source = "consumer",
            ForbiddenCalls = new List<string> { "Debug.Fail" },
        };

        ArchitectureAnalysisSession session = CreateSession(
            contract, DiscoveryPointingAtConsumer(), preprocessorSymbols: _value1);

        List<ArchitectureViolation> violations = session.CheckMethodBodyContract(contract);

        Assert.That(violations.Any(v => v.ForbiddenReferences.Any(r => r.Contains("Debug.Fail"))), Is.True,
            "With FIXTURE_SYMBOL defined, the #if-guarded call must still be visible in the project-aware compilation.");
    }

    [Test]
    [CancelAfter(30_000)]
    public void CheckMethodBodyContract_ProjectAwareResolutionAvailable_ConditionSetSymbolNotDefined_ExcludesConditionalBlock()
    {
        var contract = new ArchitectureMethodBodyContract
        {
            Name = "no-debug-fail-undefined",
            Id = "no-debug-fail-undefined",
            Source = "consumer",
            ForbiddenCalls = new List<string> { "Debug.Fail" },
        };

        ArchitectureAnalysisSession session = CreateSession(
            contract, DiscoveryPointingAtConsumer(), preprocessorSymbols: Array.Empty<string>());

        List<ArchitectureViolation> violations = session.CheckMethodBodyContract(contract);

        Assert.That(violations.Any(v => v.ForbiddenReferences.Any(r => r.Contains("Debug.Fail"))), Is.False,
            "Without FIXTURE_SYMBOL defined, the #if-guarded call must stay excluded even in the project-aware compilation.");
    }

    [Test]
    public void CheckMethodBodyContract_NoProjectDiscoveryConfigured_FallsBackWithoutDiagnostic()
    {
        var contract = new ArchitectureMethodBodyContract
        {
            Name = "no-console-writeline",
            Id = "no-console-writeline",
            Source = "consumer",
            ForbiddenCalls = new List<string> { "Console.WriteLine" },
        };

        ArchitectureAnalysisSession session = CreateSession(contract, projectDiscovery: null);

        List<ArchitectureViolation> violations = session.CheckMethodBodyContract(contract);

        Assert.That(violations.Any(v => v.ForbiddenReferences.Any(r => r.Contains("Console.WriteLine"))), Is.True,
            "Fallback (no discovery configured) must still detect calls resolvable via AppDomain-loaded assemblies.");
        Assert.That(violations.Any(v => v.ForbiddenNamespace == "project-aware analysis fallback"), Is.False,
            "No fallback diagnostic should appear when project discovery was never configured.");
    }

    // Writes a real project and drives MSBuild-backed project-aware analysis over it, so its wall
    // time tracks the runner rather than this repository's code — the same reason every other
    // build-bound test here carries an exemption. It was missing one and only tripped the 15 s
    // default on the slowest CI runner (Intel macOS: ~17 s, against ~10 s elsewhere), which is why
    // it stayed green until a slow runner exposed it.
    [Test]
    [CancelAfter(120_000)]
    public void CheckMethodBodyContract_DiscoveryConfiguredButProjectNotRestored_EmitsFallbackDiagnosticAndStillDetectsViolations()
    {
        string notRestoredDir = Path.Combine(_fixtureRoot, "Fixture.NotRestored");
        Directory.CreateDirectory(notRestoredDir);
        File.WriteAllText(Path.Combine(notRestoredDir, "Fixture.NotRestored.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(notRestoredDir, "Caller.cs"), """
            namespace Fixture.NotRestored;

            public class Caller
            {
                public void Run()
                {
                    System.Console.WriteLine("still detected via fallback");
                }
            }
            """);

        var document = new ArchitectureContractDocument
        {
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["not_restored"] = new ArchitectureLayer { Namespace = "Fixture.NotRestored" },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                SourceRoots = new List<string> { "Fixture.NotRestored" },
            },
        };

        var projectDiscovery = new ProjectDiscoveryResult(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[]
            {
                new ArchitectureDiscoveredProject(
                    "Fixture.NotRestored/Fixture.NotRestored.csproj", "Fixture.NotRestored", _value2),
            },
        };

        var context = new ArchitectureAnalysisContext(
            _fixtureRoot, Array.Empty<Assembly>(), Array.Empty<string>(), Array.Empty<string>(),
            projectDiscovery: projectDiscovery);

        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        var contract = new ArchitectureMethodBodyContract
        {
            Name = "no-console-writeline-not-restored",
            Id = "no-console-writeline-not-restored",
            Source = "not_restored",
            ForbiddenCalls = new List<string> { "Console.WriteLine" },
        };

        List<ArchitectureViolation> violations = session.CheckMethodBodyContract(contract);

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "project-aware analysis fallback"), Is.True,
            "Expected an explicit fallback diagnostic since discovery was configured but the project was never restored.");
        Assert.That(violations.Any(v => v.ForbiddenReferences.Any(r => r.Contains("Console.WriteLine"))), Is.True,
            "The lightweight fallback compilation should still detect the violation.");
    }

    // PR #375 review: ResolveOwningProject materialized discovered-project directories (and ran
    // the matchedFiles × discoveredProjects scan) with no cancellation checks, so cancellation
    // during the prepass was only observed at the next surrounding boundary, after the whole
    // prepass had run. This proves the materialization loop's per-project check is live: the
    // collection cancels on its 6th cumulative item fetch, which is the second project's fetch
    // inside ResolveOwningProject — the first two enumerations (2 fetches each) belong to
    // ArchitectureAnalysisContext's construction-time project-path inventory and
    // ArchitectureSourceFileFactIndex's construction-time source-path ownership pass, both of
    // which ran with an uncancelled token — so the OperationCanceledException comes from the
    // materialization loop's check, and the loop stopped at the nearest project boundary.
    [Test]
    public void CheckMethodBodyContract_CancelledWhileResolvingOwningProject_StopsAtProjectBoundary()
    {
        var contract = new ArchitectureMethodBodyContract
        {
            Name = "no-referenced-widgets",
            Id = "no-referenced-widgets",
            Source = "consumer",
            ForbiddenCalls = new List<string> { "Widgets.Build" },
        };

        using CancellationTokenSource cts = new();
        var projects = new CancelOnFetchCountCollection<ArchitectureDiscoveredProject>(
        [
            new ArchitectureDiscoveredProject(
                _consumerRelativePath.Replace('\\', '/'), "Fixture.Consumer", _value3),
            new ArchitectureDiscoveredProject(
                "Fixture.Referenced/Fixture.Referenced.csproj", "Fixture.Referenced", _value4),
        ], cts, cancelBeforeTotalFetch: 6);

        var document = new ArchitectureContractDocument
        {
            Layers = new Dictionary<string, ArchitectureLayer>
            {
                ["consumer"] = new ArchitectureLayer { Namespace = "Fixture.Consumer" },
            },
            Analysis = new ArchitectureAnalysisConfiguration
            {
                SourceRoots = new List<string> { "Fixture.Consumer" },
            },
        };

        var context = new ArchitectureAnalysisContext(
            _fixtureRoot,
            Array.Empty<Assembly>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: new ProjectDiscoveryResult(
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
            {
                DiscoveredProjects = projects,
            })
        {
            CancellationToken = cts.Token,
        };

        var session = new ArchitectureAnalysisSession(context, document, null, false, null);

        Assert.Throws<OperationCanceledException>(() => session.CheckMethodBodyContract(contract));

        Assert.That(projects.FetchedCount, Is.EqualTo(6),
            "2 fetches (context construction) + 2 fetches (fact-index construction) + the materialization loop's own first project = 5, so the 6th fetch is the second project inside ResolveOwningProject — the loop must stop there and never fetch it again");
    }

    private sealed class CancelOnFetchCountCollection<T> : IReadOnlyCollection<T>
    {
        private readonly IReadOnlyList<T> _items;
        private readonly CancellationTokenSource _cts;
        private readonly int _cancelBeforeTotalFetch;

        public CancelOnFetchCountCollection(IReadOnlyList<T> items, CancellationTokenSource cts, int cancelBeforeTotalFetch)
        {
            _items = items;
            _cts = cts;
            _cancelBeforeTotalFetch = cancelBeforeTotalFetch;
        }

        public int FetchedCount { get; private set; }

        public int Count => _items.Count;

        public IEnumerator<T> GetEnumerator()
        {
            foreach (T item in _items)
            {
                FetchedCount++;
                if (FetchedCount == _cancelBeforeTotalFetch)
                {
                    _cts.Cancel();
                }

                yield return item;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
