using System.Reflection;
using ArchLinterNet.CEL;
using ArchLinterNet.CEL.Compilation;
using ArchLinterNet.CEL.Diagnostics;
using ArchLinterNet.CEL.Profile;
using ArchLinterNet.CEL.Schema;
using ArchLinterNet.CEL.Values;
using NUnit.Framework;

namespace ArchLinterNet.CEL.Tests;

/// <summary>
/// Tests for the binder and static type checker (#326): identifier/member/index/call resolution,
/// operator type checking against the frozen Profile v1 signature table, whole-AST binding, and
/// required-result-type enforcement. Exercised entirely through the public
/// <see cref="CelEnvironment.CompilePredicate"/>/<see cref="CelEnvironment.Compile"/> entry points.
/// </summary>
[TestFixture]
public sealed class CelBinderTests
{
    private static CelEnvironment BuildEnvironment(Action<CelEnvironmentBuilder>? configure = null)
    {
        var schemaBuilder = CelContextSchema.CreateBuilder("binder-tests-v1");
        schemaBuilder.AddVariable("x", CelType.Bool);
        schemaBuilder.AddVariable("s", CelType.String);
        schemaBuilder.AddVariable("i", CelType.Int);
        schemaBuilder.AddVariable("f", CelType.Float);
        schemaBuilder.AddVariable("list", CelType.ListOf(CelType.Int));
        schemaBuilder.AddVariable("map", CelType.MapOf(CelType.Bool));
        schemaBuilder.AddVariable("obj", CelType.ObjectOf("widget"));

        var objBuilder = CelObjectSchema.CreateBuilder("widget");
        objBuilder.AddMember("name", CelType.String);
        objBuilder.AddMember("count", CelType.Int);
        var objSchema = objBuilder.Build();

        var builder = CelEnvironment.CreateBuilder(CelProfile.V1)
            .WithContextSchema(schemaBuilder.Build())
            .WithObjectSchema(objSchema);
        configure?.Invoke(builder);
        return builder.Build();
    }

    // ── Identifier resolution ──────────────────────────────────────────────

