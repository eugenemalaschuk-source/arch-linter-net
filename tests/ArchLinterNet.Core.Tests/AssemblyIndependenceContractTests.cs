using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AssemblyIndependenceContractTests
{
    private static readonly Assembly _coreAssembly = typeof(ArchitectureContractDocument).Assembly;
    private static readonly Assembly _testingAssembly = typeof(ArchLinterNet.Testing.ArchitectureAssertions).Assembly;
    private static readonly Assembly _testsAssembly = typeof(AssemblyIndependenceContractTests).Assembly;

    private static ArchitectureAnalysisContext CreateContext(params Assembly[] assemblies)
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            assemblies,
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static ArchitectureContractDocument CreateDocument(
        List<string> assemblyNames, ArchitectureAssemblyIndependenceContract contract)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = assemblyNames },
            Contracts = new ArchitectureContractGroups
            {
                StrictAssemblyIndependence = new List<ArchitectureAssemblyIndependenceContract> { contract }
            }
        };
    }

    [Test]
    public void CheckAssemblyIndependenceContract_NoDirectReferenceEitherWay_ReturnsNoViolations()
    {
        // ArchLinterNet.Testing does not reference nunit.framework, and nunit.framework does not
        // reference ArchLinterNet.Testing, so the pair is genuinely independent.
        Assembly nunitAssembly = typeof(Assert).Assembly;
        string testingName = _testingAssembly.GetName().Name!;
        string nunitName = nunitAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyIndependenceContract
        {
            Name = "Independent Assemblies",
            Assemblies = new List<string> { testingName, nunitName }
        };
        var document = CreateDocument(new List<string> { testingName, nunitName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly, nunitAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyIndependenceContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckAssemblyIndependenceContract_DirectReference_ReturnsViolationWithSourceAndForbidden()
    {
        string coreName = _coreAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyIndependenceContract
        {
            Name = "Assembly Independence",
            Id = "no-cross-talk",
            Assemblies = new List<string> { testingName, coreName }
        };
        var document = CreateDocument(new List<string> { testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly, _coreAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyIndependenceContract(contract);

        // Testing -> Core is a real direct reference; Core does not reference Testing back.
        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ContractId, Is.EqualTo("no-cross-talk"));
        Assert.That(violations[0].SourceType, Is.EqualTo(testingName));
        Assert.That(violations[0].ForbiddenNamespace, Is.EqualTo(coreName));
    }

    [Test]
    public void CheckAssemblyIndependenceContract_MultipleForbiddenAssemblies_OrderedByDeclaration()
    {
        // The test assembly directly references both ArchLinterNet.Testing and ArchLinterNet.Core.
        string testsName = _testsAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;
        string coreName = _coreAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyIndependenceContract
        {
            Name = "Assembly Independence",
            Assemblies = new List<string> { testsName, testingName, coreName }
        };
        var document = CreateDocument(new List<string> { testsName, testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(_testsAssembly, _testingAssembly, _coreAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyIndependenceContract(contract);

        Assert.That(violations.Select(v => (v.SourceType, v.ForbiddenNamespace)),
            Is.EqualTo(new[] { (testsName, testingName), (testsName, coreName) }));
    }

    [Test]
    public void CheckAssemblyIndependenceContract_IgnoredPair_SuppressesViolation()
    {
        string coreName = _coreAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyIndependenceContract
        {
            Name = "Assembly Independence",
            Assemblies = new List<string> { testingName, coreName },
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new() { SourceType = testingName, ForbiddenReference = coreName, Reason = "fixture" },
            }
        };
        var document = CreateDocument(new List<string> { testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly, _coreAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyIndependenceContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckAssemblyIndependenceContract_ContractNotSelected_ReturnsNoViolations()
    {
        string coreName = _coreAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyIndependenceContract
        {
            Name = "Assembly Independence",
            Id = "no-cross-talk",
            Assemblies = new List<string> { testingName, coreName }
        };
        var document = CreateDocument(new List<string> { testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(_testingAssembly, _coreAssembly), document, selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyIndependenceContract(contract);

        Assert.That(violations, Is.Empty);
    }
}
