using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed partial class AcyclicSiblingContractTests
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

    private static Assembly FixtureAssembly => typeof(AcyclicSiblingFixtures.TwoNode.Auth.AuthService).Assembly;

    private static string FixtureAssemblyName => FixtureAssembly.GetName().Name!;

    private (ArchitectureContractRunner runner, ArchitectureAcyclicSiblingContract contract) CreateRunnerWithContract(
        List<string> ancestors,
        string name = "test",
        string? id = null,
        List<ArchitectureIgnoredViolation>? ignoredViolations = null,
        bool isAudit = false)
    {
        var context = new ArchitectureAnalysisContext(
            _tempDir,
            new[] { FixtureAssembly },
            Array.Empty<string>(),
            Array.Empty<string>());

        var contract = new ArchitectureAcyclicSiblingContract
        {
            Name = name,
            Id = id ?? name.Replace(' ', '-'),
            Ancestors = ancestors,
            IgnoredViolations = ignoredViolations ?? new List<ArchitectureIgnoredViolation>()
        };

        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "Test",
            Layers = new Dictionary<string, ArchitectureLayer>(),
            Analysis = new ArchitectureAnalysisConfiguration
            {
                TargetAssemblies = new List<string> { FixtureAssemblyName }
            },
            Contracts = new ArchitectureContractGroups()
        };

        if (isAudit)
        {
            document.Contracts.AuditAcyclicSiblings = new List<ArchitectureAcyclicSiblingContract> { contract };
        }
        else
        {
            document.Contracts.StrictAcyclicSiblings = new List<ArchitectureAcyclicSiblingContract> { contract };
        }

        var runner = new ArchitectureContractRunner(context, document);
        return (runner, contract);
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

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

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

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

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

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

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

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

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

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

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
            new ArchitecturePolicyDocumentLoader().Load(path));
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
            new ArchitecturePolicyDocumentLoader().Load(path));
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
            new ArchitecturePolicyDocumentLoader().Load(path));
        Assert.That(ex!.Message, Does.Contain("blank or empty ancestor"));
    }

    private const string TwoNodeAncestor = "AcyclicSiblingFixtures.TwoNode";
    private const string ThreeNodeAncestor = "AcyclicSiblingFixtures.ThreeNode";
    private const string DescendantAncestor = "AcyclicSiblingFixtures.Descendant.Desc";
    private const string MultiAncestorA = "AcyclicSiblingFixtures.MultiAncestor.ModuleA";
    private const string MultiAncestorB = "AcyclicSiblingFixtures.MultiAncestor.ModuleB";
    private const string CleanAncestor = "AcyclicSiblingFixtures.Clean";

    [Test]
    public void TwoNodeCycle_ProducesExactCycleDiagnostic()
    {
        var (runner, contract) = CreateRunnerWithContract(new List<string> { TwoNodeAncestor });

        IReadOnlyCollection<string> cycles = runner.CheckAcyclicSiblingContract(contract);

        Assert.That(cycles, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.TwoNode: Auth -> Payments -> Auth"));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.TwoNode: Payments -> Auth -> Payments"));
    }

    [Test]
    public void ThreeNodeCycle_ProducesExactCycleDiagnostic()
    {
        var (runner, contract) = CreateRunnerWithContract(new List<string> { ThreeNodeAncestor });

        IReadOnlyCollection<string> cycles = runner.CheckAcyclicSiblingContract(contract);

        Assert.That(cycles, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.ThreeNode: Auth -> Payments -> Billing -> Auth"));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.ThreeNode: Payments -> Billing -> Auth -> Payments"));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.ThreeNode: Billing -> Auth -> Payments -> Billing"));
    }

    [Test]
    public void DescendantAttribution_CycleReportedAsDirectSibling()
    {
        var (runner, contract) = CreateRunnerWithContract(new List<string> { DescendantAncestor });

        IReadOnlyCollection<string> cycles = runner.CheckAcyclicSiblingContract(contract);

        Assert.That(cycles, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.Descendant.Desc: Controllers -> Core -> Controllers"));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.Descendant.Desc: Core -> Controllers -> Core"));
    }

    [Test]
    public void MultipleAncestors_EachGetsIndependentCycleReport()
    {
        var (runner, contract) = CreateRunnerWithContract(new List<string>
        {
            MultiAncestorA,
            MultiAncestorB
        });

        IReadOnlyCollection<string> cycles = runner.CheckAcyclicSiblingContract(contract);

        Assert.That(cycles, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.MultiAncestor.ModuleA: Alpha -> Beta -> Alpha"));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.MultiAncestor.ModuleA: Beta -> Alpha -> Beta"));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.MultiAncestor.ModuleB: Gamma -> Delta -> Gamma"));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.MultiAncestor.ModuleB: Delta -> Gamma -> Delta"));
    }

    [Test]
    public void CleanSiblingGraph_NoCyclesReported()
    {
        var (runner, contract) = CreateRunnerWithContract(new List<string> { CleanAncestor });

        IReadOnlyCollection<string> cycles = runner.CheckAcyclicSiblingContract(contract);

        Assert.That(cycles, Is.Empty);
    }

    [Test]
    public void IgnoredViolations_BreakKnownCycle()
    {
        var ignored = new List<ArchitectureIgnoredViolation>
        {
            new()
            {
                SourceType = "AcyclicSiblingFixtures.TwoNode.Auth.AuthService",
                ForbiddenReference = "AcyclicSiblingFixtures.TwoNode.Payments.PaymentService",
                Reason = "breaking the cycle"
            }
        };
        var (runner, contract) = CreateRunnerWithContract(
            new List<string> { TwoNodeAncestor },
            ignoredViolations: ignored);

        IReadOnlyCollection<string> cycles = runner.CheckAcyclicSiblingContract(contract);

        Assert.That(cycles, Is.Empty);
    }

    [Test]
    public void StrictValidation_ReturnsFalseOnTwoNodeCycle()
    {
        string yaml = $@"
version: 1
name: Acyclic Sibling Strict Test
layers: {{}}
analysis:
  target_assemblies:
    - {FixtureAssemblyName}
contracts:
  strict_acyclic_siblings:
    - name: strict-two-node
      ancestors:
        - {TwoNodeAncestor}
";
        string path = WriteContract(yaml);

        bool result = ArchitectureValidator.Validate(path, out var violations, out var cycles);

        Assert.That(violations, Is.Empty);
        Assert.That(cycles, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(cycles, Does.Contain(
            "[strict-two-node] AcyclicSiblingFixtures.TwoNode: Auth -> Payments -> Auth"));
        Assert.That(result, Is.False);
    }

    [Test]
    public void AuditContract_ReportsCycles()
    {
        var (runner, contract) = CreateRunnerWithContract(
            new List<string> { TwoNodeAncestor },
            isAudit: true);

        IReadOnlyCollection<string> cycles = runner.CheckAcyclicSiblingContract(contract);

        Assert.That(cycles, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(cycles, Does.Contain(
            "AcyclicSiblingFixtures.TwoNode: Auth -> Payments -> Auth"));
    }

    [Test]
    public void DeterministicOutput_ExactOrderedCycleString()
    {
        var (runner1, contract1) = CreateRunnerWithContract(new List<string> { TwoNodeAncestor });
        var (runner2, contract2) = CreateRunnerWithContract(new List<string> { TwoNodeAncestor });

        IReadOnlyCollection<string> cycles1 = runner1.CheckAcyclicSiblingContract(contract1);
        IReadOnlyCollection<string> cycles2 = runner2.CheckAcyclicSiblingContract(contract2);

        Assert.That(cycles1, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(cycles1, Does.Contain(
            "AcyclicSiblingFixtures.TwoNode: Auth -> Payments -> Auth"));
        Assert.That(cycles2, Is.EqualTo(cycles1));
    }

    [Test]
    public void ContractId_IncludedInValidatorOutput()
    {
        string contractId = "acyclic-two-node";
        string yaml = $@"
version: 1
name: Contract Id Test
layers: {{}}
analysis:
  target_assemblies:
    - {FixtureAssemblyName}
contracts:
  strict_acyclic_siblings:
    - id: {contractId}
      name: two-node-with-id
      ancestors:
        - {TwoNodeAncestor}
";
        string path = WriteContract(yaml);

        ArchitectureValidator.Validate(path, out var violations, out var cycles);

        Assert.That(violations, Is.Empty);
        Assert.That(cycles, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(cycles, Does.Contain(
            $"[{contractId}] AcyclicSiblingFixtures.TwoNode: Auth -> Payments -> Auth"));
    }
}
