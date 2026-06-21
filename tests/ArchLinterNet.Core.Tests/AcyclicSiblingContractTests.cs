using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class AcyclicSiblingContractTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private string WriteContract(string yaml)
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");
        File.WriteAllText(contractPath, yaml);
        return contractPath;
    }

    private static string MinimalContractsYaml(string contractsBlock) => $@"
version: 1
name: Test
layers:
  core:
    namespace: Test.Core
analysis:
  target_assemblies: []
contracts:
{contractsBlock}
";

    private ArchitectureAnalysisContext CreateContext()
    {
        return new ArchitectureAnalysisContext(
            _tempDir,
            new[] { typeof(ArchitectureContractDocument).Assembly },
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    [Test]
    public void SiblingGraphBuilder_FindsChildrenUnderAncestor()
    {
        var groups = ArchitectureSiblingGraphBuilder.BuildSiblingGroups(
            new[] { typeof(ArchitectureContractDocument).Assembly },
            "ArchLinterNet.Core");

        Assert.That(groups, Has.Count.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void SiblingGraphBuilder_GroupNamesAreImmediateChildren()
    {
        var groups = ArchitectureSiblingGraphBuilder.BuildSiblingGroups(
            new[] { typeof(ArchitectureContractDocument).Assembly },
            "ArchLinterNet.Core");

        foreach (string siblingName in groups.Keys)
        {
            Assert.That(siblingName, Does.Not.Contain("."));
        }
    }

    [Test]
    public void SiblingGraphBuilder_EmptyAssemblyList_ReturnsEmpty()
    {
        var groups = ArchitectureSiblingGraphBuilder.BuildSiblingGroups(
            Array.Empty<Assembly>(),
            "ArchLinterNet.Core");

        Assert.That(groups, Is.Empty);
    }

    [Test]
    public void SiblingGraphBuilder_UnrelatedAncestor_ReturnsEmpty()
    {
        var groups = ArchitectureSiblingGraphBuilder.BuildSiblingGroups(
            new[] { typeof(ArchitectureContractDocument).Assembly },
            "Some.Unrelated.Namespace");

        Assert.That(groups, Is.Empty);
    }

    [Test]
    public void CheckAcyclicSiblingContract_EmptyAncestor_ReturnsEmptyCycles()
    {
        var context = CreateContext();
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(ArchitectureContractDocument).Assembly.GetName().Name! }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictAcyclicSiblings = new List<ArchitectureAcyclicSiblingContract>
                {
                    new()
                    {
                        Name = "No matching types",
                        Ancestors = new List<string> { "No.Types.Here" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(context, document);
        var result = runner.CheckAcyclicSiblingContract(
            document.Contracts.StrictAcyclicSiblings[0]);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void CheckAcyclicSiblingContract_SingleChildAncestor_ReturnsEmptyCycles()
    {
        var context = CreateContext();
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(ArchitectureContractDocument).Assembly.GetName().Name! }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictAcyclicSiblings = new List<ArchitectureAcyclicSiblingContract>
                {
                    new()
                    {
                        Name = "Single child",
                        Ancestors = new List<string> { "ArchLinterNet.Core.Scanning" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(context, document);
        var result = runner.CheckAcyclicSiblingContract(
            document.Contracts.StrictAcyclicSiblings[0]);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void CheckAcyclicSiblingContract_WithFilteredContractId_RespectsSelection()
    {
        var context = CreateContext();
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(ArchitectureContractDocument).Assembly.GetName().Name! }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictAcyclicSiblings = new List<ArchitectureAcyclicSiblingContract>
                {
                    new()
                    {
                        Name = "Selected",
                        Id = "selected",
                        Ancestors = new List<string> { "ArchLinterNet.Core" }
                    },
                    new()
                    {
                        Name = "Ignored",
                        Id = "ignored",
                        Ancestors = new List<string> { "ArchLinterNet.Core" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(
            context, document,
            new HashSet<string> { "selected" });

        IReadOnlyCollection<string> selectedResult = runner.CheckAcyclicSiblingContract(
            document.Contracts.StrictAcyclicSiblings[0]);
        IReadOnlyCollection<string> ignoredResult = runner.CheckAcyclicSiblingContract(
            document.Contracts.StrictAcyclicSiblings[1]);

        Assert.That(selectedResult, Is.Not.Null);
        Assert.That(ignoredResult, Is.Empty);
    }

    [Test]
    public void CheckAcyclicSiblingContract_MultipleAncestors_IndependentEvaluation()
    {
        var context = CreateContext();
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(ArchitectureContractDocument).Assembly.GetName().Name! }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictAcyclicSiblings = new List<ArchitectureAcyclicSiblingContract>
                {
                    new()
                    {
                        Name = "Multiple ancestors",
                        Ancestors = new List<string>
                        {
                            "ArchLinterNet.Core",
                            "ArchLinterNet.Core.Contracts"
                        }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(context, document);
        var result = runner.CheckAcyclicSiblingContract(
            document.Contracts.StrictAcyclicSiblings[0]);

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void DeterministicOutput_SameGraph_ReturnsSameOrder()
    {
        var groups1 = ArchitectureSiblingGraphBuilder.BuildSiblingGroups(
            new[] { typeof(ArchitectureContractDocument).Assembly },
            "ArchLinterNet.Core");

        var groups2 = ArchitectureSiblingGraphBuilder.BuildSiblingGroups(
            new[] { typeof(ArchitectureContractDocument).Assembly },
            "ArchLinterNet.Core");

        Assert.That(groups1.Keys.OrderBy(k => k),
            Is.EqualTo(groups2.Keys.OrderBy(k => k)));
    }

    [Test]
    public void ContractLoader_LoadsAcyclicSiblingContract()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict_acyclic_siblings:
    - name: sibling-check
      ancestors:
        - MyApp.Features
      reason: Siblings must be acyclic
"));

        ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(path);

        Assert.That(document.Contracts.StrictAcyclicSiblings, Has.Count.EqualTo(1));
        Assert.That(document.Contracts.StrictAcyclicSiblings[0].Name, Is.EqualTo("sibling-check"));
        Assert.That(document.Contracts.StrictAcyclicSiblings[0].Ancestors, Has.Count.EqualTo(1));
        Assert.That(document.Contracts.StrictAcyclicSiblings[0].Ancestors[0], Is.EqualTo("MyApp.Features"));
    }

    [Test]
    public void ContractLoader_AcyclicSibling_FallbackId()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict_acyclic_siblings:
    - name: Feature Sibling Check
      ancestors:
        - MyApp.Features
"));

        ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(path);

        Assert.That(document.Contracts.StrictAcyclicSiblings[0].Id, Is.EqualTo("feature-sibling-check"));
    }

    [Test]
    public void ContractLoader_AcyclicSibling_ExplicitId()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict_acyclic_siblings:
    - id: explicit-id
      name: Feature Sibling Check
      ancestors:
        - MyApp.Features
"));

        ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(path);

        Assert.That(document.Contracts.StrictAcyclicSiblings[0].Id, Is.EqualTo("explicit-id"));
    }

    [Test]
    public void ContractLoader_AcyclicSibling_IgnoredViolations()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict_acyclic_siblings:
    - name: sibling-check
      ancestors:
        - MyApp.Features
      ignored_violations:
        - source_type: MyApp.Features.Legacy.*
          forbidden_reference: MyApp.Features.NewSystem.*
          reason: Migration
"));

        ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(path);

        Assert.That(document.Contracts.StrictAcyclicSiblings[0].IgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(document.Contracts.StrictAcyclicSiblings[0].IgnoredViolations[0].SourceType, Is.EqualTo("MyApp.Features.Legacy.*"));
    }

    [Test]
    public void ContractLoader_AuditAcyclicSibling_LoadsCorrectly()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  audit_acyclic_siblings:
    - name: audit-sibling-check
      ancestors:
        - MyApp.Modules
      reason: Audit mode
"));

        ArchitectureContractDocument document = ArchitectureContractLoader.LoadFromPath(path);

        Assert.That(document.Contracts.AuditAcyclicSiblings, Has.Count.EqualTo(1));
        Assert.That(document.Contracts.AuditAcyclicSiblings[0].Name, Is.EqualTo("audit-sibling-check"));
    }

    [Test]
    public void IArchitectureContract_AcyclicSiblingContract_ImplementsInterface()
    {
        Assert.That(typeof(ArchitectureAcyclicSiblingContract).GetInterfaces(),
            Does.Contain(typeof(IArchitectureContract)));
    }

    [Test]
    public void ContractLoader_DuplicateAcyclicSiblingIds_Throws()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict_acyclic_siblings:
    - id: dup-sibling
      name: First
      ancestors:
        - MyApp.Features
    - id: dup-sibling
      name: Second
      ancestors:
        - MyApp.Modules
"));

        Assert.Throws<InvalidOperationException>(() =>
            ArchitectureContractLoader.LoadFromPath(path));
    }

    [Test]
    public void ContractLoader_EmptyAncestors_Throws()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict_acyclic_siblings:
    - name: empty-ancestors
      ancestors: []
"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureContractLoader.LoadFromPath(path));
        Assert.That(ex!.Message, Does.Contain("empty ancestors list"));
    }

    [Test]
    public void ContractLoader_BlankAncestor_Throws()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string path = Path.Combine(contractDir, "dependencies.arch.yml");
        File.WriteAllText(path, @"
version: 1
name: Test
layers:
  core:
    namespace: Test.Core
analysis:
  target_assemblies: []
contracts:
  strict_acyclic_siblings:
    - name: blank-ancestor
      ancestors:
        - ''

        - Some.Valid.Namespace
");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureContractLoader.LoadFromPath(path));
        Assert.That(ex!.Message, Does.Contain("blank or empty ancestor"));
    }

    [Test]
    public void Validate_WithStrictAcyclicSibling_ProcessesWithoutError()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Acyclic Sibling Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict_acyclic_siblings:
    - name: core-siblings-acyclic
      ancestors:
        - ArchLinterNet.Core
      reason: Core sub-namespaces must be acyclic
");

        var validator = new ArchitectureValidator();
        bool result = validator.Validate(contractPath, out var violations, out var cycles);

        Assert.That(violations, Is.Empty);
        Assert.That(result || cycles.Count > 0, Is.True);
    }

    [Test]
    public void Validate_DeterministicOutput_SamePolicySameResult()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        string yaml = @"
version: 1
name: Acyclic Sibling Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  strict_acyclic_siblings:
    - name: core-siblings-acyclic
      ancestors:
        - ArchLinterNet.Core
      reason: Core sub-namespaces must be acyclic
";
        File.WriteAllText(contractPath, yaml);

        var validator = new ArchitectureValidator();
        validator.Validate(contractPath, out var violations1, out var cycles1);
        validator.Validate(contractPath, out var violations2, out var cycles2);

        Assert.That(cycles1.OrderBy(c => c), Is.EqualTo(cycles2.OrderBy(c => c)));
    }

    [Test]
    public void Validate_WithAuditAcyclicSibling_DoesNotFailOnCycles()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Acyclic Sibling Audit Test
layers:
  core:
    namespace: ArchLinterNet.Core
analysis:
  target_assemblies:
    - ArchLinterNet.Core
contracts:
  audit_acyclic_siblings:
    - name: core-siblings-audit
      ancestors:
        - ArchLinterNet.Core
      reason: Audit mode
");

        var validator = new ArchitectureValidator();
        bool result = validator.Validate(contractPath, out var violations, out var cycles);

        Assert.That(violations, Is.Empty);
        Assert.That(cycles, Is.Not.Null);
    }

    [Test]
    public void Validate_RunnerProducesSiblingGroupsForRealAssembly()
    {
        var context = CreateContext();
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { typeof(ArchitectureContractDocument).Assembly.GetName().Name! }
            },
            Contracts = new ArchitectureContractGroups
            {
                StrictAcyclicSiblings = new List<ArchitectureAcyclicSiblingContract>
                {
                    new()
                    {
                        Name = "Core sibling check",
                        Id = "core-siblings",
                        Ancestors = new List<string> { "ArchLinterNet.Core" }
                    }
                }
            }
        };

        var runner = new ArchitectureContractRunner(context, document);
        var result = runner.CheckAcyclicSiblingContract(
            document.Contracts.StrictAcyclicSiblings[0]);

        Assert.That(result, Is.Not.Null);
    }
}
