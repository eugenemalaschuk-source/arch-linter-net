using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

// Contract evaluation now goes through a real Buildalyzer-backed MSBuild design-time build (see
// ArchitectureFrameworkReferenceEvaluator), so these fixtures are real on-disk .csproj files, not
// fabricated ArchitectureDiscoveredProject instances - the checks under test genuinely exercise
// MSBuild condition/import evaluation, not a hand-built fake.
[TestFixture]
public sealed class FrameworkReferenceContractTests
{
    private const string SourceAssemblyName = "MyApp.Domain";

    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-framework-contract-{Guid.NewGuid():N}");
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

    private string CreateProject(string assemblyName, string itemGroupBody)
    {
        string projectDir = Path.Combine(_repoRoot, assemblyName);
        Directory.CreateDirectory(projectDir);
        string projectPath = Path.Combine(projectDir, $"{assemblyName}.csproj");

        File.WriteAllText(projectPath, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
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
            new[] { typeof(FrameworkReferenceContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
    }

    private static ArchitectureContractDocument CreateDocument(
        Dictionary<string, ArchitectureFrameworkReferenceGroup> frameworkReferences,
        ArchitectureFrameworkReferenceContract contract,
        string sourceAssemblyName = SourceAssemblyName)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            FrameworkReferences = frameworkReferences,
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { sourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictFrameworkDependency = new List<ArchitectureFrameworkReferenceContract> { contract }
            }
        };
    }

    [Test]
    public void CheckFrameworkDependencyContract_NoForbiddenFrameworkReference_ReturnsNoViolations()
    {
        string projectPath = CreateProject(SourceAssemblyName, string.Empty);
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
        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckFrameworkDependencyContract_DirectForbiddenFrameworkReference_ReturnsViolation()
    {
        string projectPath = CreateProject(
            SourceAssemblyName, """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
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
        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ContractId, Is.EqualTo("domain-no-aspnet"));
        Assert.That(violations[0].SourceType, Is.EqualTo(SourceAssemblyName));
        Assert.That((violations[0].Payload as FrameworkReferencePayload)?.ForbiddenFrameworkGroup, Is.EqualTo("forbidden_web"));
        Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(new[] { "Microsoft.AspNetCore.App (net10.0)" }));

        FrameworkReferenceEvidence evidence = (violations[0].Payload as FrameworkReferencePayload)!.Evidence!.Single();
        Assert.That(evidence.FrameworkName, Is.EqualTo("Microsoft.AspNetCore.App"));
        Assert.That(evidence.TargetFramework, Is.EqualTo("net10.0"));
        Assert.That(evidence.Explicit, Is.True);
    }

    [Test]
    public void CheckFrameworkDependencyContract_ImplicitFrameworkReference_IsNotExplicit()
    {
        string projectPath = CreateProject(SourceAssemblyName, string.Empty);
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["core"] = new() { FrameworkNames = { "Microsoft.NETCore.App" } }
        };
        var contract = new ArchitectureFrameworkReferenceContract
        {
            Name = "Domain must not reference the core framework",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "core" }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        FrameworkReferenceEvidence evidence = (violations[0].Payload as FrameworkReferencePayload)!.Evidence!.Single();
        Assert.That(evidence.Explicit, Is.False);
    }

    [Test]
    public void CheckFrameworkDependencyContract_FrameworkPrefixMatch_ReturnsViolation()
    {
        string projectPath = CreateProject(
            SourceAssemblyName, """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
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
        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
    }

    [Test]
    public void CheckFrameworkDependencyContract_IgnoredFrameworkName_SuppressesViolation()
    {
        string projectPath = CreateProject(
            SourceAssemblyName, """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
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
        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckFrameworkDependencyContract_ContractNotSelected_ReturnsNoViolations()
    {
        string projectPath = CreateProject(
            SourceAssemblyName, """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
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
            CreateContext(projectPath), document,
            selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckFrameworkDependencyContract_SameFrameworkInTwoProjects_ProducesDistinctViolations()
    {
        string apiProjectPath = CreateProject(
            "MyApp.Api", """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
        string workerProjectPath = CreateProject(
            "MyApp.Worker", """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");

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
        var runner = new ArchitectureContractRunner(CreateContext(apiProjectPath, workerProjectPath), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contractApi);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].SourceType, Is.EqualTo("MyApp.Api"));
    }

    [Test]
    public void CheckFrameworkDependencyContract_UnevaluableTargetFramework_ProducesNoViolationsFromThisCheck()
    {
        // Fail-closed happens at CheckConfiguration level (see FrameworkReferenceConfigurationTests);
        // the contract check itself must not crash or fabricate a result when MSBuild evaluation fails.
        string projectPath = CreateProject("MyApp.Broken", string.Empty);
        File.WriteAllText(Path.Combine(_repoRoot, "MyApp.Broken", "MyApp.Broken.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net1.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
            </Project>
            """);

        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["forbidden_web"] = new() { FrameworkNames = { "Microsoft.AspNetCore.App" } }
        };
        var contract = new ArchitectureFrameworkReferenceContract
        {
            Name = "Domain must not reference ASP.NET Core",
            Source = "MyApp.Broken",
            Forbidden = new List<string> { "forbidden_web" }
        };
        var document = CreateDocument(frameworkReferences, contract, "MyApp.Broken");
        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }
}
