using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Evaluation;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Tests for the compilation/evaluation limits model and cache-identity behavior:
/// budget diagnostics, limit validation, environment evaluation ceilings, and
/// <see cref="CelCompilationKey"/> equality regressions. Split out of
/// <c>CelApiShapeTests</c> to keep both files under the repository size threshold.
/// </summary>
[TestFixture]
public sealed class CelLimitsAndCacheIdentityTests
{
    // ── BudgetExceeded diagnostic ─────────────────────────────────────────────

    [Test]
    public void CelEnvironment_CompilePredicate_ExceedsMaxLength_ReturnsBudgetExceeded()
    {
        var tightLimits = new CelCompilationLimits(
            maxExpressionLength: 5, maxNestingDepth: 4, maxIdentifierCount: 4,
            maxTokenCount: 64, maxAstNodeCount: 64, maxLiteralSize: 64);
        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(BuildSimpleSchema())
            .WithCompilationLimits(tightLimits)
            .Build();

        var result = env.CompilePredicate("source == 'too_long_value'");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
    }

    [Test]
    public void CelEnvironment_Compile_ExceedsMaxLength_ReturnsBudgetExceeded()
    {
        var tightLimits = new CelCompilationLimits(
            maxExpressionLength: 3, maxNestingDepth: 4, maxIdentifierCount: 4,
            maxTokenCount: 64, maxAstNodeCount: 64, maxLiteralSize: 64);
        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(BuildSimpleSchema())
            .WithCompilationLimits(tightLimits)
            .Build();

        var result = env.Compile("1 + 2");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
    }

    [Test]
    public void CelCompilationLimits_ZeroOrNegativeArgs_ThrowArgumentOutOfRange()
    {
        Assert.That(() => new CelCompilationLimits(0, 4, 4, 4, 4, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => new CelCompilationLimits(4, 0, 4, 4, 4, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => new CelCompilationLimits(4, 4, 0, 4, 4, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => new CelCompilationLimits(4, 4, 4, 0, 4, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => new CelCompilationLimits(4, 4, 4, 4, 0, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => new CelCompilationLimits(4, 4, 4, 4, 4, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    // ── Cache identity regressions ────────────────────────────────────────────

    [Test]
    public void CelCompilationKey_SameSource_ProducesEqualKeys()
    {
        var env = BuildSimpleEnvironment();
        var r1 = env.CompilePredicate("source == 'x'");
        var r2 = env.CompilePredicate("source == 'x'");
        Assert.That(r1.CompilationKey, Is.EqualTo(r2.CompilationKey));
        Assert.That(r1.CompilationKey.GetHashCode(), Is.EqualTo(r2.CompilationKey.GetHashCode()));
    }

    [Test]
    public void CelCompilationKey_DifferentSource_ProducesDifferentKeys()
    {
        var env = BuildSimpleEnvironment();
        var r1 = env.CompilePredicate("source == 'x'");
        var r2 = env.CompilePredicate("source == 'y'");
        Assert.That(r1.CompilationKey, Is.Not.EqualTo(r2.CompilationKey));
    }

    [Test]
    public void CelCompilationKey_WhitespaceInStringLiteral_ProducesDifferentKeys()
    {
        // Regression: "source == 'x'" and "source == ' x'" must be distinct keys.
        // Earlier code normalized whitespace inside string literals, which was wrong.
        var env = BuildSimpleEnvironment();
        var r1 = env.CompilePredicate("source == 'x'");
        var r2 = env.CompilePredicate("source == ' x'");
        Assert.That(r1.CompilationKey, Is.Not.EqualTo(r2.CompilationKey));
    }

    [Test]
    public void CelContextSchema_Identity_CollisionSafe()
    {
        // Regression: schemas whose components concatenate to the same string under a naive
        // (non-length-prefixed) encoding must still produce distinct identities.
        // "x" + var "ab"  vs  "xa" + var "b"  → naive concat gives "xab" for both;
        // the length-prefixed encoding keeps them distinct.
        var builderA = CelContextSchema.CreateBuilder("x");
        builderA.AddVariable("ab", CelType.String);
        var schemaA = builderA.Build();

        var builderB = CelContextSchema.CreateBuilder("xa");
        builderB.AddVariable("b", CelType.String);
        var schemaB = builderB.Build();

        Assert.That(schemaA.Identity, Is.Not.EqualTo(schemaB.Identity));
    }

    // ── EvaluationLimits on environment ───────────────────────────────────────

    [Test]
    public void CelEnvironment_EvaluationLimits_DefaultsToSafeDefaults()
    {
        var env = BuildSimpleEnvironment();
        Assert.That(env.EvaluationLimits, Is.Not.Null);
        Assert.That(env.EvaluationLimits.MaxIterations, Is.GreaterThan(0));
        Assert.That(env.EvaluationLimits.MaxCostUnits, Is.GreaterThan(0));
    }

    [Test]
    public void CelEnvironmentBuilder_WithEvaluationLimits_SetsLimits()
    {
        var limits = new CelEvaluationLimits(maxIterations: 10, maxCostUnits: 1000);
        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(BuildSimpleSchema())
            .WithEvaluationLimits(limits)
            .Build();

        Assert.That(env.EvaluationLimits, Is.SameAs(limits));
    }

    [Test]
    public void CelCompilationKey_DiffersWhenEvaluationLimitsDiffer()
    {
        var schema = BuildSimpleSchema();
        var envA = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithEvaluationLimits(new CelEvaluationLimits(maxIterations: 10, maxCostUnits: 100))
            .Build();
        var envB = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schema)
            .WithEvaluationLimits(new CelEvaluationLimits(maxIterations: 500, maxCostUnits: 5000))
            .Build();

        var keyA = envA.CompilePredicate("true").CompilationKey;
        var keyB = envB.CompilePredicate("true").CompilationKey;

        // Compiled programs capture their environment's evaluation ceiling, so two environments
        // with different evaluation maximums must not share a cached program.
        Assert.That(keyA, Is.Not.EqualTo(keyB));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CelContextSchema BuildSimpleSchema()
    {
        var builder = CelContextSchema.CreateBuilder("test_v1");
        builder.AddVariable("source", CelType.String);
        return builder.Build();
    }

    private static CelEnvironment BuildSimpleEnvironment()
    {
        return CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(BuildSimpleSchema())
            .WithCompilationLimits(CelCompilationLimits.SafeDefaults)
            .Build();
    }
}
