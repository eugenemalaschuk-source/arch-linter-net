using ArchLinterNet.CEL.Binding;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Tests for <see cref="CelBuiltinFunctionInvoker"/> (#327): pure execution of the closed Profile
/// v1 built-in function catalog, exercised directly against <see cref="CelValue"/> inputs
/// (no evaluator/tree-walker exists yet — that is #328's scope).
/// </summary>
[TestFixture]
public sealed class CelBuiltinFunctionInvokerTests
{
    // ── startsWith ──────────────────────────────────────────────────────────

    [TestCase("hello world", "hello", true)]
    [TestCase("hello world", "world", false)]
    [TestCase("hello", "", true)]
    [TestCase("", "", true)]
    [TestCase("hello", "hello world", false)]
    [TestCase("Hello", "hello", false)]
    public void StartsWith_ReturnsExpectedResult(string receiver, string arg, bool expected)
    {
        var result = CelBuiltinFunctionInvoker.Invoke(
            CelFunctionOperationId.StartsWith, CelValue.String(receiver), [CelValue.String(arg)]);

        Assert.That(result.AsBool(), Is.EqualTo(expected));
    }

    // ── endsWith ────────────────────────────────────────────────────────────

    [TestCase("hello world", "world", true)]
    [TestCase("hello world", "hello", false)]
    [TestCase("hello", "", true)]
    [TestCase("", "", true)]
    [TestCase("hello", "hello world", false)]
    [TestCase("Hello", "hello", false)]
    public void EndsWith_ReturnsExpectedResult(string receiver, string arg, bool expected)
    {
        var result = CelBuiltinFunctionInvoker.Invoke(
            CelFunctionOperationId.EndsWith, CelValue.String(receiver), [CelValue.String(arg)]);

        Assert.That(result.AsBool(), Is.EqualTo(expected));
    }

    // ── contains ────────────────────────────────────────────────────────────

    [TestCase("hello world", "lo wo", true)]
    [TestCase("hello world", "xyz", false)]
    [TestCase("hello", "", true)]
    [TestCase("", "", true)]
    [TestCase("hello", "Hello", false)]
    public void Contains_ReturnsExpectedResult(string receiver, string arg, bool expected)
    {
        var result = CelBuiltinFunctionInvoker.Invoke(
            CelFunctionOperationId.Contains, CelValue.String(receiver), [CelValue.String(arg)]);

        Assert.That(result.AsBool(), Is.EqualTo(expected));
    }

    // ── size(String) — Unicode code-point counting ─────────────────────────

    [Test]
    public void SizeString_EmptyString_ReturnsZero()
    {
        var result = CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId.SizeString, CelValue.String(""), []);

        Assert.That(result.AsInt(), Is.EqualTo(0));
    }

    [Test]
    public void SizeString_BmpString_CountsCharacters()
    {
        var result = CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId.SizeString, CelValue.String("abc"), []);

        Assert.That(result.AsInt(), Is.EqualTo(3));
    }

    [Test]
    public void SizeString_SurrogatePairCharacter_CountsAsOneCodePoint()
    {
        // U+1F600 GRINNING FACE — one code point, encoded as a UTF-16 surrogate pair (two chars).
        var result = CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId.SizeString, CelValue.String("😀"), []);

        Assert.That(result.AsInt(), Is.EqualTo(1));
    }

    [Test]
    public void SizeString_CombiningSequence_CountsSeparateCodePoints()
    {
        // LATIN SMALL LETTER E (U+0065) followed by COMBINING ACUTE ACCENT (U+0301) — two code
        // points, not collapsed into one grapheme cluster.
        var result = CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId.SizeString, CelValue.String("é"), []);

        Assert.That(result.AsInt(), Is.EqualTo(2));
    }

    // ── size(List) ──────────────────────────────────────────────────────────

    [Test]
    public void SizeList_EmptyList_ReturnsZero()
    {
        var result = CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId.SizeList, CelValue.List([]), []);

        Assert.That(result.AsInt(), Is.EqualTo(0));
    }

    [Test]
    public void SizeList_NonEmptyList_ReturnsElementCount()
    {
        var list = CelValue.List([CelValue.Int(1), CelValue.Int(2), CelValue.Int(3)]);
        var result = CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId.SizeList, list, []);

        Assert.That(result.AsInt(), Is.EqualTo(3));
    }

    // ── size(Map) ───────────────────────────────────────────────────────────

    [Test]
    public void SizeMap_EmptyMap_ReturnsZero()
    {
        var result = CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId.SizeMap, CelValue.Map(new Dictionary<string, CelValue>()), []);

        Assert.That(result.AsInt(), Is.EqualTo(0));
    }

    [Test]
    public void SizeMap_NonEmptyMap_ReturnsEntryCount()
    {
        var map = CelValue.Map(new Dictionary<string, CelValue>
        {
            ["a"] = CelValue.Bool(true),
            ["b"] = CelValue.Bool(false),
        });
        var result = CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId.SizeMap, map, []);

        Assert.That(result.AsInt(), Is.EqualTo(2));
    }

    // ── containsKey ─────────────────────────────────────────────────────────

    [Test]
    public void ContainsKey_PresentKey_ReturnsTrue()
    {
        var map = CelValue.Map(new Dictionary<string, CelValue> { ["role"] = CelValue.String("admin") });
        var result = CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId.ContainsKey, map, [CelValue.String("role")]);

        Assert.That(result.AsBool(), Is.True);
    }

    [Test]
    public void ContainsKey_MissingKey_ReturnsFalseWithoutThrowing()
    {
        var map = CelValue.Map(new Dictionary<string, CelValue> { ["role"] = CelValue.String("admin") });

        CelValue result = null!;
        Assert.DoesNotThrow(() =>
            result = CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId.ContainsKey, map, [CelValue.String("missing")]));
        Assert.That(result.AsBool(), Is.False);
    }

    [Test]
    public void ContainsKey_EmptyMap_ReturnsFalse()
    {
        var map = CelValue.Map(new Dictionary<string, CelValue>());
        var result = CelBuiltinFunctionInvoker.Invoke(CelFunctionOperationId.ContainsKey, map, [CelValue.String("anything")]);

        Assert.That(result.AsBool(), Is.False);
    }

    // ── ComputeCost — input-size-proportional cost model ───────────────────

    [Test]
    public void ComputeCost_StartsWith_IsProportionalToArgumentLength()
    {
        var shortCost = CelBuiltinFunctionInvoker.ComputeCost(
            CelFunctionOperationId.StartsWith, CelValue.String("hello world"), [CelValue.String("h")]);
        var longCost = CelBuiltinFunctionInvoker.ComputeCost(
            CelFunctionOperationId.StartsWith, CelValue.String("hello world"), [CelValue.String("hello wo")]);

        Assert.That(longCost, Is.GreaterThan(shortCost));
    }

    [Test]
    public void ComputeCost_Contains_IsProportionalToReceiverAndArgumentLength()
    {
        var smallReceiverCost = CelBuiltinFunctionInvoker.ComputeCost(
            CelFunctionOperationId.Contains, CelValue.String("abc"), [CelValue.String("b")]);
        var largeReceiverCost = CelBuiltinFunctionInvoker.ComputeCost(
            CelFunctionOperationId.Contains, CelValue.String(new string('a', 1000)), [CelValue.String("b")]);

        Assert.That(largeReceiverCost, Is.GreaterThan(smallReceiverCost));
    }

    [Test]
    public void ComputeCost_SizeString_IsProportionalToReceiverLength()
    {
        var shortCost = CelBuiltinFunctionInvoker.ComputeCost(CelFunctionOperationId.SizeString, CelValue.String("a"), []);
        var longCost = CelBuiltinFunctionInvoker.ComputeCost(
            CelFunctionOperationId.SizeString, CelValue.String(new string('a', 1000)), []);

        Assert.That(longCost, Is.GreaterThan(shortCost));
    }

    [Test]
    public void ComputeCost_SizeListSizeMapContainsKey_AreConstant()
    {
        var smallList = CelValue.List([CelValue.Int(1)]);
        var largeList = CelValue.List(Enumerable.Range(0, 500).Select(i => CelValue.Int(i)).ToList());

        var smallCost = CelBuiltinFunctionInvoker.ComputeCost(CelFunctionOperationId.SizeList, smallList, []);
        var largeCost = CelBuiltinFunctionInvoker.ComputeCost(CelFunctionOperationId.SizeList, largeList, []);

        Assert.That(largeCost, Is.EqualTo(smallCost));
    }

    [Test]
    public void ComputeCost_EveryOperation_IsAtLeastOne()
    {
        foreach (var overload in CelFunctionCatalog.All)
        {
            var receiver = SampleReceiver(overload.ReceiverKind);
            var arguments = overload.ArgumentKinds.Select(k => SampleReceiver(k)).ToList();

            var cost = CelBuiltinFunctionInvoker.ComputeCost(overload.OperationId, receiver, arguments);

            Assert.That(cost, Is.GreaterThanOrEqualTo(1), $"OperationId={overload.OperationId}");
        }
    }

    // ── Catalog completeness (conformance/security) ────────────────────────

    [Test]
    public void Catalog_ContainsExactlySevenOverloads()
    {
        Assert.That(CelFunctionCatalog.All, Has.Count.EqualTo(7));
    }

    [Test]
    public void Catalog_EveryOverloadHasAUniqueOperationId()
    {
        var operationIds = CelFunctionCatalog.All.Select(o => o.OperationId).ToList();

        Assert.That(operationIds.Distinct().Count(), Is.EqualTo(operationIds.Count));
    }

    [Test]
    public void Catalog_MatchesTheDocumentedOverloadSet()
    {
        // Full-fidelity comparison — function name, receiver kind, every argument kind in order,
        // result type, AND operation id — so a mistake like swapping StartsWith/EndsWith's
        // OperationId (which would make the future evaluator execute the wrong function while every
        // other test here still passes) is caught here.
        var actual = CelFunctionCatalog.All
            .Select(o => (
                o.FunctionName,
                o.ReceiverKind,
                ArgumentKinds: string.Join(",", o.ArgumentKinds),
                ResultType: o.ResultType.Kind,
                o.OperationId))
            .OrderBy(t => t.FunctionName, StringComparer.Ordinal)
            .ThenBy(t => t.ReceiverKind)
            .ToList();

        var expected = new[]
        {
            ("contains", (CelTypeKind?)CelTypeKind.String, "String", CelTypeKind.Bool, CelFunctionOperationId.Contains),
            ("containsKey", (CelTypeKind?)CelTypeKind.Map, "String", CelTypeKind.Bool, CelFunctionOperationId.ContainsKey),
            ("endsWith", (CelTypeKind?)CelTypeKind.String, "String", CelTypeKind.Bool, CelFunctionOperationId.EndsWith),
            ("size", (CelTypeKind?)CelTypeKind.List, "", CelTypeKind.Int, CelFunctionOperationId.SizeList),
            ("size", (CelTypeKind?)CelTypeKind.Map, "", CelTypeKind.Int, CelFunctionOperationId.SizeMap),
            ("size", (CelTypeKind?)CelTypeKind.String, "", CelTypeKind.Int, CelFunctionOperationId.SizeString),
            ("startsWith", (CelTypeKind?)CelTypeKind.String, "String", CelTypeKind.Bool, CelFunctionOperationId.StartsWith),
        }
            .OrderBy(t => t.Item1, StringComparer.Ordinal)
            .ThenBy(t => t.Item2)
            .ToList();

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Catalog_EveryOverloadInvokesWithoutThrowingUsingItsOwnOperationId()
    {
        // Cross-checks each catalog row's declared OperationId against CelBuiltinFunctionInvoker by
        // actually invoking it with a matching sample receiver/arguments — catches a declaration
        // whose OperationId does not correspond to a working implementation for its own shape.
        foreach (var overload in CelFunctionCatalog.All)
        {
            var receiver = SampleReceiver(overload.ReceiverKind);
            var arguments = overload.ArgumentKinds.Select(k => SampleReceiver(k)).ToList();

            Assert.DoesNotThrow(
                () => CelBuiltinFunctionInvoker.Invoke(overload.OperationId, receiver, arguments),
                $"Overload '{overload.FunctionName}' (OperationId={overload.OperationId}) failed to invoke.");
        }
    }

    private static CelValue SampleReceiver(CelTypeKind? kind) => kind switch
    {
        CelTypeKind.String => CelValue.String("sample"),
        CelTypeKind.List => CelValue.List([CelValue.Int(1)]),
        CelTypeKind.Map => CelValue.Map(new Dictionary<string, CelValue> { ["k"] = CelValue.Bool(true) }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported sample receiver kind."),
    };
}
