using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ContractLoaderSelectorTests
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

    [TestCase("        domains: [Sales, Billing]", "domains")]
    [TestCase("        domains:\n          - Sales\n          - Billing", "domains")]
    [TestCase("        domains:\n          primary: Sales", "domains")]
    [TestCase("        domains: null", "domains")]
    [TestCase("        domains: 1e300", "domains")]
    public void LoadFromPath_SelectorMetadataNonScalar_ThrowsDeterministicValidationError(string metadataYaml, string key)
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, $"""
version: 1
name: Invalid Selector Metadata
layers:
  semantic:
    selector:
      role: DomainLayer
      metadata:
{metadataYaml}
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
""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("Layer 'semantic'"));
        Assert.That(ex.Message, Does.Contain($"selector metadata key '{key}'"));
        Assert.That(ex.Message, Does.Contain("string, boolean, or finite numeric scalar"));
    }

    [Test]
    public void LoadFromPath_SelectorExplicitNull_ThrowsDeterministicValidationError()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Null Selector
layers:
  semantic:
    namespace: MyApp.Domain
    selector: null
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
""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("Layer 'semantic' selector must be an object"));
    }

    [Test]
    public void LoadFromPath_SelectorMetadataExplicitNull_ThrowsDeterministicValidationError()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Null Selector Metadata
layers:
  semantic:
    selector:
      role: DomainLayer
      metadata: null
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
""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("Layer 'semantic' selector metadata must be an object"));
    }

    [Test]
    public void LoadFromPath_SelectorOnlyLayerWithNamespaceSuffix_ThrowsDeterministicValidationError()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Selector Only With Suffix
layers:
  semantic:
    namespace_suffix: Generated
    selector:
      role: DomainLayer
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
""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("Layer 'semantic' namespace_suffix requires a non-empty namespace"));
    }

    [Test]
    public void LoadFromPath_SelectorMetadataEmptyString_ThrowsDeterministicValidationError()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Empty String Selector Metadata
layers:
  semantic:
    selector:
      role: DomainLayer
      metadata:
        domain: ""
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
""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("selector metadata key 'domain' must not be an empty string"));
    }

    [Test]
    public void LoadFromPath_SelectorUnknownProperty_ThrowsDeterministicValidationError()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Unknown Selector Property
layers:
  semantic:
    selector:
      role: DomainLayer
      metdata:
        domain: Sales
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
""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("Layer 'semantic' selector contains unknown property 'metdata'"));
    }

    [Test]
    public void LoadFromPath_QuotedNullNamespaceWithSuffix_LoadsWithoutThrowing()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Quoted Null Namespace
layers:
  core:
    namespace: "Null"
    namespace_suffix: Generated
analysis:
  target_assemblies:
    - Null.Generated
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
""");

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(contractPath));
    }

    [Test]
    public void LoadFromPath_NullNamespaceWithSelector_ThrowsDeterministicValidationError()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Null Namespace With Selector
layers:
  semantic:
    namespace: null
    selector:
      role: DomainLayer
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
""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("Layer 'semantic' namespace must be a non-empty string"));
    }

    [Test]
    public void LoadFromPath_EmptyNamespaceWithSelector_ThrowsDeterministicValidationError()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Empty Namespace With Selector
layers:
  semantic:
    namespace: ""
    selector:
      role: DomainLayer
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
""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("Layer 'semantic' namespace must be a non-empty string"));
    }

    [Test]
    public void LoadFromPath_LayerUnknownProperty_ThrowsDeterministicValidationError()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Unknown Layer Property
layers:
  core:
    namespace: Test.Core
    selecter:
      role: DomainLayer
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
""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("Layer 'core' contains unknown property 'selecter'"));
    }

    [Test]
    public void LoadFromPath_LayerExclude_LoadsSuccessfully()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Layer Exclude
layers:
  core:
    namespace: Test.Core.*
    exclude:
      - namespace: Test.Core.*.Generated
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
""");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(contractPath);

        Assert.That(document.Layers["core"].Exclude, Has.Count.EqualTo(1));
        Assert.That(document.Layers["core"].Exclude[0].Namespace, Is.EqualTo("Test.Core.*.Generated"));
    }

    [Test]
    public void LoadFromPath_LayerExcludeUnknownProperty_ThrowsDeterministicValidationError()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Layer Exclude Unknown Property
layers:
  core:
    namespace: Test.Core.*
    exclude:
      - namespace: Test.Core.*.Generated
        role: DomainLayer
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
""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("Layer 'core' exclude entry contains unknown property 'role'"));
    }

    [Test]
    public void LoadFromPath_LayerExcludeMissingNamespace_ThrowsDeterministicValidationError()
    {
        string contractDir = Path.Combine(_tempDir, "architecture");
        Directory.CreateDirectory(contractDir);
        string contractPath = Path.Combine(contractDir, "dependencies.arch.yml");

        File.WriteAllText(contractPath, """
version: 1
name: Layer Exclude Missing Namespace
layers:
  core:
    namespace: Test.Core.*
    exclude:
      - namespace_suffix: Generated
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
""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(contractPath))!;

        Assert.That(ex.Message, Does.Contain("Layer 'core' exclude entry must declare 'namespace'"));
    }
}
