using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class FrameworkReferenceAllowOnlyContractTests
{
    private const string SourceAssemblyName = "MyApp.Domain";

    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Combine(Path.GetTempPath(), $"arch-linter-framework-allowonly-{Guid.NewGuid():N}");
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
            new[] { typeof(FrameworkReferenceAllowOnlyContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
    }

    private static ArchitectureContractDocument CreateDocument(
        Dictionary<string, ArchitectureFrameworkReferenceGroup> frameworkReferences,
        ArchitectureFrameworkReferenceAllowOnlyContract contract)
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
                StrictFrameworkAllowOnly = new List<ArchitectureFrameworkReferenceAllowOnlyContract> { contract }
            }
        };
    }

    [Test]
    public void CheckFrameworkAllowOnlyContract_AllReferencesAllowed_ReturnsNoViolations()
    {
        string projectPath = CreateProject(SourceAssemblyName, string.Empty);
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["core"] = new() { FrameworkNames = { "Microsoft.NETCore.App" } }
        };
        var contract = new ArchitectureFrameworkReferenceAllowOnlyContract
        {
            Name = "Domain may only reference the core framework",
            Source = SourceAssemblyName,
            Allowed = new List<string> { "core" }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckFrameworkAllowOnlyContract_ReferenceOutsideAllowedGroups_ReturnsViolation()
    {
        string projectPath = CreateProject(
            SourceAssemblyName, """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["core"] = new() { FrameworkNames = { "Microsoft.NETCore.App" } }
        };
        var contract = new ArchitectureFrameworkReferenceAllowOnlyContract
        {
            Name = "Domain may only reference the core framework",
            Id = "domain-allowed-frameworks",
            Source = SourceAssemblyName,
            Allowed = new List<string> { "core" }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkAllowOnlyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ContractId, Is.EqualTo("domain-allowed-frameworks"));
        Assert.That(violations[0].SourceType, Is.EqualTo(SourceAssemblyName));
        Assert.That(violations[0].ForbiddenNamespace, Is.EqualTo("outside allowed framework groups"));
        Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(new[] { "Microsoft.AspNetCore.App (net10.0)" }));
    }

    [Test]
    public void CheckFrameworkAllowOnlyContract_MultipleDisallowedReferences_SortedAndDeduplicated()
    {
        // Two distinct disallowed frameworks: the explicit ASP.NET Core reference and the always-
        // present implicit Microsoft.NETCore.App (neither matches an "allowed" group that names
        // something else entirely) - avoids Microsoft.WindowsDesktop.App, which fails MSBuild
        // evaluation on non-Windows hosts (EnableWindowsTargeting).
        string projectPath = CreateProject(
            SourceAssemblyName, """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["unrelated"] = new() { FrameworkNames = { "Some.Unrelated.Framework" } }
        };
        var contract = new ArchitectureFrameworkReferenceAllowOnlyContract
        {
            Name = "Domain may only reference an unrelated framework",
            Source = SourceAssemblyName,
            Allowed = new List<string> { "unrelated" }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkAllowOnlyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ForbiddenReferences,
            Is.EqualTo(new[] { "Microsoft.AspNetCore.App (net10.0)", "Microsoft.NETCore.App (net10.0)" }));
    }

    [Test]
    public void CheckFrameworkAllowOnlyContract_IgnoredFrameworkName_SuppressesViolation()
    {
        string projectPath = CreateProject(
            SourceAssemblyName, """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["core"] = new() { FrameworkNames = { "Microsoft.NETCore.App" } }
        };
        var contract = new ArchitectureFrameworkReferenceAllowOnlyContract
        {
            Name = "Domain may only reference the core framework",
            Source = SourceAssemblyName,
            Allowed = new List<string> { "core" },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new() { SourceType = SourceAssemblyName, ForbiddenReference = "Microsoft.AspNetCore.App", Reason = "fixture" },
            }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(CreateContext(projectPath), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckFrameworkAllowOnlyContract_ContractNotSelected_ReturnsNoViolations()
    {
        string projectPath = CreateProject(
            SourceAssemblyName, """<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
        var frameworkReferences = new Dictionary<string, ArchitectureFrameworkReferenceGroup>
        {
            ["core"] = new() { FrameworkNames = { "Microsoft.NETCore.App" } }
        };
        var contract = new ArchitectureFrameworkReferenceAllowOnlyContract
        {
            Name = "Domain may only reference the core framework",
            Id = "domain-allowed-frameworks",
            Source = SourceAssemblyName,
            Allowed = new List<string> { "core" }
        };
        var document = CreateDocument(frameworkReferences, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(projectPath), document,
            selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }
}
