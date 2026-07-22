using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class FrameworkReferenceConfigurationTests
{
    private const string SourceAssemblyName = "MyApp.Domain";

    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-framework-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_repoRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repoRoot))
        {
            Directory.Delete(_repoRoot, true);
        }
    }

    private string CreateProject(string assemblyName, string itemGroupBody, string targetFramework = "net10.0")
    {
        string projectDir = Path.Combine(_repoRoot, assemblyName);
        Directory.CreateDirectory(projectDir);
        string projectPath = Path.Combine(projectDir, $"{assemblyName}.csproj");

        File.WriteAllText(projectPath, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                {itemGroupBody}
              </ItemGroup>
            </Project>
            """);

        return projectPath;
    }

    private ArchitectureAnalysisContext CreateContext(params string[] projectAbsolutePaths)
    {
        if (projectAbsolutePaths.Length == 0)
        {
            return new ArchitectureAnalysisContext(
                _repoRoot,
                new[] { typeof(FrameworkReferenceConfigurationTests).Assembly },
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var document = new ArchitectureContractDocument
        {
            Analysis = new ArchitectureAnalysisConfiguration
            {
                Projects = projectAbsolutePaths.ToList()
            }
        };

        ProjectDiscoveryResult discovery = new ArchitectureProjectDiscoveryService()
            .ResolveFromDocument(document, _repoRoot, resolveAssemblyOutputs: false);

        return new ArchitectureAnalysisContext(
            _repoRoot,
            new[] { typeof(FrameworkReferenceConfigurationTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
    }

    [Test]
    public void CheckConfiguration_UnknownFrameworkGroup_ReturnsViolation()
    {
        string projectPath = CreateProject(SourceAssemblyName, string.Empty);
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract>
                {
                    new() { Name = "domain-no-unknown", Source = SourceAssemblyName, Forbidden = new List<string> { "unknown_group" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ForbiddenNamespace == "unknown framework group" &&
            (v.Payload as FrameworkReferencePayload)?.ForbiddenFrameworkGroup == "unknown_group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_FrameworkGroupWithoutMatchers_ReturnsViolation()
    {
        string projectPath = CreateProject(SourceAssemblyName, string.Empty);
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            FrameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup> { ["empty_group"] = new() },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract>
                {
                    new() { Name = "domain-no-empty-group", Source = SourceAssemblyName, Forbidden = new List<string> { "empty_group" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ForbiddenNamespace == "invalid framework group" &&
            (v.Payload as FrameworkReferencePayload)?.ForbiddenFrameworkGroup == "empty_group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_KnownUsableFrameworkGroup_ReturnsNoConfigurationViolation()
    {
        string projectPath = CreateProject(SourceAssemblyName, string.Empty);
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            FrameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
            {
                ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract>
                {
                    new() { Name = "domain-no-aspnet", Source = SourceAssemblyName, Forbidden = new List<string> { "forbidden_web" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckConfiguration_NoProjectDiscoveryConfigured_ReturnsMissingProjectMetadataViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            FrameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
            {
                ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract>
                {
                    new() { Id = "domain-no-aspnet", Name = "domain-no-aspnet", Source = SourceAssemblyName, Forbidden = new List<string> { "forbidden_web" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ContractId == "domain-no-aspnet" && v.ForbiddenNamespace == "no project metadata discovered"), Is.True);
    }

    [Test]
    public void CheckConfiguration_SourceNotAmongDiscoveredProjects_ReturnsMissingProjectMetadataViolation()
    {
        string projectPath = CreateProject("SomeOtherAssembly", string.Empty);
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            FrameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
            {
                ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract>
                {
                    new() { Id = "domain-no-aspnet", Name = "domain-no-aspnet", Source = SourceAssemblyName, Forbidden = new List<string> { "forbidden_web" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ContractId == "domain-no-aspnet" && v.ForbiddenNamespace == "no project metadata discovered"), Is.True);
    }

    [Test]
    public void CheckConfiguration_FrameworkAllowOnly_UnknownFrameworkGroup_ReturnsViolation()
    {
        string projectPath = CreateProject(SourceAssemblyName, string.Empty);
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkAllowOnly = new List<ArchitectureFrameworkReferenceAllowOnlyContract>
                {
                    new() { Name = "domain-allow-only-known", Source = SourceAssemblyName, Allowed = new List<string> { "unknown_group" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ForbiddenNamespace == "unknown framework group" &&
            (v.Payload as FrameworkReferencePayload)?.ForbiddenFrameworkGroup == "unknown_group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_SourceAmongDiscoveredProjects_ReturnsNoMissingMetadataViolation()
    {
        string projectPath = CreateProject(SourceAssemblyName, string.Empty);
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            FrameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
            {
                ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract>
                {
                    new() { Id = "domain-no-aspnet", Name = "domain-no-aspnet", Source = SourceAssemblyName, Forbidden = new List<string> { "forbidden_web" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "no project metadata discovered"), Is.False);
    }

    [Test]
    public void CheckConfiguration_ProjectTargetsUninstalledFramework_ReturnsEvaluationFailedViolation()
    {
        // Fail-closed regression: an MSBuild-evaluation-impossible project (bogus/uninstalled TFM)
        // must surface as a configuration violation, not a silent pass.
        string projectPath = CreateProject(SourceAssemblyName, string.Empty, targetFramework: "net1.0");
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            FrameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
            {
                ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract>
                {
                    new() { Id = "domain-no-aspnet", Name = "domain-no-aspnet", Source = SourceAssemblyName, Forbidden = new List<string> { "forbidden_web" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ContractId == "domain-no-aspnet" && v.ForbiddenNamespace == "framework reference evaluation failed"), Is.True);
    }
}
