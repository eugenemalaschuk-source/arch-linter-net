using System.Text.Json;
using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.PolicyContext;
using ArchLinterNet.Core.Resolution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitecturePolicyContextApplicationServiceTests
{
    private string _repositoryRoot = null!;
    private ArchitectureEngine _engine = null!;

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
            Assert.That(context.SchemaVersion, Is.EqualTo(1));
            Assert.That(context.Kind, Is.EqualTo("architecture-policy-context"));
            Assert.That(context.Policy.HasImports, Is.True);
            Assert.That(context.Contracts.Select(contract => contract.Id), Does.Contain("sales-to-catalog-through-port"));
            Assert.That(context.Contracts.Single(contract => contract.Id == "sales-to-catalog-through-port")
                .Selectors.Where(selector => !string.IsNullOrWhiteSpace(selector.Role)).Select(selector => selector.Role),
                Is.EquivalentTo(new[] { "ApplicationLayer", "Port", "DomainLayer", "Adapter" }));
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
            Assert.That(document.RootElement.GetProperty("schema_version").GetInt32(), Is.EqualTo(1));
            Assert.That(document.RootElement.GetProperty("kind").GetString(), Is.EqualTo("architecture-policy-context"));
            Assert.That(firstMarkdown, Does.Contain("# Architecture policy context"));
            Assert.That(firstMarkdown, Does.Contain("does not build projects, analyze assemblies, or prove architecture compliance"));
        });
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
                Assert.That(context.SemanticRoles, Is.EqualTo(new[] { "System", "UnityEditor" }));
                Assert.That(context.Contexts.Single(value => value.Key == "runtime").Values,
                    Is.EqualTo(new[] { "editor", "player" }));
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
                    Is.EqualTo(new[] { "ApplicationLayer", "DomainLayer" }));
                Assert.That(context.SemanticRoles,
                    Does.Contain("Adapter").And.Contain("Port").And.Contain("DomainLayer"));
                Assert.That(context.Contexts.Single(value => value.Key == "transport").Values, Is.EqualTo(new[] { "http" }));
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

    private ArchitecturePolicyContextExport Export(string relativePolicyPath)
    {
        return _engine.ExportPolicyContext(new ArchitecturePolicyContextRequest
        {
            PolicyPath = Path.Combine(_repositoryRoot, relativePolicyPath.Replace('/', Path.DirectorySeparatorChar)),
        });
    }
}
