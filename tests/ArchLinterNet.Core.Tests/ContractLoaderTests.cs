using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ContractLoaderTests
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

    [Test]
    public void LoadFromPath_ValidYaml_ReturnsDocument()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Test Contract
layers:
  core:
    namespace: Test.Core
  web:
    namespace: Test.Web
analysis:
  target_assemblies:
    - Test.Core
    - Test.Web
contracts:
  strict: []
  audit: []
  strict_layers: []
  audit_layers: []
  strict_allow_only: []
  audit_allow_only: []
  strict_cycles: []
  audit_cycles: []
  strict_method_body: []
  audit_method_body: []
  strict_asmdef: []
  audit_asmdef: []
  strict_independence: []
  audit_independence: []
");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(contractPath);

        Assert.That(document.Version, Is.EqualTo(1));
        Assert.That(document.Name, Is.EqualTo("Test Contract"));
        Assert.That(document.Layers, Has.Count.EqualTo(2));
        Assert.That(document.Layers["core"].Namespace, Is.EqualTo("Test.Core"));
        Assert.That(document.Analysis.TargetAssemblies, Has.Count.EqualTo(2));
        Assert.That(document.Classification.Attributes, Is.Empty);
        Assert.That(document.Classification.AssemblyAttributes, Is.Empty);
    }

    [Test]
    public void LoadFromPath_ClassificationSection_BindsAttributeAndAssemblyAttributeMappings()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Test Contract
layers:
  core:
    namespace: Test.Core
analysis:
  target_assemblies:
    - Test.Core
classification:
  attributes:
    - attribute: Acme.Architecture.DomainLayerAttribute
      role: DomainLayer
      metadata:
        domain: constructor[0]
        module: property:Module
        tier: const:Acme.Architecture.Tiers.CORE
        owner: platform-team
  assembly_attributes:
    - attribute: Acme.Architecture.BoundedContextAttribute
      role: ApplicationLayer
      metadata:
        boundedContext: constructor[0]
contracts:
  strict: []
  audit: []
  strict_layers: []
  audit_layers: []
  strict_allow_only: []
  audit_allow_only: []
  strict_cycles: []
  audit_cycles: []
  strict_method_body: []
  audit_method_body: []
  strict_asmdef: []
  audit_asmdef: []
  strict_independence: []
  audit_independence: []
");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(contractPath);

        Assert.That(document.Classification.Attributes, Has.Count.EqualTo(1));
        ArchitectureAttributeClassificationMapping attributeMapping = document.Classification.Attributes[0];
        Assert.That(attributeMapping.Attribute, Is.EqualTo("Acme.Architecture.DomainLayerAttribute"));
        Assert.That(attributeMapping.Role, Is.EqualTo("DomainLayer"));
        Assert.That(attributeMapping.Metadata["domain"], Is.EqualTo("constructor[0]"));
        Assert.That(attributeMapping.Metadata["module"], Is.EqualTo("property:Module"));
        Assert.That(attributeMapping.Metadata["tier"], Is.EqualTo("const:Acme.Architecture.Tiers.CORE"));
        Assert.That(attributeMapping.Metadata["owner"], Is.EqualTo("platform-team"));

        Assert.That(document.Classification.AssemblyAttributes, Has.Count.EqualTo(1));
        ArchitectureAttributeClassificationMapping assemblyMapping = document.Classification.AssemblyAttributes[0];
        Assert.That(assemblyMapping.Attribute, Is.EqualTo("Acme.Architecture.BoundedContextAttribute"));
        Assert.That(assemblyMapping.Role, Is.EqualTo("ApplicationLayer"));
        Assert.That(assemblyMapping.Metadata["boundedContext"], Is.EqualTo("constructor[0]"));
    }

    [Test]
    public void LoadFromPath_LiteralMetadataScalars_PreserveTheirYamlType()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Test Contract
layers:
  core:
    namespace: Test.Core
analysis:
  target_assemblies:
    - Test.Core
classification:
  attributes:
    - attribute: Acme.Architecture.DomainLayerAttribute
      role: DomainLayer
      metadata:
        enabled: true
        priority: 1
        ratio: 1.5
        owner: platform-team
        quotedNumber: ""42""
contracts:
  strict: []
  audit: []
  strict_layers: []
  audit_layers: []
  strict_allow_only: []
  audit_allow_only: []
  strict_cycles: []
  audit_cycles: []
  strict_method_body: []
  audit_method_body: []
  strict_asmdef: []
  audit_asmdef: []
  strict_independence: []
  audit_independence: []
