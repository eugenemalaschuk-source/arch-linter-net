using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class FrameworkReferenceContractTests
{
    private const string SourceAssemblyName = "MyApp.Domain";

    private static ArchitectureAnalysisContext CreateContext(
        params ArchitectureDiscoveredProject[] discoveredProjects)
    {
        ProjectDiscoveryResult discovery = new(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects = discoveredProjects
        };

        return new ArchitectureAnalysisContext(
            "/tmp",
            new[] { typeof(FrameworkReferenceContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
    }

    private static ArchitectureContractDocument CreateDocument(
        Dictionary<string, ArchitectureFrameworkReferenceGroup> frameworkReferences,
        ArchitectureFrameworkReferenceContract contract)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            FrameworkReferences = frameworkReferences,
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract> { contract }
            }
        };
    }

    private static ArchitectureDiscoveredProject Project(
        string assemblyName, params (string Name, string? Condition)[] frameworks)
    {
        return new ArchitectureDiscoveredProject(
            $"src/{assemblyName}/{assemblyName}.csproj",
            assemblyName,
            new[] { "net10.0" },
            Array.Empty<ArchitectureDiscoveredPackageReference>(),
            frameworks.Select(f => new ArchitectureDiscoveredFrameworkReference(f.Name, f.Condition)).ToList());
    }

    [Test]
    public void CheckFrameworkDependencyContract_NoForbiddenFrameworkReference_ReturnsNoViolations()
    {
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
        };
        var contract = new ArchitectureFrameworkReferenceContract
        {
            Name = "Domain must not reference ASP.NET Core",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_web" }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.NETCore.App", null))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckFrameworkDependencyContract_DirectForbiddenFrameworkReference_ReturnsViolation()
    {
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
        };
        var contract = new ArchitectureFrameworkReferenceContract
        {
            Name = "Domain must not reference ASP.NET Core",
            Id = "domain-no-aspnet",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_web" }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.AspNetCore.App", null))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ContractId, Is.EqualTo("domain-no-aspnet"));
        Assert.That(violations[0].SourceType, Is.EqualTo(SourceAssemblyName));
        Assert.That((violations[0].Payload as FrameworkReferencePayload)?.ForbiddenFrameworkGroup, Is.EqualTo("forbidden_web"));
        Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(new[] { "Microsoft.AspNetCore.App" }));
    }

    [Test]
    public void CheckFrameworkDependencyContract_ForbiddenFrameworkWithCondition_EvidenceIncludesCondition()
    {
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
        };
        var contract = new ArchitectureFrameworkReferenceContract
        {
            Name = "Domain must not reference ASP.NET Core",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_web" }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.AspNetCore.App", "'$(TargetFramework)'=='net10.0'"))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations[0].ForbiddenReferences.Single(), Does.Contain("Microsoft.AspNetCore.App"));
        Assert.That(violations[0].ForbiddenReferences.Single(), Does.Contain("'$(TargetFramework)'=='net10.0'"));
    }

    [Test]
    public void CheckFrameworkDependencyContract_FrameworkPrefixMatch_ReturnsViolation()
    {
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["forbidden_web"] = new() { FrameworkNamePrefixes = { "Microsoft.AspNetCore" } }
        };
        var contract = new ArchitectureFrameworkReferenceContract
        {
            Name = "Domain must not reference ASP.NET Core family",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_web" }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.AspNetCore.App", null))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
    }

    [Test]
    public void CheckFrameworkDependencyContract_FrameworkPrefixSibling_DoesNotMatch()
    {
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["forbidden_web"] = new() { FrameworkNamePrefixes = { "Microsoft.AspNetCore" } }
        };
        var contract = new ArchitectureFrameworkReferenceContract
        {
            Name = "Domain must not reference ASP.NET Core family",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_web" }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.AspNetCoreTools.Widget", null))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckFrameworkDependencyContract_IgnoredFrameworkName_SuppressesViolation()
    {
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
        };
        var contract = new ArchitectureFrameworkReferenceContract
        {
            Name = "Domain must not reference ASP.NET Core",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_web" },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new() { SourceType = SourceAssemblyName, ForbiddenReference = "Microsoft.AspNetCore.App", Reason = "fixture" },
            }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.AspNetCore.App", null))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckFrameworkDependencyContract_ContractNotSelected_ReturnsNoViolations()
    {
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
        };
        var contract = new ArchitectureFrameworkReferenceContract
        {
            Name = "Domain must not reference ASP.NET Core",
            Id = "domain-no-aspnet",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_web" }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.AspNetCore.App", null))), document,
            selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckFrameworkDependencyContract_SameFrameworkInTwoProjects_ProducesDistinctViolations()
    {
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
        };
        var contractApi = new ArchitectureFrameworkReferenceContract
        {
            Name = "Api must not reference ASP.NET Core",
            Source = "MyApp.Api",
            Forbidden = new List<string> { "forbidden_web" }
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            FrameworkReferences = frameworkReferences,
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { "MyApp.Api" } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract> { contractApi }
            }
        };
        var runner = new ArchitectureContractRunner(
            CreateContext(
                Project("MyApp.Api", ("Microsoft.AspNetCore.App", null)),
                Project("MyApp.Worker", ("Microsoft.AspNetCore.App", null))),
            document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contractApi);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].SourceType, Is.EqualTo("MyApp.Api"));
    }
}
