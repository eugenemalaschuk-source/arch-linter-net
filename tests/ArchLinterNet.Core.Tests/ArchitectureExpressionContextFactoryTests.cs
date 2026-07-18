using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Values;
using ArchLinterNet.Core.Execution.Expressions;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Coverage for the typed context factories and evaluation-result wrapper #163 provides for #164
// to consume (openspec/changes/core-cel-integration). Not wired into any selector/contract
// matching - this exercises the CelValue/CelEvaluationContext construction boundary directly.
[TestFixture]
public sealed class ArchitectureExpressionContextFactoryTests
{
    private static ArchitectureExpressionSubjectFacts SalesSubject => new(
        FullName: "Acme.Sales.Domain.SalesOrder",
        SimpleName: "SalesOrder",
        Namespace: "Acme.Sales.Domain",
        AssemblyName: "Acme.Sales",
        ProjectName: "Acme.Sales",
        Role: "DomainLayer",
        MetadataText: new Dictionary<string, string> { ["domain"] = "Sales" },
        MetadataBool: new Dictionary<string, bool>(),
        Kind: "class",
        IsAbstract: false,
        IsSealed: true,
        BaseTypeNames: Array.Empty<string>(),
        InterfaceTypeNames: Array.Empty<string>(),
        AttributeTypeNames: Array.Empty<string>(),
        SourcePaths: new[] { "Assets/Game/Client/Presentation/SalesOrder.cs" },
        SourceDirectoryPrefixes: new[] { "Assets", "Assets/Game", "Assets/Game/Client", "Assets/Game/Client/Presentation" });

    private static ArchitectureExpressionSubjectFacts InventorySubject => SalesSubject with
    {
        FullName = "Acme.Inventory.Domain.StockItem",
        SimpleName = "StockItem",
        Namespace = "Acme.Inventory.Domain",
        MetadataText = new Dictionary<string, string> { ["domain"] = "Inventory" }
    };

    private static ArchitectureExpressionDependencyFacts TypeReferenceDependency => new(
        Kind: "type_reference",
        ViaMethodBody: false,
        SourceMemberName: string.Empty,
        TargetMemberName: string.Empty);

    [Test]
    public void CreateSubjectValue_ProducesSchemaValidObjectValue()
    {
        CelValue value = ArchitectureExpressionContextFactory.CreateSubjectValue(SalesSubject);

        Assert.That(value.Kind, Is.EqualTo(CelValueKind.Object));
        CelObjectValue obj = value.AsObject();
        Assert.Multiple(() =>
        {
            Assert.That(obj.Members["fullName"].AsString(), Is.EqualTo(SalesSubject.FullName));
            Assert.That(obj.Members["metadataText"].AsMap()["domain"].AsString(), Is.EqualTo("Sales"));
            Assert.That(obj.Members["isSealed"].AsBool(), Is.True);
            Assert.That(obj.Members["sourceDirectoryPrefixes"].AsList(), Has.Count.EqualTo(4));
        });
    }

    [Test]
    public void CreateDependencyValue_ProducesSchemaValidObjectValue()
    {
        CelValue value = ArchitectureExpressionContextFactory.CreateDependencyValue(TypeReferenceDependency);

        Assert.That(value.Kind, Is.EqualTo(CelValueKind.Object));
        CelObjectValue obj = value.AsObject();
        Assert.That(obj.Members["kind"].AsString(), Is.EqualTo("type_reference"));
    }

