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

    private static ArchitectureAnalysisContext CreateContext(ProjectDiscoveryResult? projectDiscovery = null)
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(FrameworkReferenceConfigurationTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: projectDiscovery);
    }

    private static ProjectDiscoveryResult DiscoveryWithProject(
        string assemblyName, params (string Name, string? Condition)[] frameworks)
    {
        var project = new ArchitectureDiscoveredProject(
            $"src/{assemblyName}/{assemblyName}.csproj",
            assemblyName,
            new[] { "net10.0" },
            Array.Empty<ArchitectureDiscoveredPackageReference>(),
            frameworks.Select(f => new ArchitectureDiscoveredFrameworkReference(f.Name, f.Condition)).ToList());

        return new ProjectDiscoveryResult(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[] { project }
        };
    }

    [Test]
    public void CheckConfiguration_UnknownFrameworkGroup_ReturnsViolation()
    {
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

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject(SourceAssemblyName, ("Microsoft.NETCore.App", null))), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ForbiddenNamespace == "unknown framework group" &&
            (v.Payload as FrameworkReferencePayload)?.ForbiddenFrameworkGroup == "unknown_group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_FrameworkGroupWithoutMatchers_ReturnsViolation()
    {
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

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject(SourceAssemblyName)), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ForbiddenNamespace == "invalid framework group" &&
            (v.Payload as FrameworkReferencePayload)?.ForbiddenFrameworkGroup == "empty_group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_KnownUsableFrameworkGroup_ReturnsNoConfigurationViolation()
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
                    new() { Name = "domain-no-aspnet", Source = SourceAssemblyName, Forbidden = new List<string> { "forbidden_web" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject(SourceAssemblyName, ("Microsoft.NETCore.App", null))), document);
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

        var runner = new ArchitectureContractRunner(CreateContext(projectDiscovery: null), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ContractId == "domain-no-aspnet" && v.ForbiddenNamespace == "no project metadata discovered"), Is.True);
    }

    [Test]
    public void CheckConfiguration_SourceNotAmongDiscoveredProjects_ReturnsMissingProjectMetadataViolation()
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

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject("SomeOtherAssembly", ("Microsoft.NETCore.App", null))), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ContractId == "domain-no-aspnet" && v.ForbiddenNamespace == "no project metadata discovered"), Is.True);
    }

    [Test]
    public void CheckConfiguration_FrameworkAllowOnly_UnknownFrameworkGroup_ReturnsViolation()
    {
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

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject(SourceAssemblyName, ("Microsoft.NETCore.App", null))), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ForbiddenNamespace == "unknown framework group" &&
            (v.Payload as FrameworkReferencePayload)?.ForbiddenFrameworkGroup == "unknown_group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_SourceAmongDiscoveredProjects_ReturnsNoMissingMetadataViolation()
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

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject(SourceAssemblyName, ("Microsoft.NETCore.App", null))), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "no project metadata discovered"), Is.False);
    }
}
