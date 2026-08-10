using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Regression coverage for issue #443 (must_reside_in_namespaces glob-grammar semantics):
// glob-pattern matching, invalid-pattern/blank-entry rejection at load time, and composed
// (imported-fragment) policy paths. Split out of TypePlacementContractTests.cs to stay under the
// file-size lint gate.
public sealed partial class TypePlacementContractTests
{
    [Test]
    public void CheckTypePlacementContract_MustResideInNamespacesGlobPattern_MatchesMiddleSegment()
    {
        var contract = new ArchitectureTypePlacementContract
        {
            Name = "workers-in-module-glob",
            TypesMatching = new ArchitectureTypeMatcher
            {
                NameSuffix = "Worker",
                Namespace = "TypePlacementContractTestFixtures.Modules"
            },
            MustResideInNamespaces = new List<string> { "TypePlacementContractTestFixtures.Modules.*.Correct" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckTypePlacementContract(contract);

        Assert.That(violations.Any(v => v.SourceType.Contains("Modules.Orders.Correct.OrdersWorker", StringComparison.Ordinal)), Is.False);

        var otherContract = new ArchitectureTypePlacementContract
        {
            Name = "workers-in-module-glob-negative",
            TypesMatching = new ArchitectureTypeMatcher
            {
                NameSuffix = "Worker",
                Namespace = "TypePlacementContractTestFixtures.Modules"
            },
            MustResideInNamespaces = new List<string> { "TypePlacementContractTestFixtures.Modules.*.Elsewhere" }
        };
        var otherRunner = new ArchitectureContractRunner(CreateContext(), CreateDocument(otherContract));

        Assert.That(otherRunner.Session.CheckTypePlacementContract(otherContract)
            .Any(v => v.SourceType.Contains("Modules.Orders.Correct.OrdersWorker", StringComparison.Ordinal)), Is.True,
            "A glob pattern that does not resolve to the type's actual namespace must still report a violation.");
    }

    [Test]
    public void TypePlacement_MustResideInNamespacesBareWildcard_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_type_placement:
                - name: invalid-namespace-glob
                  types_matching:
                    name_suffix: Controller
                  must_reside_in_namespaces: ["*"]
                  reason: Bare wildcard is unsupported.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("invalid-namespace-glob"));
        Assert.That(ex.Message, Does.Contain("must_reside_in_namespaces"));
        Assert.That(ex.Message, Does.Contain("Bare wildcard"));
    }

    [Test]
    public void TypePlacement_MustResideInNamespacesBlankEntry_ThrowsActionableErrorInsteadOfSilentNoMatch()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_type_placement:
                - name: blank-namespace-entry
                  types_matching:
                    name_suffix: Controller
                  must_reside_in_namespaces: ["TypePlacementContractTestFixtures.Correct", " "]
                  reason: A blank entry must fail load, not silently no-match.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("blank-namespace-entry"));
        Assert.That(ex.Message, Does.Contain("must_reside_in_namespaces"));
        Assert.That(ex.Message, Does.Contain("blank"));
    }

    [Test]
    public void TypePlacement_ComposedPolicy_ValidGlobNamespaceInImportedFragment_LoadsSuccessfully()
    {
        string root = Path.Combine(_tempDir, "root.yml");
        File.WriteAllText(root, """
            version: 1
            name: Test
            imports:
              - fragment.yml
            layers: {}
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_type_placement: []
            """);
        File.WriteAllText(Path.Combine(_tempDir, "fragment.yml"), """
            contracts:
              strict_type_placement:
                - name: composed-glob-placement
                  types_matching:
                    name_suffix: Controller
                  must_reside_in_namespaces: [Example.Modules.*.Correct]
                  reason: Composed (imported) policy path regression for issue #443.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(root));
    }
}
