using System.Collections.Generic;
using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Selected official CEL conformance fixtures, adapted from the pinned
/// <c>cel-expr/cel-spec</c> baseline's <c>tests/simple/testdata</c> suite (#324), scoped to the
/// subset of syntax and semantics Profile v1 actually supports.
///
/// Each fixture cites its upstream file/section/test name so provenance is auditable. Fixtures
/// that rely on excluded Profile v1 features (arithmetic, `uint`/`bytes` literals, `dyn()`,
/// list/map/message literal syntax, the free-function call form) are adapted to equivalent
/// Profile v1 constructs — e.g. list/map "in" membership fixtures use declared context variables
/// bound to list/map values instead of literal collection syntax, and `size(x)`/`'x'.startsWith(y)`
/// use the receiver-call form Profile v1 requires. Passing these fixtures does not by itself imply
/// full CEL conformance — see <see cref="CelParserDeferredFeatureTests"/> and
/// <see cref="CelBinderTests"/> for the negative/deferred-feature conformance surface.
/// </summary>
[TestFixture]
public sealed class CelOfficialConformanceFixtureTests
{
    private static readonly CelEnvironment _literalEnv =
        CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(CelContextSchema.CreateBuilder("literal-fixture").Build())
            .Build();

    private static bool EvaluatePredicate(string expression)
    {
        var compilation = _literalEnv.CompilePredicate(expression);
        Assert.That(compilation.IsSuccess, Is.True, $"Compilation failed for '{expression}': {string.Join("; ", compilation.Diagnostics)}");
        var context = _literalEnv.CreateEvaluationContextBuilder().Build();
        var result = compilation.Program!.Evaluate(context);
        Assert.That(result.IsSuccess, Is.True, $"Evaluation failed for '{expression}': {string.Join("; ", result.Diagnostics)}");
        return result.AsBool();
    }

    // ── logic.textproto: AND ────────────────────────────────────────────────────

    [TestCase("true && true", true, TestName = "logic.AND.all_true")]
    [TestCase("false && false", false, TestName = "logic.AND.all_false")]
    [TestCase("false && true", false, TestName = "logic.AND.false_left")]
    [TestCase("true && false", false, TestName = "logic.AND.false_right")]
    // ── logic.textproto: OR ─────────────────────────────────────────────────────
    [TestCase("true || true", true, TestName = "logic.OR.all_true")]
    [TestCase("false || false", false, TestName = "logic.OR.all_false")]
    [TestCase("false || true", true, TestName = "logic.OR.false_left")]
    [TestCase("true || false", true, TestName = "logic.OR.false_right")]
    // ── logic.textproto: NOT ────────────────────────────────────────────────────
    [TestCase("!true", false, TestName = "logic.NOT.not_true")]
    [TestCase("!false", true, TestName = "logic.NOT.not_false")]
    // ── comparisons.textproto: eq_literal (string subset; numeric/uint/dyn cases excluded) ──
    [TestCase("'' == ''", true, TestName = "comparisons.eq_literal.eq_string")]
    [TestCase("'a' == 'b'", false, TestName = "comparisons.eq_literal.not_eq_string")]
    [TestCase("'abc' == 'ABC'", false, TestName = "comparisons.eq_literal.not_eq_string_case")]
    [TestCase("'ίσος' == 'ίσος'", true, TestName = "comparisons.eq_literal.eq_string_unicode")]
    [TestCase("1 == 1", true, TestName = "comparisons.eq_literal.eq_int")]
    // ── comparisons.textproto: lt_literal (non-negative-literal, numeric-only subset) ──
    // Upstream also covers 'a' < 'b' string ordering — Profile v1's ordering operators
    // (<, <=, >, >=) are int/float-only (see CelBinder.BindOrdering); string ordering is a
    // deliberate narrowing from full CEL, verified as a rejection below rather than a fixture.
    [TestCase("0 < 1", true, TestName = "comparisons.lt_literal.lt_int")]
    [TestCase("0 < 0", false, TestName = "comparisons.lt_literal.not_lt_int")]
    // ── comparisons.textproto: lte_literal ───────────────────────────────────────
    [TestCase("0 <= 1", true, TestName = "comparisons.lte_literal.lte_int_lt")]
    [TestCase("1 <= 1", true, TestName = "comparisons.lte_literal.lte_int_eq")]
    // ── comparisons.textproto: gt_literal / gte_literal ─────────────────────────
    [TestCase("1 > 0", true, TestName = "comparisons.gt_literal.gt_int")]
    [TestCase("1 >= 1", true, TestName = "comparisons.gte_literal.gte_int_eq")]
    public void PredicateFixture_EvaluatesToExpectedResult(string expression, bool expected)
    {
        Assert.That(EvaluatePredicate(expression), Is.EqualTo(expected));
    }

