using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class PackageDependencyContractTests
{
    private static readonly string[] _value = { "net10.0" };
    private static readonly string[] _value1 = { "Microsoft.EntityFrameworkCore@8.0.0" };
    private static readonly string[] _value2 = {
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.EntityFrameworkCore.SqlServer",
                };
    private static readonly string[] _value3 = { "Microsoft.EntityFrameworkCore" };
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
            new[] { typeof(PackageDependencyContractTests).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>(),
            projectDiscovery: discovery);
    }

    private static ArchitectureContractDocument CreateDocument(
        Dictionary<string, ArchitecturePackageGroup> packages,
        ArchitecturePackageDependencyContract contract)
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
                StrictPackageDependency = new List<ArchitecturePackageDependencyContract> { contract }
            }
        };
    }

    private static ArchitectureDiscoveredProject Project(
        string assemblyName, params (string Id, string? Version)[] packages)
    {
        return new ArchitectureDiscoveredProject(
            $"src/{assemblyName}/{assemblyName}.csproj",
            assemblyName,
            _value,
            packages.Select(p => new ArchitectureDiscoveredPackageReference(p.Id, p.Version)).ToList());
    }

    [Test]
    public void CheckPackageDependencyContract_NoForbiddenPackageReference_ReturnsNoViolations()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["forbidden_infra"] = new() { PackageIds = { "Microsoft.EntityFrameworkCore" } }
        };
        var contract = new ArchitecturePackageDependencyContract
        {
            Name = "Domain must not reference EF Core",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_infra" }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Newtonsoft.Json", "13.0.3"))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPackageDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckPackageDependencyContract_DirectForbiddenPackageReference_ReturnsViolationWithVersion()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["forbidden_infra"] = new() { PackageIds = { "Microsoft.EntityFrameworkCore" } }
        };
        var contract = new ArchitecturePackageDependencyContract
        {
            Name = "Domain must not reference EF Core",
            Id = "domain-no-ef",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_infra" }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.EntityFrameworkCore", "8.0.0"))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPackageDependencyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ContractId, Is.EqualTo("domain-no-ef"));
        Assert.That(violations[0].SourceType, Is.EqualTo(SourceAssemblyName));
        Assert.That((violations[0].Payload as PackageDependencyPayload)?.ForbiddenPackageGroup, Is.EqualTo("forbidden_infra"));
        Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(_value1));
    }

    [Test]
    public void Executor_PackageReferences_RetainOneAuthoritativeIdentityPerFormattedReference()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["forbidden_infra"] = new() { PackagePrefixes = { "Microsoft.EntityFrameworkCore" } }
        };
        var contract = new ArchitecturePackageDependencyContract
        {
            Name = "Domain must not reference EF Core",
            Id = "domain-no-ef",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_infra" }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(
                SourceAssemblyName,
                ("Microsoft.EntityFrameworkCore", "8.0.0"),
                ("Microsoft.EntityFrameworkCore.SqlServer", "8.0.0"))),
            document);

        ArchitectureContractExecutionResult result = new ArchitectureContractExecutor()
            .Execute(runner.Session, "strict", new ArchitectureContractHandlerRegistry());
        ArchitectureViolation violation = result.Violations.Single();
        IReadOnlyList<ArchitectureFinding> findings = ArchitectureFindingMapper.FromViolations(
            result.Violations,
            "strict");

        Assert.Multiple(() =>
        {
            Assert.That(violation.Identities, Has.Count.EqualTo(2));
            Assert.That(
                violation.Identities.Select(identity => identity.TargetMember),
                Is.EquivalentTo(_value2));
            Assert.That(findings, Has.Count.EqualTo(2));
            Assert.That(
                findings.Select(finding => finding.CanonicalIdentity).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(2));
        });
    }

    [Test]
    public void CheckPackageDependencyContract_ForbiddenPackageWithoutVersion_EvidenceOmitsVersion()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["forbidden_infra"] = new() { PackageIds = { "Microsoft.EntityFrameworkCore" } }
        };
        var contract = new ArchitecturePackageDependencyContract
        {
            Name = "Domain must not reference EF Core",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_infra" }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.EntityFrameworkCore", null))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPackageDependencyContract(contract);

        Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(_value3));
    }

    [Test]
    public void CheckPackageDependencyContract_PackagePrefixMatch_ReturnsViolation()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["forbidden_infra"] = new() { PackagePrefixes = { "Microsoft.EntityFrameworkCore" } }
        };
        var contract = new ArchitecturePackageDependencyContract
        {
            Name = "Domain must not reference EF Core family",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_infra" }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.EntityFrameworkCore.SqlServer", "8.0.0"))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPackageDependencyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
    }

    [Test]
    public void CheckPackageDependencyContract_PackagePrefixSibling_DoesNotMatch()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["forbidden_infra"] = new() { PackagePrefixes = { "Microsoft.EntityFrameworkCore" } }
        };
        var contract = new ArchitecturePackageDependencyContract
        {
            Name = "Domain must not reference EF Core family",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_infra" }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.EntityFrameworkCoreTools.Widget", "1.0.0"))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPackageDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckPackageDependencyContract_IgnoredPackageId_SuppressesViolation()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["forbidden_infra"] = new() { PackageIds = { "Microsoft.EntityFrameworkCore" } }
        };
        var contract = new ArchitecturePackageDependencyContract
        {
            Name = "Domain must not reference EF Core",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_infra" },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new() { SourceType = SourceAssemblyName, ForbiddenReference = "Microsoft.EntityFrameworkCore", Reason = "fixture" },
            }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.EntityFrameworkCore", "8.0.0"))), document);

        List<ArchitectureViolation> violations = runner.Session.CheckPackageDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckPackageDependencyContract_ContractNotSelected_ReturnsNoViolations()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["forbidden_infra"] = new() { PackageIds = { "Microsoft.EntityFrameworkCore" } }
        };
        var contract = new ArchitecturePackageDependencyContract
        {
            Name = "Domain must not reference EF Core",
            Id = "domain-no-ef",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_infra" }
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.EntityFrameworkCore", "8.0.0"))), document,
            selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.Session.CheckPackageDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void ArchitecturePackageDependencyContract_DependencyDepth_DefaultsToDirect()
    {
        var contract = new ArchitecturePackageDependencyContract();

        Assert.That(contract.DependencyDepth, Is.EqualTo(DependencyDepthMode.Direct));
    }

    [Test]
    public void CheckPackageDependencyContract_ProgrammaticTransitiveDepth_ThrowsActionableError()
    {
        var packages = new Dictionary<string, ArchitecturePackageGroup>
        {
            ["forbidden_infra"] = new() { PackageIds = { "Microsoft.EntityFrameworkCore" } }
        };
        var contract = new ArchitecturePackageDependencyContract
        {
            Name = "Domain must not reference EF Core",
            Source = SourceAssemblyName,
            Forbidden = new List<string> { "forbidden_infra" },
            DependencyDepth = DependencyDepthMode.Transitive
        };
        var document = CreateDocument(packages, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(Project(SourceAssemblyName, ("Microsoft.EntityFrameworkCore", "8.0.0"))), document);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            runner.Session.CheckPackageDependencyContract(contract))!;

        Assert.That(ex.Message, Does.Contain("dependency_depth: transitive"));
        Assert.That(ex.Message, Does.Contain("not supported yet"));
    }

}
