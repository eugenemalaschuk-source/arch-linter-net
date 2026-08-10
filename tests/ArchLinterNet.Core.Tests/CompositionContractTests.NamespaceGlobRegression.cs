using System.Linq;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Regression coverage for issue #443 (allowed_only_in_namespaces glob-grammar semantics):
// glob-pattern matching, invalid-pattern/blank-entry rejection at load time, and composed
// (imported-fragment) policy paths. Split out of CompositionContractTests.cs to stay under the
// file-size lint gate.
public sealed partial class CompositionContractTests
{
    [Test]
    public void CheckCompositionContract_AllowedOnlyInNamespacesGlobPattern_MatchesMiddleSegment()
    {
        var contract = new ArchitectureCompositionContract
        {
            Name = "glob-namespace-composition-boundary",
            ForbiddenApis = new List<string> { GetServiceApi },
            AllowedOnlyInNamespaces = new List<string> { "CompositionContractTestFixtures.Modules.*.Composition" }
        };
        var document = CreateDocument(contract);
        var runner = new ArchitectureContractRunner(CreateContext(), document);

        var violations = runner.Session.CheckCompositionContract(contract);

        Assert.That(violations.Any(v =>
            v.SourceType == "CompositionContractTestFixtures.Modules.Orders.Composition.OrdersCompositionRoot"), Is.False,
            "The glob pattern must match the resolved 'Orders' segment and treat the call site as inside the boundary.");
        Assert.That(violations.Any(v =>
            v.SourceType == "CompositionContractTestFixtures.Application.ServiceLocatorLeak"), Is.True,
            "A namespace the glob pattern does not match must still be reported.");
    }

    [Test]
    public void Composition_AllowedOnlyInNamespacesPartialSegmentWildcard_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_composition:
                - name: invalid-namespace-glob
                  forbidden_apis: [System.IServiceProvider.GetService]
                  allowed_only_in_namespaces: [Example.Modules.*Bad]
                  reason: Partial segment wildcard is unsupported.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("invalid-namespace-glob"));
        Assert.That(ex.Message, Does.Contain("allowed_only_in_namespaces"));
        Assert.That(ex.Message, Does.Contain("Partial segment"));
    }

    [Test]
    public void Composition_AllowedOnlyInNamespacesBlankEntry_ThrowsActionableErrorInsteadOfCrashingAtScanTime()
    {
        // Regression: a blank entry must not silently reach NamespaceGlobPattern.Parse during
        // analysis (which would throw mid-scan instead of at load) or silently become a permanent
        // dead pattern that never matches anything.
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_composition:
                - name: blank-namespace-entry
                  forbidden_apis: [System.IServiceProvider.GetService]
                  allowed_only_in_namespaces: ["Example.Domain", "   "]
                  reason: A blank entry must fail load, not silently no-match.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("blank-namespace-entry"));
        Assert.That(ex.Message, Does.Contain("allowed_only_in_namespaces"));
        Assert.That(ex.Message, Does.Contain("blank"));
    }

    [Test]
    public void Composition_AllowedOnlyInNamespacesEmptyStringEntry_ThrowsActionableErrorAtLoadTime()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test
            analysis:
              target_assemblies: [ArchLinterNet.Core]
            contracts:
              strict_composition:
                - name: empty-namespace-entry
                  forbidden_apis: [System.IServiceProvider.GetService]
                  allowed_only_in_namespaces: [""]
                  reason: An empty-string entry must fail load, not crash mid-scan.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(policyPath))!;

        Assert.That(ex.Message, Does.Contain("empty-namespace-entry"));
    }

    [Test]
    public void Composition_ComposedPolicy_ValidGlobNamespaceInImportedFragment_LoadsSuccessfully()
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
              strict_composition: []
            """);
        File.WriteAllText(Path.Combine(_tempDir, "fragment.yml"), """
            contracts:
              strict_composition:
                - name: composed-glob-boundary
                  forbidden_apis: [System.IServiceProvider.GetService]
                  allowed_only_in_namespaces: [Example.Modules.*.Composition]
                  reason: Composed (imported) policy path regression for issue #443.
            """);

        Assert.DoesNotThrow(() => new ArchitecturePolicyDocumentLoader().Load(root));
    }

    [Test]
    public void Composition_ComposedPolicy_InvalidNamespaceGlobInImportedFragment_ThrowsActionableErrorIdentifyingFragment()
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
              strict_composition: []
            """);
        File.WriteAllText(Path.Combine(_tempDir, "fragment.yml"), """
            contracts:
              strict_composition:
                - name: composed-invalid-glob
                  forbidden_apis: [System.IServiceProvider.GetService]
                  allowed_only_in_namespaces: [Example.Modules.*Bad]
                  reason: Composed (imported) policy path regression for issue #443.
            """);

        // Composed (imported) policies wrap the InvalidOperationException in
        // ArchitecturePolicyValidationException - Assert.Catch matches by assignability, unlike
        // Assert.Throws's exact-type check (see ExpressionCompilationValidatorLocationRegressionTests).
        InvalidOperationException ex = Assert.Catch<InvalidOperationException>(() =>
            new ArchitecturePolicyDocumentLoader().Load(root))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Message, Does.Contain("composed-invalid-glob"));
            Assert.That(ex.Message, Does.Contain("Partial segment"));
            Assert.That(ex.Message, Does.Contain("fragment.yml"));
        });
    }
}