    [Test]
    public void OrderingOperators_RejectStringOperands_UnlikeFullCel()
    {
        // comparisons.lt_literal.lt_string exists upstream ('a' < 'b' == true); Profile v1
        // narrows ordering operators to Int/Float only, so the equivalent Profile v1 expression
        // must fail to compile rather than silently evaluate.
        var compilation = _literalEnv.CompilePredicate("'a' < 'b'");
        Assert.That(compilation.IsSuccess, Is.False);
    }

    // ── string.textproto: starts_with / ends_with / contains ───────────────────
    // Adapted to the receiver-call form ('x'.startsWith(y)); Profile v1 has no free-function
    // call form (`startsWith(x, y)`).

    [TestCase("'foobar'.startsWith('foo')", true, TestName = "string.starts_with.basic_true")]
    [TestCase("'foobar'.startsWith('bar')", false, TestName = "string.starts_with.basic_false")]
    [TestCase("''.startsWith('foo')", false, TestName = "string.starts_with.empty_target")]
    [TestCase("'foobar'.startsWith('')", true, TestName = "string.starts_with.empty_arg")]
    [TestCase("''.startsWith('')", true, TestName = "string.starts_with.empty_empty")]
    [TestCase("'завтра'.startsWith('за')", true, TestName = "string.starts_with.unicode")]
    [TestCase("'foobar'.endsWith('bar')", true, TestName = "string.ends_with.basic_true")]
    [TestCase("'foobar'.endsWith('foo')", false, TestName = "string.ends_with.basic_false")]
    [TestCase("''.endsWith('foo')", false, TestName = "string.ends_with.empty_target")]
    [TestCase("'foobar'.endsWith('')", true, TestName = "string.ends_with.empty_arg")]
    [TestCase("''.endsWith('')", true, TestName = "string.ends_with.empty_empty")]
    [TestCase("'forté'.endsWith('té')", true, TestName = "string.ends_with.unicode")]
    [TestCase("'hello'.contains('he')", true, TestName = "string.contains.contains_true")]
    [TestCase("'hello'.contains('')", true, TestName = "string.contains.contains_empty")]
    [TestCase("'hello'.contains('ol')", false, TestName = "string.contains.contains_false")]
    [TestCase("'abababc'.contains('ababc')", true, TestName = "string.contains.contains_multiple")]
    [TestCase("'Straße'.contains('aß')", true, TestName = "string.contains.contains_unicode")]
    [TestCase("''.contains('something')", false, TestName = "string.contains.empty_contains")]
    [TestCase("''.contains('')", true, TestName = "string.contains.empty_empty")]
    public void StringFunctionFixture_EvaluatesToExpectedResult(string expression, bool expected)
    {
        Assert.That(EvaluatePredicate(expression), Is.EqualTo(expected));
    }

    // ── string.textproto: size (adapted to the receiver-call form: 'x'.size(), not size(x)) ──

