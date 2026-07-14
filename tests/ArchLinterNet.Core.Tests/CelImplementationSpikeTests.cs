using Cel;
using Google.Protobuf.Reflection;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Disposable spike validating telus-oss/cel-net as the CEL engine candidate (#166).
/// Remove once #163 lands real integration tests.
/// </summary>
[TestFixture]
[Category("Spike")]
public class CelImplementationSpikeTests
{
    private CelEnvironment _env = null!;

    [SetUp]
    public void SetUp()
    {
        // Empty FileDescriptor[] — no proto schema needed for primitive/CLR-type contexts.
        _env = new CelEnvironment(fileDescriptors: [], messageNamespace: string.Empty);
    }

    [Test]
    public void Primitive_string_equality_predicate_evaluates_correctly()
    {
        var variables = new Dictionary<string, object?> { ["role"] = "Domain" };

        var result = _env.Program("role == 'Domain'", variables);

        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void Logical_and_over_two_string_variables_evaluates_correctly()
    {
        var variables = new Dictionary<string, object?>
        {
            ["role"] = "Domain",
            ["assembly"] = "MyApp.Domain"
        };

        var result = _env.Program("role == 'Domain' && assembly.startsWith('MyApp')", variables);

        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void Map_variable_key_access_evaluates_correctly()
    {
        var variables = new Dictionary<string, object?>
        {
            ["metadata"] = new Dictionary<string, string> { ["domain"] = "Sales" }
        };

        var result = _env.Program("metadata['domain'] == 'Sales'", variables);

        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void Clr_object_property_access_evaluates_correctly()
    {
        var ctx = new LayerEvalContext { Role = "Application", Namespace = "MyApp.Application.UseCases" };
        var variables = new Dictionary<string, object?> { ["ctx"] = ctx };

        var result = _env.Program("ctx.Role == 'Application' && ctx.Namespace.startsWith('MyApp')", variables);

        Assert.That(result, Is.EqualTo(true));
    }

    [Test]
    public void Predicate_returns_false_when_condition_unmet()
    {
        var variables = new Dictionary<string, object?> { ["role"] = "Infrastructure" };

        var result = _env.Program("role == 'Domain'", variables);

        Assert.That(result, Is.EqualTo(false));
    }

    [Test]
    public void Compiled_delegate_returns_same_result_as_direct_program_call()
    {
        var variables = new Dictionary<string, object?> { ["role"] = "Domain" };
        const string expr = "role == 'Domain'";

        var direct = _env.Program(expr, variables);
        var compiled = _env.Compile(expr);
        var cached = compiled(variables);

        Assert.That(cached, Is.EqualTo(direct));
    }

    [Test]
    public void Compiled_delegate_is_reusable_across_variable_sets()
    {
        var compiled = _env.Compile("role == 'Domain'");

        var trueVars = new Dictionary<string, object?> { ["role"] = "Domain" };
        var falseVars = new Dictionary<string, object?> { ["role"] = "Infrastructure" };

        Assert.Multiple(() =>
        {
            Assert.That(compiled(trueVars), Is.EqualTo(true));
            Assert.That(compiled(falseVars), Is.EqualTo(false));
        });
    }

    [Test]
    public void Unknown_variable_produces_deterministic_cel_exception()
    {
        var variables = new Dictionary<string, object?>();

        // Library throws CelUndeclaredReferenceException (: CelException) — a deterministic,
        // typed error, not a silent pass or NullReferenceException.
        Assert.That(
            () => _env.Program("undeclared_var == 'x'", variables),
            Throws.InstanceOf<CelException>());
    }

    [Test]
    public void Re2_matches_built_in_evaluates_namespace_pattern()
    {
        var variables = new Dictionary<string, object?> { ["ns"] = "MyApp.Domain.Sales" };

        var result = _env.Program("ns.matches('MyApp\\\\.Domain\\\\..*')", variables);

        Assert.That(result, Is.EqualTo(true));
    }

    /// <summary>Minimal stand-in for a future Core LayerEvalContext.</summary>
    private sealed class LayerEvalContext
    {
        public string Role { get; init; } = string.Empty;
        public string Namespace { get; init; } = string.Empty;
    }
}