    [Test]
    public void Identifier_Declared_Resolves()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("x");

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void Identifier_Undeclared_ProducesBindingErrorWithIdentifierName()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("undeclared");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BindingError));
        Assert.That(result.Diagnostics[0].Parameters["identifier"], Is.EqualTo("undeclared"));
    }

    // ── Member access resolution ───────────────────────────────────────────

    [Test]
    public void MemberAccess_DeclaredMember_Resolves()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("obj.name == 'a'");

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void MemberAccess_UndeclaredMember_ProducesSchemaMismatchWithMemberName()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("obj.missingMember == 'a'");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.SchemaMismatch));
        Assert.That(result.Diagnostics[0].Parameters["identifier"], Is.EqualTo("missingMember"));
    }

    [Test]
    public void MemberAccess_NonObjectReceiver_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("s.missingMember == 'a'");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    // ── Index resolution ────────────────────────────────────────────────────

    [Test]
    public void ListIndex_IntIndex_Resolves()
    {
        var env = BuildEnvironment();
        var result = env.Compile("list[0]");

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void ListIndex_WrongIndexType_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.Compile("list[s]");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    [Test]
    public void MapIndex_StringKey_Resolves()
    {
        var env = BuildEnvironment();
        var result = env.Compile("map[s]");

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void MapIndex_WrongKeyType_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.Compile("map[i]");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    [Test]
    public void Index_NonListMapReceiver_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.Compile("i[0]");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    // ── Function-catalog resolution ─────────────────────────────────────────

    [Test]
    public void Call_UnknownFunctionName_ProducesBindingError()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("s.unknownFunction()");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BindingError));
    }

    [TestCase("s.startsWith('a')")]
    [TestCase("s.endsWith('a')")]
    [TestCase("s.contains('a')")]
    [TestCase("map.containsKey('a')")]
    public void Call_KnownOverload_Resolves(string expression)
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate(expression);

        Assert.That(result.IsSuccess, Is.True);
    }

    [TestCase("s.size()")]
    [TestCase("list.size()")]
    [TestCase("map.size()")]
    public void Call_SizeOverload_ResolvesToInt(string expression)
    {
        var env = BuildEnvironment();
        var result = env.Compile(expression);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void Call_ContainsOnListReceiver_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("list.contains(0)");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    [Test]
    public void Call_SizeOnUnsupportedReceiver_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.Compile("i.size()");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    [Test]
    public void Call_WrongArgumentType_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("s.startsWith(1)");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    [Test]
    public void Call_WrongArity_ProducesBindingError()
    {
        var env = BuildEnvironment();
        var result = env.Compile("s.size('extra')");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BindingError));
    }

    // ── Operator type checking ──────────────────────────────────────────────

    [TestCase("!x")]
    [TestCase("x && x")]
    [TestCase("x || x")]
    [TestCase("x == x")]
    [TestCase("x != x")]
    [TestCase("s == s")]
    [TestCase("i == i")]
    [TestCase("f == f")]
    [TestCase("list == list")]
    [TestCase("map == map")]
    [TestCase("obj == obj")]
    [TestCase("i < i")]
    [TestCase("i <= i")]
    [TestCase("i > i")]
    [TestCase("i >= i")]
    [TestCase("f < f")]
    [TestCase("i in list")]
    [TestCase("s in map")]
    public void Operator_ValidSignature_Resolves(string expression)
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate(expression);

        Assert.That(result.IsSuccess, Is.True, result.Diagnostics.Count > 0 ? result.Diagnostics[0].Message : "");
    }

    [Test]
    public void Not_NonBoolOperand_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("!s");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    [TestCase("s && x")]
    [TestCase("x && s")]
    [TestCase("s || x")]
    public void Logical_NonBoolOperand_ProducesTypeMismatch(string expression)
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate(expression);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    [Test]
    public void Equality_CrossKindOperands_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("s == i");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    [Test]
    public void Ordering_MixedIntFloat_ProducesTypeMismatch_NoImplicitWidening()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("i < f");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    [Test]
    public void Ordering_NonNumericOperand_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("s < s");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    [Test]
    public void In_ListMembership_WrongElementType_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("s in list");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    [Test]
    public void In_MapMembership_NonStringKey_ProducesTypeMismatch()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("i in map");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
    }

    // ── Whole-AST binding ────────────────────────────────────────────────────

    [Test]
    public void WholeAstBinding_InvalidRightOperandOfDeterminingOr_StillFails()
    {
        // 'x' alone would make this predicate true via a determining OR operand under a future
        // evaluator's short-circuit/error-absorbing semantics, but the binder still resolves and
        // type-checks the whole tree — the undeclared identifier on the right must fail compilation.
        var env = BuildEnvironment();
        var result = env.CompilePredicate("x || undeclared");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BindingError));
    }

    [Test]
    public void WholeAstBinding_InvalidLeftOperandOfDeterminingAnd_StillFails()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("undeclared && x");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.BindingError));
    }

    [Test]
    public void WholeAstBinding_BothOperandsValid_Succeeds()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("x || (map[s])");

        Assert.That(result.IsSuccess, Is.True);
    }

    // ── Required result type ────────────────────────────────────────────────

    [Test]
    public void Predicate_NonBoolRoot_ProducesTypeMismatchWithExpectedBool()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("i");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics[0].Code, Is.EqualTo(CelDiagnosticCode.TypeMismatch));
        Assert.That(result.Diagnostics[0].Parameters["expectedType"], Is.EqualTo("Bool"));
    }

    [Test]
    public void General_NonBoolRoot_Succeeds()
    {
        var env = BuildEnvironment();
        var result = env.Compile("i");

        Assert.That(result.IsSuccess, Is.True);
    }

    // ── Diagnostic shape ─────────────────────────────────────────────────────

    [Test]
    public void BinderDiagnostic_CarriesBinderCategoryAndProfileId()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("undeclared");

        Assert.That(result.Diagnostics[0].Category, Is.EqualTo("binder"));
        Assert.That(result.Diagnostics[0].Parameters["profileId"], Is.EqualTo("arch-linter/cel/v1"));
    }

    [Test]
    public void TypeMismatchDiagnostic_CarriesExpectedAndActualType()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("s == i");

        Assert.That(result.Diagnostics[0].Parameters, Contains.Key("expectedType"));
        Assert.That(result.Diagnostics[0].Parameters, Contains.Key("actualType"));
    }

    // ── No evaluation enabled by successful binding ─────────────────────────

    [Test]
    public void SuccessfulCompilation_EvaluateStillThrowsNotImplemented()
    {
        var env = BuildEnvironment();
        var result = env.CompilePredicate("x");
        Assert.That(result.IsSuccess, Is.True);

        var context = env.CreateEvaluationContextBuilder()
            .Set(env.Schema.Variables.First(v => v.Name == "x"), CelValue.Bool(true))
            .Set(env.Schema.Variables.First(v => v.Name == "s"), CelValue.String("a"))
            .Set(env.Schema.Variables.First(v => v.Name == "i"), CelValue.Int(1))
            .Set(env.Schema.Variables.First(v => v.Name == "f"), CelValue.Float(1.0))
            .Set(env.Schema.Variables.First(v => v.Name == "list"), CelValue.List([CelValue.Int(1)]))
            .Set(env.Schema.Variables.First(v => v.Name == "map"), CelValue.Map(new Dictionary<string, CelValue> { ["a"] = CelValue.Bool(true) }))
            .Set(env.Schema.Variables.First(v => v.Name == "obj"), CelValue.Object(new CelObjectValue("widget", new Dictionary<string, CelValue> { ["name"] = CelValue.String("n"), ["count"] = CelValue.Int(1) })))
            .Build();

        Assert.That(() => result.Program!.Evaluate(context), Throws.TypeOf<NotImplementedException>());
    }

    // ── Public API surface ───────────────────────────────────────────────────

    [Test]
    public void PublicApi_NoBindingNamespaceTypeIsPublic()
    {
        var assembly = typeof(CelEnvironment).Assembly;
        var publicBindingTypes = assembly.GetTypes()
            .Where(t => t.IsPublic && t.Namespace == "ArchLinterNet.CEL.Binding")
            .ToList();

        Assert.That(publicBindingTypes, Is.Empty);
    }

    [Test]
    public void PublicApi_NoPublicMemberExposesBindingTypes()
    {
        var assembly = typeof(CelEnvironment).Assembly;
        var publicTypes = assembly.GetTypes().Where(t => t.IsPublic);

        foreach (var type in publicTypes)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var member in members)
            {
                var memberType = member switch
                {
                    PropertyInfo p => p.PropertyType,
                    MethodInfo m => m.ReturnType,
                    FieldInfo f => f.FieldType,
                    _ => null,
                };
                if (memberType is { Namespace: "ArchLinterNet.CEL.Binding" })
                {
                    Assert.Fail($"Public member {type.FullName}.{member.Name} exposes internal Binding type {memberType.FullName}.");
                }
            }
        }
    }
}
