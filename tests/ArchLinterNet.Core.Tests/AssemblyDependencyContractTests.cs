using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AssemblyDependencyContractTests
{
    private static readonly Assembly _coreAssembly = typeof(ArchitectureContractDocument).Assembly;
    private static readonly Assembly _testingAssembly = typeof(ArchLinterNet.Testing.ArchitectureAssertions).Assembly;
    private static readonly Assembly _testsAssembly = typeof(AssemblyDependencyContractTests).Assembly;

    private static ArchitectureAnalysisContext CreateContext(params Assembly[] assemblies)
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            assemblies,
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static ArchitectureContractDocument CreateDocument(
        List<string> assemblyNames, ArchitectureAssemblyDependencyContract contract)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = assemblyNames },
            Contracts = new ArchitectureContractGroups
            {
                StrictAssemblyDependency = new List<ArchitectureAssemblyDependencyContract> { contract }
            }
        };
    }

    [Test]
    public void CheckAssemblyDependencyContract_NoDirectReference_ReturnsNoViolations()
    {
        Assembly nunitAssembly = typeof(Assert).Assembly;
        string testingName = _testingAssembly.GetName().Name!;
        string nunitName = nunitAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyDependencyContract
        {
            Name = "Testing must not reference nunit",
            Source = testingName,
            Forbidden = new List<string> { nunitName }
        };
        var document = CreateDocument(new List<string> { testingName, nunitName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly, nunitAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckAssemblyDependencyContract_DirectForbiddenReference_ReturnsViolation()
    {
        string coreName = _coreAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyDependencyContract
        {
            Name = "Testing must not reference Core",
            Id = "testing-no-core",
            Source = testingName,
            Forbidden = new List<string> { coreName }
        };
        var document = CreateDocument(new List<string> { testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly, _coreAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyDependencyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ContractId, Is.EqualTo("testing-no-core"));
        Assert.That(violations[0].SourceType, Is.EqualTo(testingName));
        Assert.That(violations[0].ForbiddenNamespace, Is.EqualTo(coreName));
        Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(new[] { $"{testingName} -> {coreName}" }),
            "Evidence must be deterministic source -> forbidden assembly text, not a filesystem path.");
    }

    [Test]
    public void CheckAssemblyDependencyContract_MultipleForbiddenAssemblies_OrderedByDeclaration()
    {
        // The test assembly directly references both ArchLinterNet.Testing and ArchLinterNet.Core.
        string testsName = _testsAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;
        string coreName = _coreAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyDependencyContract
        {
            Name = "Tests must not reference Testing or Core",
            Source = testsName,
            Forbidden = new List<string> { coreName, testingName }
        };
        var document = CreateDocument(new List<string> { testsName, testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(_testsAssembly, _testingAssembly, _coreAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyDependencyContract(contract);

        Assert.That(violations.Select(v => v.ForbiddenNamespace),
            Is.EqualTo(new[] { coreName, testingName }));
    }

    [Test]
    public void CheckAssemblyDependencyContract_SourceListedInOwnForbiddenList_NoSelfViolation()
    {
        string testingName = _testingAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyDependencyContract
        {
            Name = "Testing must not reference itself",
            Source = testingName,
            Forbidden = new List<string> { testingName }
        };
        var document = CreateDocument(new List<string> { testingName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckAssemblyDependencyContract_TransitiveOnlyReference_ReturnsNoViolation()
    {
        // Tests -> Testing -> Core is a real path, but Tests does not directly reference Core... except
        // it does in this project (test assembly references ArchLinterNet.Core directly too), so use a
        // pair with only an indirect relationship: Core does not reference Testing, so forbidding
        // Core -> Testing (an edge that would only exist transitively, if at all) must not be flagged.
        string coreName = _coreAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyDependencyContract
        {
            Name = "Core must not reference Testing",
            Source = coreName,
            Forbidden = new List<string> { testingName }
        };
        var document = CreateDocument(new List<string> { coreName, testingName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_coreAssembly, _testingAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckAssemblyDependencyContract_IgnoredPair_SuppressesViolation()
    {
        string coreName = _coreAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyDependencyContract
        {
            Name = "Testing must not reference Core",
            Source = testingName,
            Forbidden = new List<string> { coreName },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new() { SourceType = testingName, ForbiddenReference = coreName, Reason = "fixture" },
            }
        };
        var document = CreateDocument(new List<string> { testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly, _coreAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckAssemblyDependencyContract_ContractNotSelected_ReturnsNoViolations()
    {
        string coreName = _coreAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyDependencyContract
        {
            Name = "Testing must not reference Core",
            Id = "testing-no-core",
            Source = testingName,
            Forbidden = new List<string> { coreName }
        };
        var document = CreateDocument(new List<string> { testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(_testingAssembly, _coreAssembly), document, selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyDependencyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void ArchitectureAssemblyDependencyContract_DependencyDepth_DefaultsToDirect()
    {
        var contract = new ArchitectureAssemblyDependencyContract();

        Assert.That(contract.DependencyDepth, Is.EqualTo(DependencyDepthMode.Direct));
    }
}
