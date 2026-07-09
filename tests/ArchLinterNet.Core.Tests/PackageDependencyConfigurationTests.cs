using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PackageDependencyConfigurationTests
{
    private const string SourceAssemblyName = "MyApp.Domain";

    private static ArchitectureAnalysisContext CreateContext(ProjectDiscoveryResult? projectDiscovery = null)
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(PackageDependencyConfigurationTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: projectDiscovery);
    }

    private static ProjectDiscoveryResult DiscoveryWithProject(
        string assemblyName, params (string Id, string? Version)[] packages)
    {
        var project = new ArchitectureDiscoveredProject(
            $"src/{assemblyName}/{assemblyName}.csproj",
            assemblyName,
            new[] { "net10.0" },
            packages.Select(p => new ArchitectureDiscoveredPackageReference(p.Id, p.Version)).ToList());

        return new ProjectDiscoveryResult(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = new[] { project }
        };
    }

    [Test]
    public void CheckConfiguration_UnknownPackageGroup_ReturnsViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictPackageDependency = new List<ArchitecturePackageDependencyContract>
                {
                    new() { Name = "domain-no-unknown", Source = SourceAssemblyName, Forbidden = new List<string> { "unknown_group" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject(SourceAssemblyName, ("Newtonsoft.Json", "13.0.3"))), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "unknown package group" && v.ForbiddenPackageGroup == "unknown_group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_UnknownPackageGroup_TypoInForbiddenList_DoesNotSilentlyPassStrictValidation()
    {
        // Regression coverage for the false-green risk this check exists to close: a typo'd
        // group name in `forbidden` must surface as a configuration violation instead of the
        // contract silently matching nothing and strict validation passing clean.
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Packages = new Dictionary<string, ArchitecturePackageGroup>
            {
                ["forbidden_infra"] = new() { PackageIds = { "Microsoft.EntityFrameworkCore" } }
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictPackageDependency = new List<ArchitecturePackageDependencyContract>
                {
                    // Typo: "forbidden_infraa" instead of the declared "forbidden_infra".
                    new() { Name = "domain-no-ef", Source = SourceAssemblyName, Forbidden = new List<string> { "forbidden_infraa" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject(SourceAssemblyName, ("Microsoft.EntityFrameworkCore", "8.0.0"))), document);

        List<ArchitectureViolation> configurationViolations = runner.CheckConfiguration();
        List<ArchitectureViolation> contractViolations = runner.Session.CheckPackageDependencyContract(
            document.Contracts.StrictPackageDependency[0]);

        Assert.That(configurationViolations.Any(v => v.ForbiddenNamespace == "unknown package group"), Is.True);
        Assert.That(contractViolations, Is.Empty,
            "The contract itself still finds nothing for the mistyped group name; CheckConfiguration is what must catch this.");
    }

    [Test]
    public void CheckConfiguration_PackageGroupWithoutMatchers_ReturnsViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Packages = new Dictionary<string, ArchitecturePackageGroup> { ["empty_group"] = new() },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictPackageDependency = new List<ArchitecturePackageDependencyContract>
                {
                    new() { Name = "domain-no-empty-group", Source = SourceAssemblyName, Forbidden = new List<string> { "empty_group" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject(SourceAssemblyName)), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "invalid package group" && v.ForbiddenPackageGroup == "empty_group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_KnownUsablePackageGroup_ReturnsNoConfigurationViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Packages = new Dictionary<string, ArchitecturePackageGroup>
            {
                ["forbidden_infra"] = new() { PackageIds = { "Microsoft.EntityFrameworkCore" } }
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictPackageDependency = new List<ArchitecturePackageDependencyContract>
                {
                    new() { Name = "domain-no-ef", Source = SourceAssemblyName, Forbidden = new List<string> { "forbidden_infra" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject(SourceAssemblyName, ("Newtonsoft.Json", "13.0.3"))), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckConfiguration_NoProjectDiscoveryConfigured_ReturnsMissingPackageMetadataViolation()
    {
        // Regression coverage for the "package contract silently no-ops with no discovery data"
        // risk: when analysis.solution/analysis.projects were never configured, Context.ProjectDiscovery
        // is null and CheckPackageDependencyContract would otherwise return an empty violation list
        // with no visible signal that the contract never actually evaluated anything.
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Packages = new Dictionary<string, ArchitecturePackageGroup>
            {
                ["forbidden_infra"] = new() { PackageIds = { "Microsoft.EntityFrameworkCore" } }
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictPackageDependency = new List<ArchitecturePackageDependencyContract>
                {
                    new() { Id = "domain-no-ef", Name = "domain-no-ef", Source = SourceAssemblyName, Forbidden = new List<string> { "forbidden_infra" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(CreateContext(projectDiscovery: null), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ContractId == "domain-no-ef" && v.ForbiddenNamespace == "no package metadata discovered"), Is.True);
    }

    [Test]
    public void CheckConfiguration_SourceNotAmongDiscoveredProjects_ReturnsMissingPackageMetadataViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Packages = new Dictionary<string, ArchitecturePackageGroup>
            {
                ["forbidden_infra"] = new() { PackageIds = { "Microsoft.EntityFrameworkCore" } }
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictPackageDependency = new List<ArchitecturePackageDependencyContract>
                {
                    new() { Id = "domain-no-ef", Name = "domain-no-ef", Source = SourceAssemblyName, Forbidden = new List<string> { "forbidden_infra" } }
                }
            }
        };

        // Discovery ran, but found a different project than the contract's declared source.
        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject("SomeOtherAssembly", ("Newtonsoft.Json", "13.0.3"))), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v =>
            v.ContractId == "domain-no-ef" && v.ForbiddenNamespace == "no package metadata discovered"), Is.True);
    }

    [Test]
    public void CheckConfiguration_PackageAllowOnly_UnknownPackageGroup_ReturnsViolation()
    {
        // Same "unknown package group" diagnostic as package_dependency, but exercised through
        // package_allow_only's ConfigurationContributor to prove the per-family registry wiring
        // (introduced in #212) still reports both package-referencing families identically.
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictPackageAllowOnly = new List<ArchitecturePackageAllowOnlyContract>
                {
                    new() { Name = "domain-allow-only-known", Source = SourceAssemblyName, Allowed = new List<string> { "unknown_group" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject(SourceAssemblyName, ("Newtonsoft.Json", "13.0.3"))), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "unknown package group" && v.ForbiddenPackageGroup == "unknown_group"), Is.True);
    }

    [Test]
    public void CheckConfiguration_SourceAmongDiscoveredProjects_ReturnsNoMissingMetadataViolation()
    {
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Packages = new Dictionary<string, ArchitecturePackageGroup>
            {
                ["forbidden_infra"] = new() { PackageIds = { "Microsoft.EntityFrameworkCore" } }
            },
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictPackageDependency = new List<ArchitecturePackageDependencyContract>
                {
                    new() { Id = "domain-no-ef", Name = "domain-no-ef", Source = SourceAssemblyName, Forbidden = new List<string> { "forbidden_infra" } }
                }
            }
        };

        var runner = new ArchitectureContractRunner(
            CreateContext(DiscoveryWithProject(SourceAssemblyName, ("Newtonsoft.Json", "13.0.3"))), document);
        List<ArchitectureViolation> violations = runner.CheckConfiguration();

        Assert.That(violations.Any(v => v.ForbiddenNamespace == "no package metadata discovered"), Is.False);
    }
}