");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(contractPath);
        Dictionary<string, object> metadata = document.Classification.Attributes[0].Metadata;

        Assert.That(metadata["enabled"], Is.EqualTo(true));
        Assert.That(metadata["priority"], Is.EqualTo(1L));
        Assert.That(metadata["ratio"], Is.EqualTo(1.5));
        Assert.That(metadata["owner"], Is.EqualTo("platform-team"));
        Assert.That(metadata["quotedNumber"], Is.EqualTo("42"));
    }

    [Test]
    public void LoadFromPath_MissingFile_ThrowsFileNotFoundException()
    {
        string missingPath = Path.Combine(_tempDir, "nonexistent.yml");

        Assert.Throws<FileNotFoundException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(missingPath));
    }

    [Test]
    public void LoadFromPath_MinimalYaml_ParsesCorrectly()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Minimal
layers: {}
analysis:
  target_assemblies: []
contracts:
  strict: []
  audit: []
  strict_layers: []
  audit_layers: []
  strict_allow_only: []
  audit_allow_only: []
  strict_cycles: []
  audit_cycles: []
  strict_method_body: []
  audit_method_body: []
  strict_asmdef: []
  audit_asmdef: []
  strict_independence: []
  audit_independence: []
");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(contractPath);

        Assert.That(document.Version, Is.EqualTo(1));
        Assert.That(document.Layers, Is.Empty);
        Assert.That(document.Contracts.Strict, Is.Empty);
    }

    [TestCase("Test.Domain.?.Models")]
    [TestCase("Test..Domain")]
    [TestCase(".Test.Domain")]
    [TestCase("Test.Domain.")]
    [TestCase("Test.Domain.[x]")]
    public void LoadFromPath_InvalidLayerNamespace_ThrowsInvalidNamespacePatternException(string layerNamespace)
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, $@"
version: 1
name: Invalid Namespace Contract
layers:
  core:
    namespace: {layerNamespace}
analysis:
  target_assemblies: []
contracts:
  strict: []
  audit: []
  strict_layers: []
  audit_layers: []
  strict_allow_only: []
  audit_allow_only: []
  strict_cycles: []
  audit_cycles: []
  strict_method_body: []
  audit_method_body: []
  strict_asmdef: []
  audit_asmdef: []
  strict_independence: []
  audit_independence: []
");

        Assert.Throws<InvalidNamespacePatternException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath));
    }

    // Regression coverage for the semantic-classification-model design
    // (openspec/changes/archive/2026-07-10-design-semantic-classification-model): the reviewed
    // design requires that a policy declaring the reserved `classification` section or
    // `layers.<name>.selector` field loads exactly as if the field were absent, since no C#
    // binding exists yet and the loader's IgnoreUnmatchedProperties() drops unrecognized keys.
    // This asserts that behavior against the real loader, not just the JSON schema.
    [Test]
    public void LoadFromPath_ReservedClassificationSectionAndSelectorField_LoadsWithoutThrowing()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, @"
version: 1
name: Reserved Classification Fields
classification:
  attributes:
    - attribute: Acme.DomainLayerAttribute
      role: DomainLayer
  overrides:
    - namespace: Test.Legacy
      role: Unclassified
      reason: Predates attribute adoption.
layers:
  core:
    namespace: Test.Core
    selector:
      role: DomainLayer
      metadata:
        domain: Sales
analysis:
  target_assemblies:
    - Test.Core
contracts:
  strict: []
  audit: []
  strict_layers: []
  audit_layers: []
  strict_allow_only: []
  audit_allow_only: []
  strict_cycles: []
  audit_cycles: []
  strict_method_body: []
  audit_method_body: []
  strict_asmdef: []
  audit_asmdef: []
  strict_independence: []
  audit_independence: []
");

        ArchitectureContractDocument? document = null;

        Assert.DoesNotThrow(() => document = new ArchitecturePolicyDocumentLoader().Load(contractPath));
        Assert.That(document!.Version, Is.EqualTo(1));
        Assert.That(document.Layers["core"].Namespace, Is.EqualTo("Test.Core"));
    }

    [Test]
    public void LoadFromPath_DuplicateIdsAndUnrelatedFamilyInvalid_ThrowsDuplicateIdErrorFirst()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        string assemblyName = typeof(ContractLoaderTests).Assembly.GetName().Name!;

        File.WriteAllText(contractPath, $"""
            version: 1
            name: Pipeline Order Test
            analysis:
              target_assemblies: [{assemblyName}]
            contracts:
              strict_assembly_independence:
                - name: assembly-independence-one
                  id: dup-id
                  assemblies: [{assemblyName}]
                  reason: First contract.
                - name: assembly-independence-two
                  id: dup-id
                  assemblies: [{assemblyName}]
                  reason: Second contract.
              strict_attribute_usage:
                - name: no-attributes
                  allowed_only_in_layers: [api]
                  reason: Missing attributes/attribute_prefixes.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("Duplicate contract IDs found"));
        Assert.That(ex.Message, Does.Not.Contain("attributes' or 'attribute_prefixes'"));
    }
}
