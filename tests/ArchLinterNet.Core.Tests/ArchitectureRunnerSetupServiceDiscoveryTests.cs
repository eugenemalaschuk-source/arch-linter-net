using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureRunnerSetupServiceDiscoveryTests
{
    private static readonly string[] _archLinterNetCore = { "ArchLinterNet.Core" };
    private static readonly string[] _noOutput = { "_noOutput" };

    private string _repoRoot = null!;
    private string _policyPath = null!;
    private ArchitectureRunnerSetupService _runnerSetupService = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-factory-discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoRoot);
        _policyPath = Path.Combine(_repoRoot, "policy.arch.yml");
        File.WriteAllText(_policyPath, "version: 1\nname: test\n");
        _runnerSetupService = new ArchitectureRunnerSetupService(
            new ArchitecturePolicyDocumentLoader(),
            new ArchitectureBaselineLoadingService(),
            new ArchitectureRepositoryRootResolver(),
            new ConditionSetResolutionService(),
            new ArchitectureProjectDiscoveryService(),
            new ArchitectureAssemblyResolutionService());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, true);
        }
    }

    [Test]
    public void BuildRunner_ExplicitTargetAssemblies_TakesPrecedenceOverDiscovery()
    {
        CreateProjectWithOutput("Unresolvable", "net9.0");

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" },
                Projects = new List<string> { Path.Combine(_repoRoot, "Unresolvable", "Unresolvable.csproj") }
            }
        };

        ArchitectureRunnerSetup setup = _runnerSetupService.BuildRunner(document, _policyPath);

        Assert.That(document.Analysis.TargetAssemblies, Is.EquivalentTo(_archLinterNetCore));
        Assert.That(setup.Runner.CheckConfiguration().Any(v => v.ForbiddenNamespace == "missing project build output"), Is.False);

        string discoveredOutputDir = Path.Combine(_repoRoot, "Unresolvable", "bin", "Debug", "net9.0");
        Assert.That(document.Analysis.AssemblySearchPaths, Has.None.Matches<string>(
            path => string.Equals(path, discoveredOutputDir, StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public void BuildRunner_EmptyTargetAssemblies_SeedsFromDiscoveredProject()
    {
        CreateProjectWithOutput("ArchLinterNet.Core", "net9.0");

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(_repoRoot, "ArchLinterNet.Core", "ArchLinterNet.Core.csproj") }
            }
        };

        ArchitectureRunnerSetup setup = _runnerSetupService.BuildRunner(document, _policyPath);

        Assert.That(document.Analysis.TargetAssemblies, Is.EquivalentTo(_archLinterNetCore));
        Assert.That(document.Analysis.SourceRoots, Is.EquivalentTo(_archLinterNetCore));
        Assert.That(setup.Runner.CheckConfiguration().Any(v => v.SourceType.Contains("ArchLinterNet.Core.csproj")), Is.False);
    }

    [Test]
    public void BuildRunner_ExplicitTargetAssemblies_ProjectWithNoBuildOutput_DoesNotProduceDiagnosticButStillSeedsSourceRoot()
    {
        string projectDir = Path.Combine(_repoRoot, "_noOutput");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "_noOutput.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" },
                Projects = new List<string> { Path.Combine(projectDir, "_noOutput.csproj") }
            }
        };

        ArchitectureRunnerSetup setup = _runnerSetupService.BuildRunner(document, _policyPath);

        Assert.That(document.Analysis.TargetAssemblies, Is.EquivalentTo(_archLinterNetCore));
        Assert.That(setup.Runner.CheckConfiguration().Any(v => v.ForbiddenNamespace == "missing project build output"), Is.False);
        Assert.That(document.Analysis.SourceRoots, Is.EquivalentTo(_noOutput));
    }

    [Test]
    public void BuildRunner_NoTargetAssembliesAndDiscoveryYieldsNothing_ThrowsWithDiagnosticDetails()
    {
        string projectDir = Path.Combine(_repoRoot, "_noOutput");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "_noOutput.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "_noOutput.csproj") }
            }
        };

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(
            () => _runnerSetupService.BuildRunner(document, _policyPath));

        Assert.That(exception!.Message, Does.Contain("analysis.target_assemblies"));
        Assert.That(exception.Message, Does.Contain("_noOutput"));
    }

    [Test]
    public void BuildRunner_ProjectMetadataOnlyPolicy_ProjectWithNoBuildOutput_DoesNotRequireResolvedAssemblies()
    {
        string projectDir = Path.Combine(_repoRoot, "_noOutput");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, "_noOutput.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(projectDir, "_noOutput.csproj") }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictProjectMetadata = new List<ArchitectureProjectMetadataContract>
                {
                    new()
                    {
                        Name = "project-metadata",
                        Id = "project-metadata",
                        Projects = new List<string> { "_noOutput/_noOutput.csproj" },
                        RequiredProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Nullable"] = "enable"
                        }
                    }
                }
            }
        };

        ArchitectureRunnerSetup setup = _runnerSetupService.BuildRunner(document, _policyPath, mode: "strict");
        List<ArchitectureViolation> configurationViolations = setup.Runner.CheckConfiguration();

        Assert.That(document.Analysis.TargetAssemblies, Is.Empty);
        Assert.That(configurationViolations.Any(v => v.ForbiddenNamespace == "missing project build output"), Is.False);
        Assert.That(configurationViolations.Any(v => v.ForbiddenNamespace == "no project metadata discovered"), Is.False);
        Assert.That(document.Analysis.SourceRoots, Is.EquivalentTo(_noOutput));
    }

    private void CreateProjectWithOutput(string assemblyName, string targetFramework)
    {
        string projectDir = Path.Combine(_repoRoot, assemblyName);
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, $"{assemblyName}.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        string outputDir = Path.Combine(projectDir, "bin", "Debug", targetFramework);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, $"{assemblyName}.dll"), string.Empty);
    }
}