    [TestCase("''", 0L, TestName = "string.size.empty")]
    [TestCase("'A'", 1L, TestName = "string.size.one_ascii")]
    [TestCase("'ÿ'", 1L, TestName = "string.size.one_unicode")]
    [TestCase("'four'", 4L, TestName = "string.size.ascii")]
    [TestCase("'πέντε'", 5L, TestName = "string.size.unicode")]
    public void SizeFixture_EvaluatesToExpectedLength(string literal, long expected)
    {
        var compilation = _literalEnv.Compile($"{literal}.size()");
        Assert.That(compilation.IsSuccess, Is.True);
        var context = _literalEnv.CreateEvaluationContextBuilder().Build();
        var result = compilation.Program!.Evaluate(context);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.AsInt(), Is.EqualTo(expected));
    }

    // ── comparisons.textproto: in_list_literal / in_map_literal ────────────────
    // Adapted to declared context variables bound to list/map values (mirroring cel-spec's own
    // "bound" section pattern for non-literal operands) instead of literal collection syntax,
    // which Profile v1 does not support (see CelParserDeferredFeatureTests).

    private static readonly CelEnvironment _membershipEnv = BuildMembershipEnvironment();

    private static CelEnvironment BuildMembershipEnvironment()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("membership-fixture");
        schemaBuilder.AddVariable("elems", CelType.ListOf(CelType.String));
        schemaBuilder.AddVariable("keys", CelType.MapOf(CelType.String));
        return CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schemaBuilder.Build())
            .Build();
    }

    private static bool EvaluateMembership(string expression, IReadOnlyList<CelValue> elems, IReadOnlyDictionary<string, CelValue> keys)
    {
        var compilation = _membershipEnv.CompilePredicate(expression);
        Assert.That(compilation.IsSuccess, Is.True, $"Compilation failed for '{expression}': {string.Join("; ", compilation.Diagnostics)}");
        var context = _membershipEnv.CreateEvaluationContextBuilder()
            .Set("elems", CelValue.List(elems))
            .Set("keys", CelValue.Map(keys))
            .Build();
        var result = compilation.Program!.Evaluate(context);
        Assert.That(result.IsSuccess, Is.True, $"Evaluation failed for '{expression}': {string.Join("; ", result.Diagnostics)}");
        return result.AsBool();
    }

    [Test]
    public void InListFixture_ElemNotInEmptyList()
    {
        // comparisons.in_list_literal.elem_not_in_empty_list
        Assert.That(EvaluateMembership("'empty' in elems", [], new Dictionary<string, CelValue>()), Is.False);
    }

    [Test]
    public void InListFixture_ElemInList()
    {
        // comparisons.in_list_literal.elem_in_list
        Assert.That(
            EvaluateMembership(
                "'elem' in elems",
                [CelValue.String("elem"), CelValue.String("elemA"), CelValue.String("elemB")],
                new Dictionary<string, CelValue>()),
            Is.True);
    }

    [Test]
    public void InListFixture_ElemNotInList()
    {
        // comparisons.in_list_literal.elem_not_in_list
        Assert.That(
            EvaluateMembership(
                "'not' in elems",
                [CelValue.String("elem1"), CelValue.String("elem2"), CelValue.String("elem3")],
                new Dictionary<string, CelValue>()),
            Is.False);
    }

    [Test]
    public void InMapFixture_KeyNotInEmptyMap()
    {
        // comparisons.in_map_literal.key_not_in_empty_map
        Assert.That(EvaluateMembership("'empty' in keys", [], new Dictionary<string, CelValue>()), Is.False);
    }

    [Test]
    public void InMapFixture_KeyInMap()
    {
        // comparisons.in_map_literal.key_in_map
        Assert.That(
            EvaluateMembership(
                "'key' in keys",
                [],
                new Dictionary<string, CelValue> { ["key"] = CelValue.String("1"), ["other"] = CelValue.String("2") }),
            Is.True);
    }

    [Test]
    public void InMapFixture_KeyNotInMap()
    {
        // comparisons.in_map_literal.key_not_in_map
        Assert.That(
            EvaluateMembership(
                "'key' in keys",
                [],
                new Dictionary<string, CelValue> { ["lock"] = CelValue.String("1"), ["gate"] = CelValue.String("2") }),
            Is.False);
    }
}
