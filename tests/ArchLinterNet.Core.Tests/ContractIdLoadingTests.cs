using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ContractIdLoadingTests
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
  runtime:
    namespace: Test.Runtime
analysis:
  target_assemblies: []
contracts:
{contractsBlock}
";

    [Test]
    public void ExplicitId_DeserializedCorrectly()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict:
    - id: my-rule
      name: My Rule
      source: core
      forbidden: [runtime]
"));

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        Assert.That(document.Contracts.Strict, Has.Count.EqualTo(1));
        Assert.That(document.Contracts.Strict[0].Id, Is.EqualTo("my-rule"));
    }

    [Test]
    public void OmittedId_GetsFallbackNormalizedId()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict:
    - name: My Rule
      source: core
      forbidden: [runtime]
"));

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        Assert.That(document.Contracts.Strict[0].Id, Is.Not.Null);
        Assert.That(document.Contracts.Strict[0].Id, Is.EqualTo("my-rule"));
    }

    [Test]
    public void FallbackId_NormalizesNameCorrectly()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict:
    - name: Map.Core must not depend on Runtime
      source: core
      forbidden: [runtime]
"));

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        Assert.That(document.Contracts.Strict[0].Id, Is.EqualTo("map-core-must-not-depend-on-runtime"));
    }

    [Test]
    public void IArchitectureContract_AllContractTypesImplementInterface()
    {
        Assert.That(typeof(ArchitectureDependencyContract).GetInterfaces(), Does.Contain(typeof(IArchitectureContract)));
        Assert.That(typeof(ArchitectureLayerContract).GetInterfaces(), Does.Contain(typeof(IArchitectureContract)));
        Assert.That(typeof(ArchitectureAllowOnlyContract).GetInterfaces(), Does.Contain(typeof(IArchitectureContract)));
        Assert.That(typeof(ArchitectureCycleContract).GetInterfaces(), Does.Contain(typeof(IArchitectureContract)));
        Assert.That(typeof(ArchitectureMethodBodyContract).GetInterfaces(), Does.Contain(typeof(IArchitectureContract)));
        Assert.That(typeof(ArchitectureAsmdefContract).GetInterfaces(), Does.Contain(typeof(IArchitectureContract)));
        Assert.That(typeof(ArchitectureIndependenceContract).GetInterfaces(), Does.Contain(typeof(IArchitectureContract)));
        Assert.That(typeof(ArchitectureAcyclicSiblingContract).GetInterfaces(), Does.Contain(typeof(IArchitectureContract)));
    }

    [Test]
    public void DuplicateIdsInSameGroup_ThrowsInvalidOperationException()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict:
    - id: dup-rule
      name: Rule One
      source: core
      forbidden: [runtime]
    - id: dup-rule
      name: Rule Two
      source: runtime
      forbidden: [core]
"));

        Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(path));
    }

    [Test]
    public void SameIdAcrossDifferentContractTypes_Allowed()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict:
    - id: boundary
      name: Dep Contract
      source: core
      forbidden: [runtime]
  strict_cycles:
    - id: boundary
      name: Cycle Contract
      layers: [core, runtime]
"));

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        Assert.That(document.Contracts.Strict[0].Id, Is.EqualTo("boundary"));
        Assert.That(document.Contracts.StrictCycles[0].Id, Is.EqualTo("boundary"));
    }

    [Test]
    public void SameIdAcrossStrictAndAudit_Allowed()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict:
    - id: common-rule
      name: Strict Rule
      source: core
      forbidden: [runtime]
  audit:
    - id: common-rule
      name: Audit Rule
      source: runtime
      forbidden: [core]
"));

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        Assert.That(document.Contracts.Strict[0].Id, Is.EqualTo("common-rule"));
        Assert.That(document.Contracts.Audit[0].Id, Is.EqualTo("common-rule"));
    }

    [Test]
    public void CaseOnlyDuplicateIdsInSameGroup_ThrowsInvalidOperationException()
    {
        string path = WriteContract(MinimalContractsYaml(@"
  strict:
    - id: dup-rule
      name: Rule One
      source: core
      forbidden: [runtime]
    - id: DUP-RULE
      name: Rule Two
      source: runtime
      forbidden: [core]
"));

        Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(path));
    }

    [Test]
    public void NormalizeToContractId_HandlesSpecialCharacters()
    {
        Assert.That(ArchitecturePolicyDocumentLoader.NormalizeToContractId("Assembly A -> Assembly B"), Is.EqualTo("assembly-a-to-assembly-b"));
        Assert.That(ArchitecturePolicyDocumentLoader.NormalizeToContractId("  Multiple   Spaces  "), Is.EqualTo("multiple-spaces"));
        Assert.That(ArchitecturePolicyDocumentLoader.NormalizeToContractId("Special!@#Characters"), Is.EqualTo("special-characters"));
    }
}
