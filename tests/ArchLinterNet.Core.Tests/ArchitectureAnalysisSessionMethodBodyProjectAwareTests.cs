using System.Diagnostics;
using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureAnalysisSessionMethodBodyProjectAwareTests
{
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
                <PackageReference Include="YamlDotNet" Version="16.3.0" />
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
                    _consumerRelativePath.Replace('\\', '/'), "Fixture.Consumer", new[] { "net10.0" }),
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
            contract, DiscoveryPointingAtConsumer(), preprocessorSymbols: new[] { "FIXTURE_SYMBOL" });

        List<ArchitectureViolation> violations = session.CheckMethodBodyContract(contract);

        Assert.That(violations.Any(v => v.ForbiddenReferences.Any(r => r.Contains("Debug.Fail"))), Is.True,
            "With FIXTURE_SYMBOL defined, the #if-guarded call must still be visible in the project-aware compilation.");
    }

    [Test]
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

    [Test]
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
                    "Fixture.NotRestored/Fixture.NotRestored.csproj", "Fixture.NotRestored", new[] { "net10.0" }),
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
}
