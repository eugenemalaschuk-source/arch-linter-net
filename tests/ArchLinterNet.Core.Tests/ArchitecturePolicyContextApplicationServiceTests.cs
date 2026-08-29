using System.Text.Json;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.PolicyWeakening;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyContextApplicationServiceTests
{
    private string _repositoryRoot = null!;
    private ArchitectureEngine _engine = null!;

    private static readonly string[] _salesToCatalogPortSelectorRoles = { "ApplicationLayer", "Port", "DomainLayer", "Adapter" };
    private static readonly string[] _sampleHostProjectPaths = { "src/Sample.Host/Sample.Host.csproj" };
    private static readonly string[] _testsProjectExcludeGlobs = { "tests/**" };
    private static readonly string[] _unitySemanticRoles = { "System", "UnityEditor" };
    private static readonly string[] _unityRuntimeContextValues = { "editor", "player" };
    private static readonly string[] _allowedContextRoles = { "ApplicationLayer", "DomainLayer" };
    private static readonly string[] _transportContextValues = { "http" };
    private static readonly string[] _moduleShapeContainers = { "Sample.Modules.Sales", "Sample.Modules.Legacy" };
    private static readonly string[] _moduleShapeExcludeContainers = { "Sample.Modules.Legacy" };
    private static readonly string[] _moduleShapeLayerNames = { "Api", "Application", "Domain" };
    private static readonly string[] _moduleShapeLayerOptionalFlags = { "false", "true", "false" };
    private static readonly string[] _moduleShapeExhaustiveValues = { "true" };
    private static readonly string[] _compositionForbiddenApis = { "Legacy.ServiceLocator.Get", "Legacy.Container.Register" };
    private static readonly string[] _compositionAllowedAssemblySets = { "host_assemblies" };
    private static readonly string[] _hostsAvoidForbiddenSetNames = { "host_assemblies", "legacy_assemblies", "future_assemblies" };
    private static readonly string[] _hostsAvoidForbiddenInstanceSources = { "Sample.Host.Api" };

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _repositoryRoot = new ArchitectureRepositoryRootResolver().Resolve();
        _engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _engine.Dispose();
    }

    [Test]
    public void Export_ModularMonolithImportedPolicy_ProjectsEffectiveRolesContractsAndPortableProvenance()
    {
        ArchitecturePolicyContextExport context = Export("samples/policies/imports/modular-monolith/architecture/arch.yml");
        string json = ArchitecturePolicyContextFormatter.FormatAsJson(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.SchemaVersion, Is.EqualTo(5));
            Assert.That(context.Kind, Is.EqualTo("architecture-policy-context"));
            Assert.That(context.Guardrails.PolicyWeakening, Is.EqualTo("error"));
            Assert.That(context.Policy.HasImports, Is.True);
            Assert.That(context.Contracts.Select(contract => contract.Id), Does.Contain("sales-to-catalog-through-port"));
            Assert.That(context.Contracts.Single(contract => contract.Id == "sales-to-catalog-through-port")
                .Selectors.Where(selector => !string.IsNullOrWhiteSpace(selector.Role)).Select(selector => selector.Role),
                Is.EquivalentTo(_salesToCatalogPortSelectorRoles));
            Assert.That(context.SemanticRoles, Does.Contain("DomainLayer").And.Contain("ApplicationLayer"));
            Assert.That(context.Contexts.Single(value => value.Key == "module").Values,
                Does.Contain("Sales").And.Contain("Catalog"));
            Assert.That(context.Sources.Select(source => source.Path), Does.Contain("architecture/arch.yml"));
            Assert.That(json, Does.Not.Contain(_repositoryRoot));
        });
    }

    [Test]
    public void Formatters_UnchangedPolicy_AreDeterministicAndDescribeTheirNonValidationBoundary()
    {
        ArchitecturePolicyContextExport context = Export("samples/policies/imports/modular-monolith/architecture/arch.yml");

        string firstJson = ArchitecturePolicyContextFormatter.FormatAsJson(context);
        string secondJson = ArchitecturePolicyContextFormatter.FormatAsJson(Export("samples/policies/imports/modular-monolith/architecture/arch.yml"));
        string firstMarkdown = ArchitecturePolicyContextFormatter.FormatAsMarkdown(context);
        string secondMarkdown = ArchitecturePolicyContextFormatter.FormatAsMarkdown(context);

        using JsonDocument document = JsonDocument.Parse(firstJson);
        Assert.Multiple(() =>
        {
            Assert.That(secondJson, Is.EqualTo(firstJson));
            Assert.That(secondMarkdown, Is.EqualTo(firstMarkdown));
            Assert.That(document.RootElement.GetProperty("schema_version").GetInt32(), Is.EqualTo(5));
            Assert.That(document.RootElement.GetProperty("kind").GetString(), Is.EqualTo("architecture-policy-context"));
            Assert.That(firstMarkdown, Does.Contain("# Architecture policy context"));
            Assert.That(firstMarkdown, Does.Contain("Policy weakening severity: `error`"));
            Assert.That(firstMarkdown, Does.Contain("does not build projects, analyze assemblies, or prove architecture compliance"));
        });
    }

    [Test]
    public void Export_Topology_ProjectsOrderedNativeFactsAndProvenance()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-topology-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        try
        {
            File.WriteAllText(policyPath, """
                version: 1
                name: Native topology context
                layers:
                  application: { namespace: Sample.Application }
                  domain: { namespace: Sample.Domain }
                topology:
                  mode: exhaustive
                  subject_kind: type
                  scope:
                    selectors: [{ layer: domain }, { layer: application }]
                  nodes:
                    - id: domain
                      mappings: [{ layer: domain }]
                    - id: application
                      mappings: [{ layer: application }]
                  allowed_edges: [{ from: application, to: domain }]
                  out_of_scope:
                    - id: generated
                      selector: { namespace: Sample.Generated }
                      reason: Generated code is reviewed outside this topology.
                  stale_declarations: true
                """);

            ArchitecturePolicyContextExport context = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath });
            ArchitecturePolicyContextTopology topology = context.Topology!;
            string markdown = ArchitecturePolicyContextFormatter.FormatAsMarkdown(context);
            using JsonDocument json = JsonDocument.Parse(ArchitecturePolicyContextFormatter.FormatAsJson(context));

            Assert.Multiple(() =>
            {
                Assert.That(topology, Is.Not.Null);
                Assert.That(topology.Mode, Is.EqualTo("exhaustive"));
                Assert.That(topology.SubjectKind, Is.EqualTo("type"));
                Assert.That(topology.ScopeSelectors.Select(selector => selector.Value), Is.EqualTo(new[] { "application", "domain" }));
                Assert.That(topology.Nodes.Select(node => node.Id), Is.EqualTo(new[] { "application", "domain" }));
                Assert.That(topology.AllowedEdges.Single().From, Is.EqualTo("application"));
                Assert.That(topology.OutOfScope.Single().Selector.Value, Is.EqualTo("Sample.Generated"));
                Assert.That(topology.OutOfScope.Single().Provenance!.YamlPath, Does.Contain("out_of_scope"));
                Assert.That(json.RootElement.GetProperty("topology").GetProperty("subject_kind").GetString(), Is.EqualTo("type"));
                Assert.That(markdown, Does.Contain("## Declared topology").And.Contain("Reviewed out of scope `generated`"));
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Export_ImportedTopology_RetainsFragmentExclusionProvenance()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-topology-context-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        string fragmentPath = Path.Combine(temporaryDirectory, "topology.yml");
        try
        {
            File.WriteAllText(policyPath, """
                version: 1
                name: Imported topology context
                imports: [topology.yml]
                layers:
                  application: { namespace: Sample.Application }
                analysis: { target_assemblies: [] }
                contracts: {}
                """);
            File.WriteAllText(fragmentPath, """
                topology:
                  subject_kind: type
                  scope:
                    selectors: [{ namespace: Sample.* }]
                  nodes:
                    - id: application
                      mappings: [{ namespace: Sample.Application }]
                  out_of_scope:
                    - id: generated
                      selector: { namespace: Sample.Generated }
                      reason: Generated code is separately reviewed.
                """);

            ArchitecturePolicyContextExport context = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath });
            ArchitecturePolicyContextProvenance provenance = context.Topology!.OutOfScope.Single().Provenance!;

            Assert.Multiple(() =>
            {
                Assert.That(provenance.SourcePath, Is.EqualTo("topology.yml"));
                Assert.That(provenance.Role, Is.EqualTo("fragment"));
                Assert.That(provenance.YamlPath, Is.EqualTo("topology.out_of_scope[0]"));
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Export_TopologyContextSelectorsUseStructuralDeterministicOrdering()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-topology-context-order-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        try
        {
            string Policy(string selectors) => $$"""
                version: 1
                name: Topology ordering
                topology:
                  subject_kind: type
                  scope:
                    selectors:
                {{selectors}}
                  nodes:
                    - id: application
                      mappings: [{ namespace: Sample.Application }]
                """;
            const string FirstOrder = "      - context: { role: DomainLayer, metadata: { a: \"b,c=d\" } }\n      - context: { role: DomainLayer, metadata: { a: b, c: d } }";
            const string SecondOrder = "      - context: { role: DomainLayer, metadata: { a: b, c: d } }\n      - context: { role: DomainLayer, metadata: { a: \"b,c=d\" } }";

            File.WriteAllText(policyPath, Policy(FirstOrder));
            ArchitecturePolicyContextTopology first = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath }).Topology!;
            File.WriteAllText(policyPath, Policy(SecondOrder));
            ArchitecturePolicyContextTopology second = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath }).Topology!;

            string[] SelectorShapes(ArchitecturePolicyContextTopology topology) => topology.ScopeSelectors
                .Select(selector => string.Join(",", selector.Context!.Metadata.OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => item.Key + "=" + item.Value)))
                .ToArray();

            Assert.That(SelectorShapes(second), Is.EqualTo(SelectorShapes(first)));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Export_ProjectsSchemaValidatedPolicyWeakeningSeverity()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        try
        {
            File.WriteAllText(policyPath, """
                version: 1
                name: Policy weakening severity
                analysis:
                  policy_weakening: warn
                  projects: [src/Sample.Host/Sample.Host.csproj]
                  project_exclude: [tests/**]
                """);

            ArchitecturePolicyContextExport context = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath });

            Assert.Multiple(() =>
            {
                Assert.That(context.Guardrails.PolicyWeakening, Is.EqualTo("warn"));
                Assert.That(context.Analysis.Projects, Is.EqualTo(_sampleHostProjectPaths));
                Assert.That(context.Analysis.ProjectExclude, Is.EqualTo(_testsProjectExcludeGlobs));
                Assert.That(ArchitecturePolicyContextFormatter.FormatAsJson(context),
                    Does.Contain("\"policy_weakening\": \"warn\""));
                Assert.That(ArchitecturePolicyContextFormatter.FormatAsMarkdown(context),
                    Does.Contain("Policy weakening severity: `warn`"));
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Export_InvalidPolicyWeakeningSeverity_FailsPolicyLoading()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        try
        {
            File.WriteAllText(policyPath, """
                version: 1
                name: Invalid policy weakening severity
                analysis:
                  policy_weakening: trace
                """);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _engine.ExportPolicyContext(new ArchitecturePolicyContextRequest { PolicyPath = policyPath }))!;

            Assert.That(exception.Message, Does.Contain("analysis.policy_weakening"));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Export_IgnoredViolation_RetainsTypedMatchersAlongsideDisplayDetails()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        try
        {
            File.WriteAllText(policyPath, """
                version: 1
                name: Typed ignore context
                layers:
                  application: { namespace: Sample.Application }
                  infrastructure: { namespace: Sample.Infrastructure }
                contracts:
                  strict:
                    - id: application-no-infrastructure
                      name: application-no-infrastructure
                      source: application
                      forbidden: [infrastructure]
                      ignored_violations:
                        - source_type: "*"
                          forbidden_reference: "*"
                          reason: Reviewed temporary migration.
                """);

            ArchitecturePolicyContextExport context = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath });
            ArchitecturePolicyContextException ignored = context.Exceptions.Single(item => item.Kind == "ignored_violation");

            Assert.Multiple(() =>
            {
                Assert.That(ignored.Details, Is.EqualTo("*; *"));
                Assert.That(ignored.IgnoredViolation, Is.EqualTo(new ArchitecturePolicyContextIgnoredViolation("*", "*")));
                Assert.That(ArchitecturePolicyContextFormatter.FormatAsJson(context),
                    Does.Contain("\"ignored_violation\": {").And.Contain("\"source_type\": \"*\""));
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Export_StructuredWaiver_ProjectsLifecycleProfileAndCanonicalDeclaration()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        try
        {
            File.WriteAllText(policyPath, $"""
                version: 2
                name: Structured waiver context
                layers:
                  application:
                    namespace: Sample.Application
                  infrastructure:
                    namespace: Sample.Infrastructure
                analysis:
                  waiver_lifecycle_profile: strict
                contracts:
                  strict:
                    - id: application-no-infrastructure
                      name: application-no-infrastructure
                      source: application
                      forbidden: [infrastructure]
                      ignored_violations:
                        - id: ARCH-IGN-001
                          source_type: Sample.Application.Legacy
                          forbidden_reference: Sample.Infrastructure.LegacyGateway
                          target:
                            fingerprint: sha256:{new string('a', 64)}
                          reason: Temporary migration
                          owner: architecture-team
                          issue: ARCH-231
                          introduced: 2026-08-01
                          expires: 2026-10-01
                """);

            ArchitecturePolicyContextExport context = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath });
            ArchitecturePolicyContextWaiver waiver = context.Waivers.Single();

            Assert.Multiple(() =>
            {
                Assert.That(context.SchemaVersion, Is.EqualTo(5));
                Assert.That(context.WaiverLifecycleProfile, Is.EqualTo("strict"));
                Assert.That(waiver.WaiverId, Is.EqualTo("ARCH-IGN-001"));
                Assert.That(waiver.TargetFingerprint, Is.EqualTo("sha256:" + new string('a', 64)));
                Assert.That(waiver.Provenance, Is.Not.Null);
                Assert.That(ArchitecturePolicyContextFormatter.FormatAsJson(context), Does.Contain("\"waivers\""));
                Assert.That(ArchitecturePolicyContextFormatter.FormatAsMarkdown(context), Does.Contain("Structured architecture waivers"));
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Export_SourceSetStructuredWaiver_ProjectsOneCanonicalDeclarationAndWeakeningFinding()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        try
        {
            File.WriteAllText(policyPath, $"""
                version: 2
                name: Source-set structured waiver context
                analysis:
                  target_assemblies: [Sample.Host.Api, Sample.Host.Worker, Sample.Forbidden]
                  waiver_lifecycle_profile: strict
                source_sets:
                  hosts:
                    globs: [Sample.Host.*]
                contracts:
                  strict_assembly_dependency:
                    - id: hosts-avoid-forbidden
                      name: hosts-avoid-forbidden
                      source_sets: [hosts]
                      forbidden: [Sample.Forbidden]
                      ignored_violations:
                        - id: ARCH-IGN-001
                          source_type: Sample.Host.Legacy
                          forbidden_reference: Sample.Forbidden.LegacyGateway
                          target:
                            fingerprint: sha256:{new string('a', 64)}
                          reason: Temporary migration
                          owner: architecture-team
                          issue: ARCH-231
                          introduced: 2026-08-01
                          expires: 2026-10-01
                """);

            ArchitecturePolicyContextExport context = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath });
            ArchitecturePolicyContextWaiver waiver = context.Waivers.Single();
            ArchitecturePolicyWeakeningResult weakening = ArchitecturePolicyWeakeningComparer.Compare(new(
                context with { Waivers = [] },
                context));

            Assert.Multiple(() =>
            {
                Assert.That(context.Waivers, Has.Count.EqualTo(1));
                Assert.That(waiver.ContractId, Is.EqualTo("hosts-avoid-forbidden"));
                Assert.That(waiver.ContractName, Is.EqualTo("hosts-avoid-forbidden"));
                Assert.That(waiver.Provenance!.YamlPath,
                    Is.EqualTo("contracts.strict_assembly_dependency[0].ignored_violations[0]"));
                Assert.That(weakening.Findings, Has.Count.EqualTo(1));
                Assert.That(weakening.Findings.Single().Kind, Is.EqualTo("structured_waiver_added"));
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Export_UnityStyleClassification_DoesNotRequireTargetAssemblyAnalysis()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        try
        {
            File.WriteAllText(policyPath, """
                version: 1
                name: Unity Context
                layers:
                  player:
                    namespace: Game.Gameplay
                    selector:
                      role: System
                      metadata: { platform: Unity, runtime: player }
                  editor:
                    namespace: Game.Editor
                    selector:
                      role: UnityEditor
                      metadata: { platform: Unity, runtime: editor }
                analysis:
                  target_assemblies: [Missing.Unity.Target]
                classification:
                  namespace:
                    - namespace: Game.Gameplay
                      role: System
                      metadata: { platform: Unity, runtime: player }
                    - namespace: Game.Editor
                      role: UnityEditor
                      metadata: { platform: Unity, runtime: editor }
                contracts:
                  strict_context_dependencies:
                    - id: player-no-editor
                      name: player-no-editor
                      source:
                        role: System
                        metadata: { platform: Unity, runtime: player }
                      forbidden:
                        - role: UnityEditor
                          metadata: { platform: Unity, runtime: editor }
                      reason: Player systems must not reference editor tooling.
                """);

            ArchitecturePolicyContextExport context = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath });

            Assert.Multiple(() =>
            {
                Assert.That(context.SemanticRoles, Is.EqualTo(_unitySemanticRoles));
                Assert.That(context.Contexts.Single(value => value.Key == "runtime").Values,
                    Is.EqualTo(_unityRuntimeContextValues));
                Assert.That(context.Contracts.Single().Id, Is.EqualTo("player-no-editor"));
                Assert.That(context.Sources.Single().Path, Is.EqualTo("policy.yml"));
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Export_PortBoundaryAdapterBindings_RetainsTheCompleteEffectivePolicyBinding()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        try
        {
            File.WriteAllText(policyPath, """
                version: 1
                name: Port Boundary Context
                contracts:
                  strict_port_boundaries:
                    - id: sales-to-catalog-through-port
                      name: sales-to-catalog-through-port
                      source:
                        role: ApplicationLayer
                        metadata: { module: Sales }
                      target_context:
                        metadata: { module: Catalog }
                      allowed_seams:
                        - role: Port
                          metadata: { module: Catalog }
                      forbidden:
                        - role: DomainLayer
                          metadata: { module: Catalog }
                      adapter_bindings:
                        - adapter:
                            role: Adapter
                            metadata: { module: Catalog, transport: http }
                          expected_port:
                            role: Port
                            metadata: { module: Catalog, direction: inbound }
                          allowed_contexts:
                            - role: ApplicationLayer
                              metadata: { module: Sales }
                            - role: DomainLayer
                              metadata: { module: Sales }
                      reason: Sales reaches Catalog only through the reviewed port.
                """);

            ArchitecturePolicyContextExport context = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath });
            ArchitecturePolicyContextContract boundary = context.Contracts.Single();
            ArchitecturePolicyContextAdapterBinding binding = boundary.AdapterBindings.Single();
            string json = ArchitecturePolicyContextFormatter.FormatAsJson(context);
            string markdown = ArchitecturePolicyContextFormatter.FormatAsMarkdown(context);

            Assert.Multiple(() =>
            {
                Assert.That(binding.Adapter.Role, Is.EqualTo("Adapter"));
                Assert.That(binding.Adapter.Metadata, Is.EqualTo(new Dictionary<string, string>
                {
                    ["module"] = "Catalog",
                    ["transport"] = "http",
                }));
                Assert.That(binding.ExpectedPort.Role, Is.EqualTo("Port"));
                Assert.That(binding.ExpectedPort.Metadata["direction"], Is.EqualTo("inbound"));
                Assert.That(binding.AllowedContexts.Select(selector => selector.Role),
                    Is.EqualTo(_allowedContextRoles));
                Assert.That(context.SemanticRoles,
                    Does.Contain("Adapter").And.Contain("Port").And.Contain("DomainLayer"));
                Assert.That(context.Contexts.Single(value => value.Key == "transport").Values, Is.EqualTo(_transportContextValues));
                Assert.That(json, Does.Contain("\"adapter_bindings\""));
                Assert.That(json, Does.Contain("\"expected_port\""));
                Assert.That(json, Does.Contain("\"allowed_contexts\""));
                Assert.That(markdown, Does.Contain("adapter binding:"));
                Assert.That(markdown, Does.Contain("expected_port selector: role `Port`"));
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Export_TypedContractFacts_RetainsLayerTemplateAndCompositionSemantics()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        try
        {
            File.WriteAllText(policyPath, """
                version: 1
                name: Complete Contract Facts
                layers:
                  composition:
                    namespace: Sample.Composition
                analysis:
                  target_assemblies: [Sample.Host]
                source_sets:
                  host_assemblies:
                    globs: [Sample.Host]
                contracts:
                  strict_layer_templates:
                    - id: module-shape
                      name: module-shape
                      containers: [Sample.Modules.Sales, Sample.Modules.Legacy]
                      exclude_containers: [Sample.Modules.Legacy]
                      layers:
                        - name: Api
                        - name: Application
                          optional: true
                        - name: Domain
                      exhaustive: true
                      reason: Every module follows the reviewed shape.
                  strict_composition:
                    - id: composition-root-only
                      name: composition-root-only
                      forbidden_apis: [Legacy.ServiceLocator.Get, Legacy.Container.Register]
                      allowed_only_in_layers: [composition]
                      allowed_only_in_namespaces: [Sample.Bootstrap]
                      allowed_only_in_projects: [src/Sample.Host/Sample.Host.csproj]
                      allowed_only_in_assemblies: [Sample.Host]
                      allowed_only_in_assembly_sets: [host_assemblies]
                      allowed_only_in_types:
                        - assembly: Sample.Host
                          type: Sample.Program
                      reason: Registration belongs only in the composition root.
                """);

            ArchitecturePolicyContextExport context = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath });
            ArchitecturePolicyContextContract template = context.Contracts.Single(contract => contract.Id == "module-shape");
            ArchitecturePolicyContextContract composition = context.Contracts.Single(contract => contract.Id == "composition-root-only");
            ArchitecturePolicyContextContractFact layers = template.Facts.Single(fact => fact.Name == "layers");
            ArchitecturePolicyContextContractFact allowedType = composition.Facts.Single(fact => fact.Name == "allowed_only_in_types").Items.Single();
            string json = ArchitecturePolicyContextFormatter.FormatAsJson(context);
            string markdown = ArchitecturePolicyContextFormatter.FormatAsMarkdown(context);

            Assert.Multiple(() =>
            {
                Assert.That(template.Facts.Single(fact => fact.Name == "containers").Values,
                    Is.EqualTo(_moduleShapeContainers));
                Assert.That(template.Facts.Single(fact => fact.Name == "exclude_containers").Values,
                    Is.EqualTo(_moduleShapeExcludeContainers));
                Assert.That(layers.Items.Select(item => item.Items.Single(field => field.Name == "name").Values.Single()),
                    Is.EqualTo(_moduleShapeLayerNames));
                Assert.That(layers.Items.Select(item => item.Items.Single(field => field.Name == "optional").Values.Single()),
                    Is.EqualTo(_moduleShapeLayerOptionalFlags));
                Assert.That(template.Facts.Single(fact => fact.Name == "exhaustive").Values, Is.EqualTo(_moduleShapeExhaustiveValues));
                Assert.That(composition.Facts.Single(fact => fact.Name == "forbidden_apis").Values,
                    Is.EqualTo(_compositionForbiddenApis));
                Assert.That(composition.Facts.Single(fact => fact.Name == "allowed_only_in_assembly_sets").Values,
                    Is.EqualTo(_compositionAllowedAssemblySets));
                Assert.That(allowedType.Items.Single(field => field.Name == "assembly").Values.Single(), Is.EqualTo("Sample.Host"));
                Assert.That(allowedType.Items.Single(field => field.Name == "type").Values.Single(), Is.EqualTo("Sample.Program"));
                Assert.That(json, Does.Contain("\"facts\"").And.Contain("\"allowed_only_in_types\""));
                Assert.That(markdown, Does.Contain("exclude_containers:").And.Contain("allowed_only_in_types"));
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void Export_SourceExpansions_RetainsAuthoredFanOutExclusionsAndProvenance()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"arch-linter-policy-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        string policyPath = Path.Combine(temporaryDirectory, "policy.yml");
        try
        {
            File.WriteAllText(policyPath, """
                version: 1
                name: Source Expansion Context
                analysis:
                  target_assemblies: [Sample.Host.Api, Sample.Host.Worker, Sample.Legacy.Host, Sample.Forbidden]
                source_sets:
                  host_assemblies:
                    globs: [Sample.Host.*]
                  legacy_assemblies:
                    globs: [Sample.Legacy.*]
                  future_assemblies:
                    globs: [Sample.Future.*]
                    optional: true
                    reason: Future hosts have not been extracted yet.
                contracts:
                  strict_assembly_dependency:
                    - id: hosts-avoid-forbidden
                      name: hosts-avoid-forbidden
                      source_sets: [host_assemblies, legacy_assemblies, future_assemblies]
                      exclude_sources: [Sample.Host.Worker]
                      exclude_source_sets: [legacy_assemblies]
                      forbidden: [Sample.Forbidden]
                      reason: Only the reviewed host source remains in scope.
                """);

            ArchitecturePolicyContextExport context = _engine.ExportPolicyContext(
                new ArchitecturePolicyContextRequest { PolicyPath = policyPath });
            ArchitecturePolicyContextSourceExpansion expansion = context.SourceExpansions.Single(item =>
                item.AuthoredContractId == "hosts-avoid-forbidden");
            ArchitecturePolicyContextExpandedInstance optionalInclusion = expansion.Inclusions.Single(item =>
                item.SetName == "future_assemblies");
            ArchitecturePolicyContextExpandedExclusion directExclusion = expansion.Exclusions.Single(item =>
                item.Source == "Sample.Host.Worker" && item.SetName is null);
            ArchitecturePolicyContextExpandedExclusion setExclusion = expansion.Exclusions.Single(item =>
                item.SetName == "legacy_assemblies");
            string json = ArchitecturePolicyContextFormatter.FormatAsJson(context);
            string markdown = ArchitecturePolicyContextFormatter.FormatAsMarkdown(context);

            Assert.Multiple(() =>
            {
                Assert.That(expansion.Kind, Is.EqualTo("fan_out"));
                Assert.That(expansion.SetNames,
                    Is.EqualTo(_hostsAvoidForbiddenSetNames));
                Assert.That(expansion.Instances.Select(item => item.Source), Is.EqualTo(_hostsAvoidForbiddenInstanceSources));
                Assert.That(expansion.Inclusions.Select(item => item.Source),
                    Does.Contain("Sample.Host.Worker").And.Contain("Sample.Legacy.Host"));
                Assert.That(optionalInclusion.OptionalEmpty, Is.True);
                Assert.That(optionalInclusion.OptionalReason, Does.Contain("not been extracted"));
                Assert.That(optionalInclusion.SourceSetReferenceProvenance!.YamlPath,
                    Is.EqualTo("contracts.strict_assembly_dependency[0]/source_sets/2"));
                Assert.That(directExclusion.Matched, Is.True);
                Assert.That(directExclusion.Provenance!.YamlPath,
                    Is.EqualTo("contracts.strict_assembly_dependency[0]/exclude_sources/0"));
                Assert.That(setExclusion.Matched, Is.True);
                Assert.That(setExclusion.Source, Is.EqualTo("Sample.Legacy.Host"));
                Assert.That(setExclusion.Provenance!.YamlPath,
                    Is.EqualTo("contracts.strict_assembly_dependency[0]/exclude_source_sets/0"));
                Assert.That(context.Exceptions.Any(item => item is
                {
                    Scope: "source_expansion",
                    Subject: "hosts-avoid-forbidden",
                    Kind: "exclude_source_set",
                }), Is.True);
                Assert.That(json, Does.Contain("\"source_expansions\"").And.Contain("exclude_source_sets/0"));
                Assert.That(markdown, Does.Contain("## Source-set expansions").And.Contain("excluded source set `legacy_assemblies`"));
            });
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void ContractFactsProjector_CoversEveryRegisteredContractFamily()
    {
        Type[] registeredContractTypes = ArchitectureContractFamilyRegistry.All
            .SelectMany(descriptor => descriptor.OwnedContractTypes)
            .Distinct()
            .ToArray();

        Assert.That(ArchitecturePolicyContextContractFactsProjector.SupportedContractTypes,
            Is.EquivalentTo(registeredContractTypes));
    }

    private ArchitecturePolicyContextExport Export(string relativePolicyPath)
    {
        return _engine.ExportPolicyContext(new ArchitecturePolicyContextRequest
        {
            PolicyPath = Path.Combine(_repositoryRoot, relativePolicyPath.Replace('/', Path.DirectorySeparatorChar)),
        });
    }
}
