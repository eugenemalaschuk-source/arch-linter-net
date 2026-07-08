using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureSarifFormatterTests
{
    private static readonly ArchitectureSarifFormatter _formatter = new();

    private static JsonElement Run(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string>? cycles = null)
    {
        string json = _formatter.FormatResultAsSarif(mode, violations, cycles ?? Array.Empty<string>(), "1.2.3");
        return JsonDocument.Parse(json).RootElement;
    }

    [Test]
    public void FormatResultAsSarif_Envelope_HasVersionSchemaAndToolName()
    {
        JsonElement root = Run("strict", Array.Empty<ArchitectureViolation>());

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("version").GetString(), Is.EqualTo("2.1.0"));
            Assert.That(root.TryGetProperty("$schema", out _), Is.True);
            JsonElement run = root.GetProperty("runs")[0];
            Assert.That(run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString(),
                Is.EqualTo("arch-linter-net"));
            Assert.That(run.GetProperty("tool").GetProperty("driver").GetProperty("version").GetString(),
                Is.EqualTo("1.2.3"));
        });
    }

    [Test]
    public void FormatResultAsSarif_NoViolationsOrCycles_ProducesEmptyResultsArray()
    {
        JsonElement root = Run("strict", Array.Empty<ArchitectureViolation>());

        JsonElement results = root.GetProperty("runs")[0].GetProperty("results");
        Assert.That(results.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public void FormatResultAsSarif_ContractWithId_UsesIdAsRuleId()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("My Contract", "my-rule", "Source.Type", "Forbidden.Namespace", new[] { "ref1" })
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("my-rule"));
    }

    [Test]
    public void FormatResultAsSarif_ContractWithoutId_UsesNormalizedNameAsRuleId()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("<configuration>", null, "Source.Type", "Forbidden.Namespace", new[] { "ref1" })
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("configuration"));
    }

    [Test]
    public void FormatResultAsSarif_DuplicateContractId_RulesAreDeduplicated()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("My Contract", "my-rule", "Source.A", "Forbidden.Namespace", new[] { "ref1" }),
            new("My Contract", "my-rule", "Source.B", "Forbidden.Namespace", new[] { "ref2" })
        };

        JsonElement root = Run("strict", violations);

        JsonElement rules = root.GetProperty("runs")[0].GetProperty("tool").GetProperty("driver").GetProperty("rules");
        Assert.That(rules.GetArrayLength(), Is.EqualTo(1));
        Assert.That(rules[0].GetProperty("id").GetString(), Is.EqualTo("my-rule"));
    }

    [Test]
    public void FormatResultAsSarif_StrictMode_LevelIsError()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", "my-rule", "Source.Type", "Forbidden.Namespace", new[] { "ref1" })
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.That(result.GetProperty("level").GetString(), Is.EqualTo("error"));
    }

    [Test]
    public void FormatResultAsSarif_AuditMode_LevelIsWarning()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", "my-rule", "Source.Type", "Forbidden.Namespace", new[] { "ref1" })
        };

        JsonElement root = Run("audit", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.That(result.GetProperty("level").GetString(), Is.EqualTo("warning"));
    }

    [Test]
    public void FormatResultAsSarif_MethodBodyViolation_IncludesPhysicalLocationWithLine()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("method-body-rule", "method-body-rule", "src/Foo.cs", "method-body",
                new[] { "line 42: Forbidden.Call -> Forbidden.Type.Call" })
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement location = result.GetProperty("locations")[0].GetProperty("physicalLocation");
        Assert.Multiple(() =>
        {
            Assert.That(location.GetProperty("artifactLocation").GetProperty("uri").GetString(), Is.EqualTo("src/Foo.cs"));
            Assert.That(location.GetProperty("region").GetProperty("startLine").GetInt32(), Is.EqualTo(42));
        });
    }

    [Test]
    public void FormatResultAsSarif_MethodBodyViolationUnparseableReference_OmitsRegionButKeepsUri()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("method-body-rule", "method-body-rule", "src/Foo.cs", "method-body",
                new[] { "not a line reference" })
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement location = result.GetProperty("locations")[0].GetProperty("physicalLocation");
        Assert.Multiple(() =>
        {
            Assert.That(location.GetProperty("artifactLocation").GetProperty("uri").GetString(), Is.EqualTo("src/Foo.cs"));
            Assert.That(location.TryGetProperty("region", out _), Is.False);
        });
    }

    [Test]
    public void FormatResultAsSarif_NonSourceViolation_IncludesLogicalLocation()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("layer-rule", "layer-rule", "MyApp.Web.Foo", "protected layer 'Core'", new[] { "ref1" })
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.That(result.TryGetProperty("locations", out _), Is.False);
        JsonElement logicalLocation = result.GetProperty("logicalLocations")[0];
        Assert.Multiple(() =>
        {
            Assert.That(logicalLocation.GetProperty("fullyQualifiedName").GetString(), Is.EqualTo("MyApp.Web.Foo"));
            Assert.That(logicalLocation.GetProperty("kind").GetString(), Is.EqualTo("namespace"));
        });
    }

    [Test]
    public void FormatResultAsSarif_PackageDependencyViolation_LogicalLocationKindIsPackage()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("package-rule", "package-rule", "MyApp.Csproj", "forbidden-packages", new[] { "Newtonsoft.Json" })
            {
                ForbiddenPackageGroup = "legacy-json"
            }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement logicalLocation = result.GetProperty("logicalLocations")[0];
        Assert.That(logicalLocation.GetProperty("kind").GetString(), Is.EqualTo("package"));
    }

    [Test]
    public void FormatResultAsSarif_TypePlacementViolation_LogicalLocationKindIsType()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("type-placement-rule", "type-placement-rule", "MyApp.Foo", "expected-location", new[] { "ref1" })
            {
                ExpectedTypeLocation = "MyApp.Correct"
            }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement logicalLocation = result.GetProperty("logicalLocations")[0];
        Assert.That(logicalLocation.GetProperty("kind").GetString(), Is.EqualTo("type"));
    }

    [Test]
    public void FormatResultAsSarif_CycleWithIdPrefix_UsesEmbeddedIdAsRuleIdAndStripsPrefixFromPath()
    {
        var cycles = new[] { "[cycle-check] A -> B -> A" };

        JsonElement root = Run("strict", Array.Empty<ArchitectureViolation>(), cycles);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("cycle-check"));
            Assert.That(result.GetProperty("logicalLocations")[0].GetProperty("fullyQualifiedName").GetString(),
                Is.EqualTo("A -> B -> A"));
        });
    }

    [Test]
    public void FormatResultAsSarif_CycleWithoutIdPrefix_UsesFallbackRuleId()
    {
        var cycles = new[] { "A -> B -> A" };

        JsonElement root = Run("strict", Array.Empty<ArchitectureViolation>(), cycles);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("dependency-cycle"));
    }

    [Test]
    public void FormatResultAsSarif_MixedViolationKindsAndCycles_DeterministicOrdering()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("b-contract", "b-rule", "Source.B", "Forbidden.B", new[] { "ref-b" }),
            new("a-contract", "a-rule", "Source.A", "Forbidden.A", new[] { "ref-a" })
            {
                ForbiddenExternalGroup = "external-group"
            }
        };
        var cycles = new[] { "[m-rule] X -> Y -> X" };

        string first = _formatter.FormatResultAsSarif("strict", violations, cycles, "1.0.0");
        string second = _formatter.FormatResultAsSarif("strict", violations, cycles, "1.0.0");

        Assert.That(first, Is.EqualTo(second));

        using var doc = JsonDocument.Parse(first);
        JsonElement results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");
        var ruleIds = results.EnumerateArray().Select(r => r.GetProperty("ruleId").GetString()).ToArray();
        Assert.That(ruleIds, Is.EqualTo(new[] { "a-rule", "b-rule", "m-rule" }));
    }
}
