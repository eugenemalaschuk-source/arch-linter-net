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
    public void CheckFrameworkAllowOnlyContract_AllReferencesAllowed_ReturnsNoViolations()
    {
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
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.NETCore.App", null))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckFrameworkAllowOnlyContract_ReferenceOutsideAllowedGroups_ReturnsViolation()
    {
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
            CreateContext(Project(SourceAssemblyName, ("Microsoft.AspNetCore.App", null))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkAllowOnlyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ContractId, Is.EqualTo("domain-allowed-frameworks"));
        Assert.That(violations[0].SourceType, Is.EqualTo(SourceAssemblyName));
        Assert.That(violations[0].ForbiddenNamespace, Is.EqualTo("outside allowed framework groups"));
        Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(new[] { "Microsoft.AspNetCore.App" }));
    }

    [Test]
    public void CheckFrameworkAllowOnlyContract_MultipleDisallowedReferences_SortedAndDeduplicated()
    {
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
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(
                SourceAssemblyName,
                ("Microsoft.WindowsDesktop.App", null),
                ("Microsoft.AspNetCore.App", null))),
            document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkAllowOnlyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ForbiddenReferences,
            Is.EqualTo(new[] { "Microsoft.AspNetCore.App", "Microsoft.WindowsDesktop.App" }));
    }

    [Test]
    public void CheckFrameworkAllowOnlyContract_IgnoredFrameworkName_SuppressesViolation()
    {
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
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.AspNetCore.App", null))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckFrameworkAllowOnlyContract_ContractNotSelected_ReturnsNoViolations()
    {
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
            CreateContext(Project(SourceAssemblyName, ("Microsoft.AspNetCore.App", null))), document,
            selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.Session.CheckFrameworkAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }
}
