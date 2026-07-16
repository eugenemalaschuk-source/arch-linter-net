using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Tests that <see cref="CelEnvironment.CompilePredicate"/> and <see cref="CelEnvironment.Compile"/>
/// run the tokenizer/parser (#325) after the <c>MaxExpressionLength</c> gate: syntax errors and
/// deferred-feature usage surface real, span-carrying diagnostics, and syntactically valid Profile
/// v1 expressions that also bind and type-check successfully (#326) now produce a real compiled
/// program instead of a stub.
/// </summary>
[TestFixture]
public sealed class CelEnvironmentParsingTests
{
    private static CelEnvironment BuildEnvironment()
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("parsing-tests-v1");
        schemaBuilder.AddVariable("source", CelType.String);
        return CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schemaBuilder.Build())
            .Build();
    }

    [Test]
    public void CompilePredicate_SyntacticallyInvalidExpression_ReturnsSyntaxErrorWithSpan()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("source ===");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
        Assert.That(result.Diagnostics[0].Span, Is.Not.Null);
    }

    [Test]
    public void CompilePredicate_ArithmeticExpression_ReturnsUnsupportedFeature()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("source + 'x' == 'y'");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.UnsupportedFeature));
    }

    [Test]
    public void CompilePredicate_SyntacticallyValidExpression_BindsAndSucceeds()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("source == 'x' && source.startsWith('y')");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Program, Is.Not.Null);
        Assert.That(result.Diagnostics, Has.Count.EqualTo(0));
    }

    [Test]
    public void Compile_SyntacticallyInvalidExpression_ReturnsSyntaxErrorWithSpan()
    {
        var env = BuildEnvironment();
        var result = env.Compile(")");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
        Assert.That(result.Diagnostics[0].Span, Is.Not.Null);
    }

    [Test]
    public void Compile_SyntacticallyValidExpression_BindsAndSucceeds()
    {
        var env = BuildEnvironment();
        var result = env.Compile("source");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Program, Is.Not.Null);
        Assert.That(result.Diagnostics, Has.Count.EqualTo(0));
    }

    [Test]
    public void CompilePredicate_ChainedComparison_ReturnsSyntaxError()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("source < 'a' < 'b'");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.SyntaxError));
    }

    [Test]
    public void CompilePredicate_MaxExpressionLengthGate_RunsBeforeParsing()
    {
        var tightLimits = new CelCompilationLimits(
            maxExpressionLength: 3, maxNestingDepth: 4, maxIdentifierCount: 4,
            maxTokenCount: 64, maxAstNodeCount: 64, maxLiteralSize: 64);
        var schemaBuilder = CelContextSchema.CreateBuilder("s");
        schemaBuilder.AddVariable("x", CelType.String);
        var env = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schemaBuilder.Build())
            .WithCompilationLimits(tightLimits)
            .Build();

        // Syntactically invalid AND over the length limit — length gate must win (cheapest first).
        var result = env.CompilePredicate("x ===");

        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BudgetExceeded));
        Assert.That(result.Diagnostics[0].Parameters["limitName"], Is.EqualTo("MaxExpressionLength"));
    }

    [Test]
    public void CompilePredicate_ParserDiagnostic_CarriesProfileIdParameter()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("source ===");

        Assert.That(result.Diagnostics[0].Parameters["profileId"], Is.EqualTo("arch-linter/cel/v1"));
    }
}
