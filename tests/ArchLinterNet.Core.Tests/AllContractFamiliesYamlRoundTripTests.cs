using ArchLinterNet.Core.Contracts;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

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

    // Every family gets one real, non-empty strict entry and one real, non-empty audit entry
    // (content shaped to satisfy that family's dedicated validator, if it has one - see the
    // Validators/*.cs requirements this mirrors). Empty lists would round-trip identically whether
    // an alias is correct or typo'd, since IgnoreUnmatchedProperties silently drops an unrecognized
    // YAML key and an empty default list looks the same either way; a populated entry only shows up
    // in the deserialized Count if the [YamlMember] alias actually bound.
    private const string AllFamiliesYaml = @"
version: 1
name: Test
layers:
  core:
    namespace: Test.Core
  runtime:
    namespace: Test.Runtime
analysis:
  target_assemblies: [Test.Core, Test.Runtime]
contracts:
  strict:
    - name: dep strict rule
      source: core
      forbidden: [runtime]
  audit:
    - name: dep audit rule
      source: core
      forbidden: [runtime]
  strict_layers:
    - name: layer strict rule
      layers: [core, runtime]
  audit_layers:
    - name: layer audit rule
      layers: [core, runtime]
  strict_layer_templates:
    - name: layer template strict rule
      containers: [App]
      layers:
        - name: Core
  audit_layer_templates:
    - name: layer template audit rule
      containers: [App]
      layers:
        - name: Core
  strict_allow_only:
    - name: allow only strict rule
      source: core
      allowed: [runtime]
  audit_allow_only:
    - name: allow only audit rule
      source: core
      allowed: [runtime]
  strict_cycles:
    - name: cycle strict rule
      layers: [core, runtime]
  audit_cycles:
    - name: cycle audit rule
      layers: [core, runtime]
  strict_method_body:
    - name: method body strict rule
      source: core
      forbidden_calls: [System.Console.WriteLine]
  audit_method_body:
    - name: method body audit rule
      source: core
      forbidden_calls: [System.Console.WriteLine]
  strict_asmdef:
    - name: asmdef strict rule
      source_assemblies: [Test.Core]
  audit_asmdef:
    - name: asmdef audit rule
      source_assemblies: [Test.Core]
  strict_independence:
    - name: independence strict rule
      layers: [core]
  audit_independence:
    - name: independence audit rule
      layers: [core]
  strict_assembly_independence:
    - name: assembly independence strict rule
      assemblies: [Test.Core]
  audit_assembly_independence:
    - name: assembly independence audit rule
      assemblies: [Test.Core]
  strict_assembly_dependency:
    - name: assembly dependency strict rule
      source: Test.Core
      forbidden: [Test.Runtime]
  audit_assembly_dependency:
    - name: assembly dependency audit rule
      source: Test.Core
      forbidden: [Test.Runtime]
  strict_assembly_allow_only:
    - name: assembly allow only strict rule
      source: Test.Core
      allowed: [Test.Runtime]
  audit_assembly_allow_only:
    - name: assembly allow only audit rule
      source: Test.Core
      allowed: [Test.Runtime]
  strict_package_dependency:
    - name: package dependency strict rule
      source: Test.Core
      forbidden: [Some.Package]
  audit_package_dependency:
    - name: package dependency audit rule
      source: Test.Core
      forbidden: [Some.Package]
  strict_package_allow_only:
    - name: package allow only strict rule
      source: Test.Core
      allowed: [Some.Package]
  audit_package_allow_only:
    - name: package allow only audit rule
      source: Test.Core
      allowed: [Some.Package]
  strict_project_metadata:
    - name: project metadata strict rule
      projects: [src/Sample.csproj]
      required_properties:
        Nullable: enable
  audit_project_metadata:
    - name: project metadata audit rule
      projects: [src/Sample.csproj]
      required_properties:
        Nullable: enable
  strict_protected:
    - name: protected strict rule
      protected: [Test.Core.Internal]
  audit_protected:
    - name: protected audit rule
      protected: [Test.Core.Internal]
  strict_external:
    - name: external strict rule
      source: core
      forbidden: [Newtonsoft.Json]
  audit_external:
    - name: external audit rule
      source: core
      forbidden: [Newtonsoft.Json]
  strict_external_allow_only:
    - name: external allow only strict rule
      source: core
      allowed: [System.Text.Json]
  audit_external_allow_only:
    - name: external allow only audit rule
      source: core
      allowed: [System.Text.Json]
  strict_acyclic_siblings:
    - name: acyclic sibling strict rule
      ancestors: [Test.Feature]
  audit_acyclic_siblings:
    - name: acyclic sibling audit rule
      ancestors: [Test.Feature]
  strict_type_placement:
    - name: type placement strict rule
      types_matching:
        name_suffix: Service
      must_reside_in_layers: [core]
  audit_type_placement:
    - name: type placement audit rule
      types_matching:
        name_suffix: Service
      must_reside_in_layers: [core]
  strict_public_api_surface:
    - name: public api surface strict rule
      assemblies: [Test.Core]
  audit_public_api_surface:
    - name: public api surface audit rule
      assemblies: [Test.Core]
  strict_attribute_usage:
    - name: attribute usage strict rule
      attributes: [System.ObsoleteAttribute]
      allowed_only_in_layers: [core]
  audit_attribute_usage:
    - name: attribute usage audit rule
      attributes: [System.ObsoleteAttribute]
      allowed_only_in_layers: [core]
  strict_inheritance:
    - name: inheritance strict rule
      source_layers: [core]
      forbidden_base_types: [System.Exception]
  audit_inheritance:
    - name: inheritance audit rule
      source_layers: [core]
      forbidden_base_types: [System.Exception]
  strict_interface_implementation:
    - name: interface implementation strict rule
      interfaces: [System.IDisposable]
      allowed_only_in_layers: [core]
  audit_interface_implementation:
    - name: interface implementation audit rule
      interfaces: [System.IDisposable]
      allowed_only_in_layers: [core]
  strict_composition:
    - name: composition strict rule
      forbidden_apis: [System.Console.WriteLine]
      allowed_only_in_layers: [core]
  audit_composition:
    - name: composition audit rule
      forbidden_apis: [System.Console.WriteLine]
      allowed_only_in_layers: [core]
  strict_coverage:
    - name: coverage strict rule
      scope: namespace
      roots:
        - namespace: Test.Feature
  audit_coverage:
    - name: coverage audit rule
      scope: namespace
      roots:
        - namespace: Test.Feature
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
        Assert.That(contracts.StrictLayerTemplates, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditLayerTemplates, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictAllowOnly, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditAllowOnly, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictCycles, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditCycles, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictMethodBody, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditMethodBody, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictAsmdef, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditAsmdef, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictIndependence, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditIndependence, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictAssemblyIndependence, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditAssemblyIndependence, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictAssemblyDependency, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditAssemblyDependency, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictAssemblyAllowOnly, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditAssemblyAllowOnly, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictPackageDependency, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditPackageDependency, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictPackageAllowOnly, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditPackageAllowOnly, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictProjectMetadata, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditProjectMetadata, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictProtected, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditProtected, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictExternal, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditExternal, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictExternalAllowOnly, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditExternalAllowOnly, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictAcyclicSiblings, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditAcyclicSiblings, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictTypePlacement, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditTypePlacement, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictPublicApiSurface, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditPublicApiSurface, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictAttributeUsage, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditAttributeUsage, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictInheritance, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditInheritance, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictInterfaceImplementation, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditInterfaceImplementation, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictComposition, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditComposition, Has.Count.EqualTo(1));
        Assert.That(contracts.StrictCoverage, Has.Count.EqualTo(1));
        Assert.That(contracts.AuditCoverage, Has.Count.EqualTo(1));

        // AllStrict/AllAudit must reflect the populated groups too, excluding layer_template.
        Assert.That(contracts.AllStrict.Count(), Is.EqualTo(24));
        Assert.That(contracts.AllAudit.Count(), Is.EqualTo(24));
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
