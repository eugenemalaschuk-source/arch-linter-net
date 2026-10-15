using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Testing;
using NUnit.Framework;
using ConsumerFixtures = ArchLinterNet.Annotations.CSharp9Consumer;

namespace ArchLinterNet.Core.Tests
{
    [TestFixture]
    public sealed class CanonicalContextConsumerIntegrationTests
    {
        private static string WritePolicy(string policyContents)
        {
            string directory = Path.Combine(Path.GetTempPath(), $"arch-linter-canonical-context-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "dependencies.arch.yml");
            File.WriteAllText(path, policyContents);
            return path;
        }

        [Test]
        public void TestingApi_StrictContextContract_UsesCanonicalRoleAndContextMetadata()
        {
            string policyPath = WritePolicy("""
            version: 1
            name: Canonical context Testing API
            layers: {}
            analysis:
              target_assemblies: [ArchLinterNet.Annotations.CSharp9Consumer]
            contracts:
              strict_context_dependencies:
                - id: canonical-sales-no-inventory
                  name: canonical-sales-must-not-depend-on-inventory
                  source:
                    role: Entity
                    metadata:
                      boundedContext: Sales
                  forbidden:
                    - role: Entity
                      metadata:
                        boundedContext: Inventory
                  reason: Sales entities must not reference inventory entities.
            """);

            try
            {
                ArchitectureValidationResult result = ArchitectureAssertions.FromPolicy(policyPath).ValidateStrict();

                Assert.Multiple(() =>
                {
                    Assert.That(result.Passed, Is.False, string.Join("; ", result.Violations));
                    Assert.That(result.Violations.Select(violation => violation.ContractId),
                        Does.Contain("canonical-sales-no-inventory"));
                    Assert.That(result.Violations.Any(violation =>
                            violation.SourceType == typeof(ConsumerFixtures.SalesCheckout).FullName
                            && violation.ForbiddenReferences.Any(reference => reference.Contains(
                                typeof(ConsumerFixtures.InventoryStockItem).FullName!, StringComparison.Ordinal))),
                        Is.True,
                        "The violation should identify the edge selected by canonical context metadata.");
                });
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(policyPath)!, true);
            }
        }

        [Test]
        public void Explain_ContextDependency_UsesCanonicalContextMetadataInSelectors()
        {
            string policyPath = WritePolicy("""
            version: 1
            name: Canonical context explain
            layers: {}
            analysis:
              target_assemblies: [ArchLinterNet.Annotations.CSharp9Consumer]
            contracts:
              strict_context_dependencies:
                - id: canonical-sales-no-inventory-explain
                  name: canonical-sales-must-not-depend-on-inventory
                  source:
                    role: Entity
                    metadata:
                      boundedContext: Sales
                  forbidden:
                    - role: Entity
                      metadata:
                        boundedContext: Inventory
                  reason: Explain must use canonical context selectors.
            """);

            try
            {
                using ArchitectureEngine engine = new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
                ArchitectureExplainOutcome outcome = engine.Explain(new ArchitectureExplainRequest
                {
                    PolicyPath = policyPath,
                    Source = typeof(ConsumerFixtures.SalesCheckout).FullName!,
                    Target = typeof(ConsumerFixtures.InventoryStockItem).FullName!,
                    Level = ArchitectureGraphLevel.Type,
                });

                Assert.That(outcome.Path, Is.EqualTo(new[]
                {
                    typeof(ConsumerFixtures.SalesCheckout).FullName,
                    typeof(ConsumerFixtures.InventoryStockItem).FullName,
            }));
                Assert.That(outcome.ContractIds, Does.Contain("canonical-sales-no-inventory-explain"));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(policyPath)!, true);
            }
        }

        [Test]
        public void SemanticCoverage_ContextSelector_UsesCanonicalContextMetadata()
        {
            ArchitectureContractDocument document = new();
            document.Layers["sales-entities"] = new ArchitectureLayer
            {
                Selector = new ArchitectureLayerSelector
                {
                    Role = "Entity",
                    Metadata = new Dictionary<string, object> { ["boundedContext"] = "Sales" }
                }
            };
            ArchitectureCoverageContract contract = new()
            {
                Name = "canonical-context-semantic-coverage",
                Id = "canonical-context-semantic-coverage",
                Scope = "semantic_role",
                Reason = "Sales entities are explicitly governed.",
                Roots = { new ArchitectureCoverageRoot { Namespace = "ArchLinterNet.Annotations.CSharp9Consumer" } }
            };
            ArchitectureAnalysisContext context = new(
                repositoryRoot: AppContext.BaseDirectory,
                targetAssemblies: [typeof(ConsumerFixtures.SalesCheckout).Assembly],
                missingAssemblyNames: Array.Empty<string>(),
                assemblyProbingPaths: Array.Empty<string>());

            ArchitectureCoverageSummary summary = new ArchitectureContractRunner(context, document)
                .BuildCoverageSummary(contract)!;

            Assert.Multiple(() =>
            {
                Assert.That(summary.CoveredItems.Select(item => item.Item), Does.Contain(
                    typeof(ConsumerFixtures.SalesCheckout).FullName));
                Assert.That(summary.UncoveredItems.Select(item => item.Item), Does.Contain(
                    typeof(ConsumerFixtures.InventoryStockItem).FullName));
            });
        }
    }
}