    [Test]
    public void CreateSelectorContext_BuildsContextAcceptedByASelectorCompiledPredicate()
    {
        CelEnvironment environment = SelectorEnvironment();
        var compiled = environment.CompilePredicate(
            "subject.metadataText[\"domain\"] == \"Sales\"");
        Assert.That(compiled.IsSuccess, Is.True);

        var context = ArchitectureExpressionContextFactory.CreateSelectorContext(SalesSubject);
        var result = compiled.Program!.Evaluate(context);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.AsBool(), Is.True);
    }

    [Test]
    public void CreateContextualTargetContext_EvaluatesCrossDomainComparisonCorrectly()
    {
        CelEnvironment environment = ContextualTargetEnvironment();
        var compiled = environment.CompilePredicate(
            "source.metadataText[\"domain\"] != target.metadataText[\"domain\"]");
        Assert.That(compiled.IsSuccess, Is.True);

        var crossDomainContext = ArchitectureExpressionContextFactory.CreateContextualTargetContext(
            SalesSubject, InventorySubject, TypeReferenceDependency);
        var sameDomainContext = ArchitectureExpressionContextFactory.CreateContextualTargetContext(
            SalesSubject, SalesSubject, TypeReferenceDependency);

        Assert.Multiple(() =>
        {
            Assert.That(compiled.Program!.Evaluate(crossDomainContext).AsBool(), Is.True);
            Assert.That(compiled.Program!.Evaluate(sameDomainContext).AsBool(), Is.False);
        });
    }

    [Test]
    public void Evaluator_WrapsSuccessfulEvaluationAsMatch()
    {
        CelEnvironment environment = SelectorEnvironment();
        var compiled = environment.CompilePredicate("subject.role == \"DomainLayer\"");
        Assert.That(compiled.IsSuccess, Is.True);
        var context = ArchitectureExpressionContextFactory.CreateSelectorContext(SalesSubject);

        ArchitectureExpressionEvaluationResult result =
            ArchitectureExpressionEvaluator.Evaluate(compiled.Program!, context);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.False);
            Assert.That(result.IsMatch, Is.True);
        });
    }

    [Test]
    public void Evaluator_WrapsGuardedMissingKeyEvaluationAsNonMatchWhenGuarded()
    {
        CelEnvironment environment = SelectorEnvironment();
        var compiled = environment.CompilePredicate(
            "subject.metadataText.containsKey(\"missing\") && subject.metadataText[\"missing\"] == \"x\"");
        Assert.That(compiled.IsSuccess, Is.True);
        var context = ArchitectureExpressionContextFactory.CreateSelectorContext(SalesSubject);

        ArchitectureExpressionEvaluationResult result =
            ArchitectureExpressionEvaluator.Evaluate(compiled.Program!, context);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.False);
            Assert.That(result.IsMatch, Is.False);
        });
    }

    [Test]
    public void Evaluator_WrapsUnguardedMissingKeyEvaluationAsErrorNotNonMatch()
    {
        // Fail-closed per openspec/specs/cel-policy-model/spec.md's "Missing key during evaluation is
        // not treated as false" scenario: an unguarded out-of-range map access is a configuration/
        // evaluation error the caller must surface, never silently downgraded to an ordinary non-match.
        CelEnvironment environment = SelectorEnvironment();
        var compiled = environment.CompilePredicate("subject.metadataText[\"missing\"] == \"x\"");
        Assert.That(compiled.IsSuccess, Is.True);
        var context = ArchitectureExpressionContextFactory.CreateSelectorContext(SalesSubject);

        ArchitectureExpressionEvaluationResult result =
            ArchitectureExpressionEvaluator.Evaluate(compiled.Program!, context);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsError, Is.True);
            Assert.That(result.IsMatch, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        });
    }

    // Environments below mirror ArchitectureExpressionSchemas exactly (same profile/object schemas)
    // to compile predicates for these direct-factory tests without depending on internal statics
    // from a different module. Uses the loader end-to-end (ExpressionCompilationValidatorTests)
    // for coverage of the actual Contracts.Expressions.ArchitectureExpressionSchemas instances.
    private static CelEnvironment SelectorEnvironment()
    {
        return ArchLinterNet.Core.Contracts.Expressions.ArchitectureExpressionSchemas.SelectorEnvironment;
    }

    private static CelEnvironment ContextualTargetEnvironment()
    {
        return ArchLinterNet.Core.Contracts.Expressions.ArchitectureExpressionSchemas.ContextualTargetEnvironment;
    }
}
