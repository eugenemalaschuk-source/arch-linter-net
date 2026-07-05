using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PackageAllowOnlyContractTests
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
            new[] { typeof(PackageAllowOnlyContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
    }

    private static ArchitectureContractDocument CreateDocument(
        Dictionary<string, ArchitecturePackageGroup> packages,
        ArchitecturePackageAllowOnlyContract contract)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Packages = packages,
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string> { SourceAssemblyName } },
            Contracts = new ArchitectureContractGroups
            {
                StrictPackageAllowOnly = new List<ArchitecturePackageAllowOnlyContract> { contract }
            }
        };
    }

    private static ArchitectureDiscoveredProject Project(
        string assemblyName, params (string Id, string? Version)[] packages)
    {
        return new ArchitectureDiscoveredProject(
            $"src/{assemblyName}/{assemblyName}.csproj",
            assemblyName,
            new[] { "net10.0" },
            packages.Select(p => new ArchitectureDiscoveredPackageReference(p.Id, p.Version)).ToList());
    }

    [Test]
    public void CheckPackageAllowOnlyContract_AllReferencesAllowed_ReturnsNoViolations()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["test_frameworks"] = new() { PackageIds = { "NUnit" } }
        };
        var contract = new ArchitecturePackageAllowOnlyContract
        {
            Name = "Domain may only reference test frameworks",
            Source = SourceAssemblyName,
            Allowed = new List<string> { "test_frameworks" }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(CreateContext(Project(SourceAssemblyName, ("NUnit", "4.0.0"))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPackageAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckPackageAllowOnlyContract_ReferenceOutsideAllowedGroups_ReturnsViolation()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["test_frameworks"] = new() { PackageIds = { "NUnit" } }
        };
        var contract = new ArchitecturePackageAllowOnlyContract
        {
            Name = "Domain may only reference test frameworks",
            Id = "domain-allowed-packages",
            Source = SourceAssemblyName,
            Allowed = new List<string> { "test_frameworks" }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.EntityFrameworkCore", "8.0.0"))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPackageAllowOnlyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ContractId, Is.EqualTo("domain-allowed-packages"));
        Assert.That(violations[0].SourceType, Is.EqualTo(SourceAssemblyName));
        Assert.That(violations[0].ForbiddenNamespace, Is.EqualTo("outside allowed package groups"));
        Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(new[] { "Microsoft.EntityFrameworkCore@8.0.0" }));
    }

    [Test]
    public void CheckPackageAllowOnlyContract_MultipleDisallowedReferences_SortedAndDeduplicated()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["test_frameworks"] = new() { PackageIds = { "NUnit" } }
        };
        var contract = new ArchitecturePackageAllowOnlyContract
        {
            Name = "Domain may only reference test frameworks",
            Source = SourceAssemblyName,
            Allowed = new List<string> { "test_frameworks" }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Zebra.Sdk", "1.0.0"), ("Acme.Sdk", "1.0.0"))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPackageAllowOnlyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(new[] { "Acme.Sdk@1.0.0", "Zebra.Sdk@1.0.0" }));
    }

    [Test]
    public void CheckPackageAllowOnlyContract_IgnoredPackageId_SuppressesViolation()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["test_frameworks"] = new() { PackageIds = { "NUnit" } }
        };
        var contract = new ArchitecturePackageAllowOnlyContract
        {
            Name = "Domain may only reference test frameworks",
            Source = SourceAssemblyName,
            Allowed = new List<string> { "test_frameworks" },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new() { SourceType = SourceAssemblyName, ForbiddenReference = "Microsoft.EntityFrameworkCore", Reason = "fixture" },
            }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.EntityFrameworkCore", "8.0.0"))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPackageAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckPackageAllowOnlyContract_ContractNotSelected_ReturnsNoViolations()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["test_frameworks"] = new() { PackageIds = { "NUnit" } }
        };
        var contract = new ArchitecturePackageAllowOnlyContract
        {
            Name = "Domain may only reference test frameworks",
            Id = "domain-allowed-packages",
            Source = SourceAssemblyName,
            Allowed = new List<string> { "test_frameworks" }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.EntityFrameworkCore", "8.0.0"))), document,
            selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.Session.CheckPackageAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void ArchitecturePackageAllowOnlyContract_DependencyDepth_DefaultsToDirect()
    {
        var contract = new ArchitecturePackageAllowOnlyContract();

        Assert.That(contract.DependencyDepth, Is.EqualTo(DependencyDepthMode.Direct));
    }

    [Test]
    public void CheckPackageAllowOnlyContract_ProgrammaticTransitiveDepth_ThrowsActionableError()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["test_frameworks"] = new() { PackageIds = { "NUnit" } }
        };
        var contract = new ArchitecturePackageAllowOnlyContract
        {
            Name = "Domain may only reference test frameworks",
            Source = SourceAssemblyName,
            Allowed = new List<string> { "test_frameworks" },
            DependencyDepth = DependencyDepthMode.Transitive
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.EntityFrameworkCore", "8.0.0"))), document);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            runner.Session.CheckPackageAllowOnlyContract(contract))!;

        Assert.That(ex.Message, Does.Contain("dependency_depth: transitive"));
        Assert.That(ex.Message, Does.Contain("not supported yet"));
    }
}
