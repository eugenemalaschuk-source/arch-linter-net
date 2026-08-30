using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Discovery.Abstractions;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

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
    public void BuildRunner_PublicApiMode_UsesDiscoveredOutputForExplicitTargetAssembly()
    {
        CreateProjectWithOutput("ArchLinterNet.Core", "net9.0");

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { "ArchLinterNet.Core" },
                Projects = new List<string> { Path.Combine(_repoRoot, "ArchLinterNet.Core", "ArchLinterNet.Core.csproj") }
            }
        };

        ArchitectureRunnerSetup setup = _runnerSetupService.BuildRunner(document, _policyPath, mode: "public-api");
        string expectedArtifactPath = Path.Combine(_repoRoot, "ArchLinterNet.Core", "bin", "Debug", "net9.0", "ArchLinterNet.Core.dll");

        Assert.Multiple(() =>
        {
            Assert.That(setup.Runner.Session.Context.ProjectDiscovery!.ResolvedAssemblyPaths,
                Does.ContainKey("ArchLinterNet.Core").WithValue(expectedArtifactPath));
            Assert.That(setup.Runner.Session.Context.AssemblyProbingPaths,
                Does.Contain(Path.GetDirectoryName(expectedArtifactPath)!));
        });
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
    public void BuildRunner_ExplicitTargetAssemblies_ProjectMetricUsesTheExactDiscoveredProjectOutput()
    {
        Assembly assembly = typeof(ArchitectureMetricMeasurement).Assembly;
        const string ProjectPath = "src/MyApp/MyApp.csproj";
        string artifactPath = Path.Combine(_repoRoot, "src", "MyApp", "bin", "Debug", "net10.0", "MyApp.dll");
        ProjectDiscoveryResult discovery = new(
            [assembly.GetName().Name!], Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = [new ArchitectureDiscoveredProject(ProjectPath, assembly.GetName().Name!, ["net10.0"])],
            ResolvedAssemblyPathsByNormalizedProjectPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ProjectPath] = artifactPath,
            },
        };
        var projectDiscovery = new RecordingProjectDiscoveryService(discovery);
        var resolution = new StaticAssemblyResolutionService(assembly, artifactPath);
        var service = new ArchitectureRunnerSetupService(
            new ArchitecturePolicyDocumentLoader(),
            new ArchitectureBaselineLoadingService(),
            new ArchitectureRepositoryRootResolver(),
            new ConditionSetResolutionService(),
            projectDiscovery,
            resolution);
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { assembly.GetName().Name! },
                Projects = new List<string> { ProjectPath },
            },
            Topology = new ArchitectureTopology
            {
                Mode = "partial",
                SubjectKind = "type",
                Scope = new ArchitectureTopologyScope
                {
                    Selectors = [new ArchitectureTopologySubjectSelector { Namespace = "ArchLinterNet.Core.Model" }],
                },
                Nodes =
                [
                    new ArchitectureTopologyNode
                    {
                        Id = "application",
                        Mappings = [new ArchitectureTopologySubjectSelector { Namespace = "ArchLinterNet.Core.Model" }],
                    },
                ],
            },
            Metrics =
            [
                new ArchitectureMetricDefinition
                {
                    Id = "application-project-footprint",
                    Kind = ArchitectureMetricKinds.ComponentFootprintCount,
                    TopologyNode = "application",
                    Unit = "project",
                },
            ],
        };

        ArchitectureRunnerSetup setup = service.BuildRunner(document, _policyPath);
        ArchitectureMetricMeasurement measurement = ArchitectureMetricEvaluator
            .Evaluate(setup.Runner.Session, document.Metrics)
            .Measurements
            .Single();

        Assert.Multiple(() =>
        {
            Assert.That(projectDiscovery.ResolveAssemblyOutputs, Is.True);
            Assert.That(measurement.IsEvaluable, Is.True);
            Assert.That(measurement.Value, Is.EqualTo(1));
            Assert.That(measurement.Contributors, Is.EqualTo(new[] { ProjectPath }));
        });
    }

    [Test]
    public void BuildRunner_NoTargetAssembliesAndDiscoveredProjectHasNoOutput_ReportsItMissingInsteadOfThrowing()
    {
        // A real project was discovered (analysis.projects) — it just has no build output. Build-
        // state preflight (see #362) now needs this reported as missing so it can emit a typed
        // diagnostic, instead of BuildRunner throwing an untyped configuration error here. The
        // generic "define analysis.target_assemblies" error is now reserved for when discovery
        // found no projects at all — see BuildRunner_NoTargetAssembliesAndNoProjectsDiscovered_Throws.
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

        ArchitectureRunnerSetup setup = _runnerSetupService.BuildRunner(document, _policyPath);

        Assert.That(setup.Runner.Session.Context.MissingAssemblyNames, Does.Contain("_noOutput"));
    }

    [Test]
    public void BuildRunner_NoTargetAssembliesAndNoProjectsDiscovered_Throws()
    {
        // No target_assemblies and no discovered projects at all — genuinely nothing identifies
        // what should be validated, so this remains a thrown configuration error.
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = new List<string> { Path.Combine(_repoRoot, "does-not-exist.csproj") }
            }
        };

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(
            () => _runnerSetupService.BuildRunner(document, _policyPath));

        Assert.That(exception!.Message, Does.Contain("analysis.target_assemblies"));
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

    private sealed class RecordingProjectDiscoveryService(ProjectDiscoveryResult result) : IArchitectureProjectDiscoveryService
    {
        public bool ResolveAssemblyOutputs { get; private set; }

        public ProjectDiscoveryResult ResolveAndApply(
            ArchitectureContractDocument document,
            string repositoryRoot,
            bool resolveAssemblyOutputs,
            CancellationToken cancellationToken = default)
        {
            ResolveAssemblyOutputs = resolveAssemblyOutputs;
            return result;
        }
    }

    private sealed class StaticAssemblyResolutionService(Assembly assembly, string artifactPath)
        : IArchitectureAssemblyResolutionService
    {
        public ResolutionResult Resolve(
            ArchitectureContractDocument document,
            string repositoryRoot,
            ProjectDiscoveryResult discovery,
            bool resolveAssemblyOutputs,
            string? mode,
            HashSet<string>? selectedContractIds,
            CancellationToken cancellationToken = default) => CreateResult();

        public ResolutionResult ResolvePostBuild(
            ArchitectureContractDocument document,
            string repositoryRoot,
            ProjectDiscoveryResult discovery,
            bool resolveAssemblyOutputs,
            string? mode,
            HashSet<string>? selectedContractIds,
            IReadOnlyDictionary<string, string>? expectedArtifactContentDigests = null,
            CancellationToken cancellationToken = default) => CreateResult();

        private ResolutionResult CreateResult() => new(
            [assembly], Array.Empty<string>(), Array.Empty<string>())
        {
            ResolvedAssemblyArtifactPaths = new Dictionary<Assembly, string> { [assembly] = artifactPath },
        };
    }
}
