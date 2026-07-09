using ArchLinterNet.Core.Contracts;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Regression coverage for issue #216: ArchitectureContractGroups was split from a single 723-line
// mega DTO into one C# partial-class file per contract family (Contracts/Families/*.cs). This test
// loads a YAML document declaring every registered family's strict/audit keys and asserts each one
// still binds to the correct property - the one thing a copy-paste alias mistake across 25 new
// files could silently break without a test noticing.
[TestFixture]
public sealed class AllContractFamiliesYamlRoundTripTests
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

    // strict/audit are populated (dependency and layer have no dedicated content validator beyond
    // duplicate-ID checking); every other family is declared empty so every validator in the
    // pipeline is trivially satisfied while every alias still round-trips through deserialization.
    private const string AllFamiliesYaml = @"
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
  strict:
    - name: dep rule
      source: core
      forbidden: [runtime]
  audit:
    - name: dep audit rule
      source: core
      forbidden: [runtime]
  strict_layers:
    - name: layer rule
      layers: [core, runtime]
  audit_layers:
    - name: layer audit rule
      layers: [core, runtime]
  strict_layer_templates: []
  audit_layer_templates: []
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
  strict_assembly_independence: []
  audit_assembly_independence: []
  strict_assembly_dependency: []
  audit_assembly_dependency: []
  strict_assembly_allow_only: []
  audit_assembly_allow_only: []
  strict_package_dependency: []
  audit_package_dependency: []
  strict_package_allow_only: []
  audit_package_allow_only: []
  strict_project_metadata: []
  audit_project_metadata: []
  strict_protected: []
  audit_protected: []
  strict_external: []
  audit_external: []
  strict_external_allow_only: []
  audit_external_allow_only: []
  strict_acyclic_siblings: []
  audit_acyclic_siblings: []
  strict_type_placement: []
  audit_type_placement: []
  strict_public_api_surface: []
  audit_public_api_surface: []
  strict_attribute_usage: []
  audit_attribute_usage: []
  strict_inheritance: []
  audit_inheritance: []
  strict_interface_implementation: []
  audit_interface_implementation: []
  strict_composition: []
  audit_composition: []
  strict_coverage: []
  audit_coverage: []
";

    [Test]
    public void AllRegisteredFamilyAliases_BindToTheirPartialClassProperty()
    {
        string path = WriteContract(AllFamiliesYaml);

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);
        ArchitectureContractGroups contracts = document.Contracts;

        Assert.That(contracts.Strict, Has.Count.EqualTo(1));
        Assert.That(contracts.Audit, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictLayers, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditLayers, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictLayerTemplates, Is.Empty);
        Assert.That(contracts.AuditLayerTemplates, Is.Empty);
        Assert.That(contracts.StrictAllowOnly, Is.Empty);
        Assert.That(contracts.AuditAllowOnly, Is.Empty);
        Assert.That(contracts.StrictCycles, Is.Empty);
        Assert.That(contracts.AuditCycles, Is.Empty);
        Assert.That(contracts.StrictMethodBody, Is.Empty);
        Assert.That(contracts.AuditMethodBody, Is.Empty);
        Assert.That(contracts.StrictAsmdef, Is.Empty);
        Assert.That(contracts.AuditAsmdef, Is.Empty);
        Assert.That(contracts.StrictIndependence, Is.Empty);
        Assert.That(contracts.AuditIndependence, Is.Empty);
        Assert.That(contracts.StrictAssemblyIndependence, Is.Empty);
        Assert.That(contracts.AuditAssemblyIndependence, Is.Empty);
        Assert.That(contracts.StrictAssemblyDependency, Is.Empty);
        Assert.That(contracts.AuditAssemblyDependency, Is.Empty);
        Assert.That(contracts.StrictAssemblyAllowOnly, Is.Empty);
        Assert.That(contracts.AuditAssemblyAllowOnly, Is.Empty);
        Assert.That(contracts.StrictPackageDependency, Is.Empty);
        Assert.That(contracts.AuditPackageDependency, Is.Empty);
        Assert.That(contracts.StrictPackageAllowOnly, Is.Empty);
        Assert.That(contracts.AuditPackageAllowOnly, Is.Empty);
        Assert.That(contracts.StrictProjectMetadata, Is.Empty);
        Assert.That(contracts.AuditProjectMetadata, Is.Empty);
        Assert.That(contracts.StrictProtected, Is.Empty);
        Assert.That(contracts.AuditProtected, Is.Empty);
        Assert.That(contracts.StrictExternal, Is.Empty);
        Assert.That(contracts.AuditExternal, Is.Empty);
        Assert.That(contracts.StrictExternalAllowOnly, Is.Empty);
        Assert.That(contracts.AuditExternalAllowOnly, Is.Empty);
        Assert.That(contracts.StrictAcyclicSiblings, Is.Empty);
        Assert.That(contracts.AuditAcyclicSiblings, Is.Empty);
        Assert.That(contracts.StrictTypePlacement, Is.Empty);
        Assert.That(contracts.AuditTypePlacement, Is.Empty);
        Assert.That(contracts.StrictPublicApiSurface, Is.Empty);
        Assert.That(contracts.AuditPublicApiSurface, Is.Empty);
        Assert.That(contracts.StrictAttributeUsage, Is.Empty);
        Assert.That(contracts.AuditAttributeUsage, Is.Empty);
        Assert.That(contracts.StrictInheritance, Is.Empty);
        Assert.That(contracts.AuditInheritance, Is.Empty);
        Assert.That(contracts.StrictInterfaceImplementation, Is.Empty);
        Assert.That(contracts.AuditInterfaceImplementation, Is.Empty);
        Assert.That(contracts.StrictComposition, Is.Empty);
        Assert.That(contracts.AuditComposition, Is.Empty);
        Assert.That(contracts.StrictCoverage, Is.Empty);
        Assert.That(contracts.AuditCoverage, Is.Empty);
    }

    [Test]
    public void LayerFamily_DuplicateIdAcrossStrictGroup_Throws()
    {
        string path = WriteContract(@"
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
  strict_layers:
    - id: layer-rule
      name: Layer Rule One
      layers: [core, runtime]
    - id: layer-rule
      name: Layer Rule Two
      layers: [runtime, core]
");

        Assert.Throws<InvalidOperationException>(() => new ArchitecturePolicyDocumentLoader().Load(path));
    }

    [Test]
    public void LayerFamily_LoadedContracts_AppearInAggregateEnumeration()
    {
        string path = WriteContract(@"
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
  strict_layers:
    - id: layer-strict-rule
      name: Layer Strict Rule
      layers: [core, runtime]
  audit_layers:
    - id: layer-audit-rule
      name: Layer Audit Rule
      layers: [core, runtime]
");

        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(path);

        Assert.That(document.Contracts.AllStrict.Select(c => c.Id), Does.Contain("layer-strict-rule"));
        Assert.That(document.Contracts.AllAudit.Select(c => c.Id), Does.Contain("layer-audit-rule"));
    }
}
