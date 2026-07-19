using ArchLinterNet.Core.Composition;
using ArchLinterNet.Core.Graph;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureExplainApplicationServiceTests
{
    private const string ExecutionNamespace = "ArchLinterNet.Core.Execution";
    private const string ContractsNamespace = "ArchLinterNet.Core.Contracts";
    private const string ModelNamespace = "ArchLinterNet.Core.Model";

    private static ArchitectureEngine CreateEngine()
    {
        return new ArchitectureEngineBuilder().AddArchLinterNetCore().Build();
    }

    private static string WritePolicy(string yaml)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-explain-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string policyPath = Path.Combine(tempDir, "dependencies.arch.yml");
        File.WriteAllText(policyPath, yaml);
        return policyPath;
    }

    [Test]
    public void Explain_DirectDependency_ReturnsPathOfLengthTwo()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            layers:
              execution:
                namespace: ArchLinterNet.Core.Execution
              contracts:
                namespace: ArchLinterNet.Core.Contracts

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict:
                - id: no-execution-to-contracts
                  name: execution-must-not-depend-on-contracts
                  source: execution
                  forbidden: [contracts]
            """);

        try
        {
            using ArchitectureEngine engine = CreateEngine();

            ArchitectureExplainOutcome outcome = engine.Explain(new ArchitectureExplainRequest
            {
                PolicyPath = policyPath,
                Source = ExecutionNamespace,
                Target = ContractsNamespace,
            });

            Assert.That(outcome.Path, Is.Not.Null);
            Assert.That(outcome.Path, Is.EqualTo(new[] { ExecutionNamespace, ContractsNamespace }));
            Assert.That(outcome.ContractIds, Does.Contain("no-execution-to-contracts"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(policyPath)!, true);
        }
    }

    [Test]
    public void Explain_TransitiveDependency_ReturnsShortestPath()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts: {}
            """);

        try
        {
            using ArchitectureEngine engine = CreateEngine();

            ArchitectureExplainOutcome outcome = engine.Explain(new ArchitectureExplainRequest
            {
                PolicyPath = policyPath,
                Source = ExecutionNamespace,
                Target = ModelNamespace,
            });

            Assert.That(outcome.Path, Is.Not.Null);
            Assert.That(outcome.Path!.First(), Is.EqualTo(ExecutionNamespace));
            Assert.That(outcome.Path!.Last(), Is.EqualTo(ModelNamespace));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(policyPath)!, true);
        }
    }

    [Test]
    public void Explain_NoPath_ReturnsNullPath()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts: {}
            """);

        try
        {
            using ArchitectureEngine engine = CreateEngine();

            ArchitectureExplainOutcome outcome = engine.Explain(new ArchitectureExplainRequest
            {
                PolicyPath = policyPath,
                Source = ModelNamespace,
                Target = "ArchLinterNet.Core.NonExistent.Namespace",
            });

            Assert.That(outcome.Path, Is.Null);
            Assert.That(outcome.ContractIds, Is.Empty);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(policyPath)!, true);
        }
    }

    [Test]
    public void Explain_ExternalGroupTarget_ResolvesFirstPartySourceAndContractId()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            layers:
              reporting:
                namespace: ArchLinterNet.Core.Reporting

            external_dependencies:
              json:
                namespace_prefixes: [System.Text.Json]

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_external:
                - id: reporting-no-json
                  name: reporting-must-not-use-json
                  source: reporting
                  forbidden: [json]
            """);

        try
        {
            using ArchitectureEngine engine = CreateEngine();

            ArchitectureExplainOutcome outcome = engine.Explain(new ArchitectureExplainRequest
            {
                PolicyPath = policyPath,
                Source = "ArchLinterNet.Core.Reporting",
                Target = "json",
            });

            Assert.That(outcome.Path, Is.EqualTo(new[] { "ArchLinterNet.Core.Reporting", "json" }));
            Assert.That(outcome.ContractIds, Does.Contain("reporting-no-json"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(policyPath)!, true);
        }
    }

    [Test]
    public void Explain_ContextDependencyWithMatchedWhen_ExpressionParticipationIsPopulated()
    {
        // Reuses ContextualContractTestFixtures (compiled into this test assembly) rather than
        // inventing new fixture types: SalesCheckout (domain=Sales) references InventoryStockItem
        // (domain=Inventory) - a real cross-domain edge the `when` below matches.
        string policyPath = WritePolicy($$"""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core.Tests]

            classification:
              attributes:
                - attribute: ContextualContractTestFixtures.ContextDomainMarkerAttribute
                  role: DomainLayer
                  metadata:
                    domain: constructor[0]

            contracts:
              strict_context_dependencies:
                - id: cross-domain-when
                  name: cross-domain-when
                  source:
                    role: DomainLayer
                  forbidden:
                    - role: DomainLayer
                      when: target.metadataText["domain"] != source.metadataText["domain"]
                  reason: Test.
            """);

        try
        {
            using ArchitectureEngine engine = CreateEngine();

            ArchitectureExplainOutcome outcome = engine.Explain(new ArchitectureExplainRequest
            {
                PolicyPath = policyPath,
                Source = typeof(ContextualContractTestFixtures.SalesCheckout).FullName!,
                Target = typeof(ContextualContractTestFixtures.InventoryStockItem).FullName!,
                Level = ArchitectureGraphLevel.Type,
            });

            Assert.That(outcome.Path, Is.EqualTo(new[]
            {
                typeof(ContextualContractTestFixtures.SalesCheckout).FullName,
                typeof(ContextualContractTestFixtures.InventoryStockItem).FullName,
            }));
            Assert.That(outcome.ExpressionParticipation, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(outcome.ExpressionParticipation[0].ContractId, Is.EqualTo("cross-domain-when"));
                Assert.That(outcome.ExpressionParticipation[0].Source,
                    Is.EqualTo("target.metadataText[\"domain\"] != source.metadataText[\"domain\"]"));
                Assert.That(outcome.ExpressionParticipation[0].Result, Is.EqualTo(ExpressionParticipationResult.Matched));
            });
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(policyPath)!, true);
        }
    }

    [Test]
    public void Explain_NoCelInvolvement_ExpressionParticipationIsEmpty()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            layers:
              execution:
                namespace: ArchLinterNet.Core.Execution
              contracts:
                namespace: ArchLinterNet.Core.Contracts

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict:
                - id: no-execution-to-contracts
                  name: execution-must-not-depend-on-contracts
                  source: execution
                  forbidden: [contracts]
            """);

        try
        {
            using ArchitectureEngine engine = CreateEngine();

            ArchitectureExplainOutcome outcome = engine.Explain(new ArchitectureExplainRequest
            {
                PolicyPath = policyPath,
                Source = ExecutionNamespace,
                Target = ContractsNamespace,
            });

            Assert.That(outcome.ExpressionParticipation, Is.Empty);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(policyPath)!, true);
        }
    }

    [Test]
    public void Explain_AssemblyLevel_ThrowsArgumentException()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts: {}
            """);

        try
        {
            using ArchitectureEngine engine = CreateEngine();

            Assert.Throws<ArgumentException>(() => engine.Explain(new ArchitectureExplainRequest
            {
                PolicyPath = policyPath,
                Source = "ArchLinterNet.Core",
                Target = "ArchLinterNet.Core",
                Level = ArchitectureGraphLevel.Assembly,
            }));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(policyPath)!, true);
        }
    }
}
