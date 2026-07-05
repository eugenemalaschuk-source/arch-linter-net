using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AssemblyAllowOnlyContractTests
{
    private static readonly Assembly _coreAssembly = typeof(ArchitectureContractDocument).Assembly;
    private static readonly Assembly _testingAssembly = typeof(ArchLinterNet.Testing.ArchitectureAssertions).Assembly;
    private static readonly Assembly _testsAssembly = typeof(AssemblyAllowOnlyContractTests).Assembly;

    private static ArchitectureAnalysisContext CreateContext(params Assembly[] assemblies)
    {
        return new ArchitectureAnalysisContext(
            "/tmp",
            assemblies,
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static ArchitectureContractDocument CreateDocument(
        List<string> assemblyNames, ArchitectureAssemblyAllowOnlyContract contract)
    {
        return new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = assemblyNames },
            Contracts = new ArchitectureContractGroups
            {
                StrictAssemblyAllowOnly = new List<ArchitectureAssemblyAllowOnlyContract> { contract }
            }
        };
    }

    [Test]
    public void CheckAssemblyAllowOnlyContract_AllReferencesAllowed_ReturnsNoViolations()
    {
        // ArchLinterNet.Testing directly references ArchLinterNet.Core; allowing Core covers it.
        string testingName = _testingAssembly.GetName().Name!;
        string coreName = _coreAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyAllowOnlyContract
        {
            Name = "Testing may only reference Core",
            Source = testingName,
            Allowed = new List<string> { coreName }
        };
        var document = CreateDocument(new List<string> { testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly, _coreAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckAssemblyAllowOnlyContract_ReferenceOutsideAllowed_ReturnsViolation()
    {
        string testingName = _testingAssembly.GetName().Name!;
        string coreName = _coreAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyAllowOnlyContract
        {
            Name = "Testing may not reference anything",
            Id = "testing-allow-only",
            Source = testingName,
            Allowed = new List<string>()
        };
        var document = CreateDocument(new List<string> { testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly, _coreAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyAllowOnlyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ContractId, Is.EqualTo("testing-allow-only"));
        Assert.That(violations[0].SourceType, Is.EqualTo(testingName));
        Assert.That(violations[0].ForbiddenNamespace, Is.EqualTo("outside allowed assemblies"));
        Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(new[] { coreName }));
    }

    [Test]
    public void CheckAssemblyAllowOnlyContract_ReferenceToUndeclaredAssembly_ReturnsNoViolation()
    {
        // Core is not part of Context.TargetAssemblies here, so a direct Testing -> Core reference
        // must not be reported even though it is not in `allowed`.
        string testingName = _testingAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyAllowOnlyContract
        {
            Name = "Testing may not reference anything declared",
            Source = testingName,
            Allowed = new List<string>()
        };
        var document = CreateDocument(new List<string> { testingName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckAssemblyAllowOnlyContract_MultipleDisallowedReferences_SortedAndDeduplicated()
    {
        // The test assembly directly references both ArchLinterNet.Core and ArchLinterNet.Testing.
        string testsName = _testsAssembly.GetName().Name!;
        string testingName = _testingAssembly.GetName().Name!;
        string coreName = _coreAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyAllowOnlyContract
        {
            Name = "Tests may not reference anything declared",
            Source = testsName,
            Allowed = new List<string>()
        };
        var document = CreateDocument(new List<string> { testsName, testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(_testsAssembly, _testingAssembly, _coreAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyAllowOnlyContract(contract);

        Assert.That(violations, Has.Count.EqualTo(1));
        Assert.That(violations[0].ForbiddenReferences, Is.EqualTo(new[] { coreName, testingName }));
    }

    [Test]
    public void CheckAssemblyAllowOnlyContract_IgnoredPair_SuppressesViolation()
    {
        string testingName = _testingAssembly.GetName().Name!;
        string coreName = _coreAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyAllowOnlyContract
        {
            Name = "Testing may not reference anything",
            Source = testingName,
            Allowed = new List<string>(),
            IgnoredViolations = new List<ArchitectureIgnoredViolation>
            {
                new() { SourceType = testingName, ForbiddenReference = coreName, Reason = "fixture" },
            }
        };
        var document = CreateDocument(new List<string> { testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly, _coreAssembly), document);

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void CheckAssemblyAllowOnlyContract_ContractNotSelected_ReturnsNoViolations()
    {
        string testingName = _testingAssembly.GetName().Name!;
        string coreName = _coreAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyAllowOnlyContract
        {
            Name = "Testing may not reference anything",
            Id = "testing-allow-only",
            Source = testingName,
            Allowed = new List<string>()
        };
        var document = CreateDocument(new List<string> { testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(
            CreateContext(_testingAssembly, _coreAssembly), document, selectedContractIds: new HashSet<string> { "some-other-contract" });

        List<ArchitectureViolation> violations = runner.Session.CheckAssemblyAllowOnlyContract(contract);

        Assert.That(violations, Is.Empty);
    }

    [Test]
    public void ArchitectureAssemblyAllowOnlyContract_DependencyDepth_DefaultsToDirect()
    {
        var contract = new ArchitectureAssemblyAllowOnlyContract();

        Assert.That(contract.DependencyDepth, Is.EqualTo(DependencyDepthMode.Direct));
    }

    [Test]
    public void CheckAssemblyAllowOnlyContract_ProgrammaticTransitiveDepth_ThrowsActionableError()
    {
        // Contracts built directly (not via the YAML loader) must still be rejected at check time,
        // so a programmatic/API caller cannot bypass the loader's dependency_depth guard.
        string testingName = _testingAssembly.GetName().Name!;
        string coreName = _coreAssembly.GetName().Name!;

        var contract = new ArchitectureAssemblyAllowOnlyContract
        {
            Name = "Testing may not reference anything",
            Source = testingName,
            Allowed = new List<string>(),
            DependencyDepth = DependencyDepthMode.Transitive
        };
        var document = CreateDocument(new List<string> { testingName, coreName }, contract);
        var runner = new ArchitectureContractRunner(CreateContext(_testingAssembly, _coreAssembly), document);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            runner.Session.CheckAssemblyAllowOnlyContract(contract))!;

        Assert.That(ex.Message, Does.Contain("dependency_depth: transitive"));
        Assert.That(ex.Message, Does.Contain("not supported yet"));
    }
}
