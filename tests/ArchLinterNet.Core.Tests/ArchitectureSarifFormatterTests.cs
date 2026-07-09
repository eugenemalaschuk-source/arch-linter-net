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
    public void FormatResultAsSarif_SameRuleIdDifferentContractName_RulesAreDeduplicatedById()
    {
        // Dedup must key strictly on ruleId, not on (ruleId, contractName) — otherwise a policy
        // where the same id is reused with a slightly different name would produce two SARIF
        // rule entries sharing one id, which SARIF consumers can reject as invalid.
        var violations = new List<ArchitectureViolation>
        {
            new("My Contract", "my-rule", "Source.A", "Forbidden.Namespace", new[] { "ref1" }),
            new("My Contract (renamed)", "my-rule", "Source.B", "Forbidden.Namespace", new[] { "ref2" })
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
                Payload = new PackageDependencyPayload("legacy-json")
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
                Payload = new TypePlacementPayload(ExpectedTypeLocation: "MyApp.Correct")
            }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement logicalLocation = result.GetProperty("logicalLocations")[0];
        Assert.That(logicalLocation.GetProperty("kind").GetString(), Is.EqualTo("type"));
    }

    [Test]
    public void FormatResultAsSarif_MethodBodyIlViolation_LogicalLocationKindIsType()
    {
        // ArchitectureIlMethodBodyScanner produces DependencyDiagnostic instances (same as
        // namespace/layer violations) with ForbiddenNamespace == "method-body-il" and SourceType
        // set to a type's fully-qualified name, not a namespace or a file — the kind must reflect
        // that, not fall through to the DependencyDiagnostic default of "namespace".
        var violations = new List<ArchitectureViolation>
        {
            new("method-body-il-rule", "method-body-il-rule", "MyApp.Infrastructure.LegacyService", "method-body-il",
                new[] { "il 0012 (MyApp.Infrastructure.LegacyService.Run): Forbidden.Call -> Forbidden.Type.Call" })
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.That(result.TryGetProperty("locations", out _), Is.False);
        JsonElement logicalLocation = result.GetProperty("logicalLocations")[0];
        Assert.Multiple(() =>
        {
            Assert.That(logicalLocation.GetProperty("fullyQualifiedName").GetString(),
                Is.EqualTo("MyApp.Infrastructure.LegacyService"));
            Assert.That(logicalLocation.GetProperty("kind").GetString(), Is.EqualTo("type"));
        });
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
    public void FormatResultAsSarif_MixedViolationKindsAndCycles_OutputIsIndependentOfInputOrder()
    {
        var violationA = new ArchitectureViolation("a-contract", "a-rule", "Source.A", "Forbidden.A", new[] { "ref-a" })
        {
            Payload = new ExternalDependencyPayload("external-group")
        };
        var violationB = new ArchitectureViolation("b-contract", "b-rule", "Source.B", "Forbidden.B", new[] { "ref-b" });
        var cycle = "[m-rule] X -> Y -> X";

        string inOriginalOrder = _formatter.FormatResultAsSarif(
            "strict", new[] { violationB, violationA }, new[] { cycle }, "1.0.0");
        string inReversedOrder = _formatter.FormatResultAsSarif(
            "strict", new[] { violationA, violationB }, new[] { cycle }, "1.0.0");

        Assert.That(inOriginalOrder, Is.EqualTo(inReversedOrder));

        using var doc = JsonDocument.Parse(inOriginalOrder);
        JsonElement results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");
        var ruleIds = results.EnumerateArray().Select(r => r.GetProperty("ruleId").GetString()).ToArray();
        Assert.That(ruleIds, Is.EqualTo(new[] { "a-rule", "b-rule", "m-rule" }));
    }
}
