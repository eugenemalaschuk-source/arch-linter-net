using System.Text.Json;
using System.Text.Json.Nodes;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed partial class ArchitectureSarifFormatterTests
{
    private static readonly string[] _value = { "line 42: Forbidden.Call -> Forbidden.Type.Call" };
    private static readonly string[] _value1 = { "not a line reference" };
    private static readonly string[] _value2 = { "Newtonsoft.Json" };
    private static readonly string[] _value3 = { "Microsoft.AspNetCore.App" };
    private static readonly string[] _value4 = { "Microsoft.AspNetCore.App" };
    private static readonly string[] _value5 = { "approved_core" };
    private static readonly string[] _value6 = { "il 0012 (MyApp.Infrastructure.LegacyService.Run): Forbidden.Call -> Forbidden.Type.Call" };
    private static readonly string[] _value7 = { "ref-a" };
    private static readonly string[] _value8 = { "ref-b" };
    private static readonly string[] _value9 = { "a-rule", "b-rule", "m-rule" };
    private static readonly ArchitectureSarifFormatter _formatter = new();
    private static readonly string[] _ref1 = { "ref1" };
    private static readonly string[] _ref2 = { "ref2" };
    private static readonly string[] _newtonsoftJsonReference = { "Newtonsoft.Json" };
    private static readonly string[] _approvedInfraGroup = { "approved_infra" };

    private static JsonElement Run(
        string mode,
        IReadOnlyCollection<ArchitectureViolation> violations,
        IReadOnlyCollection<string>? cycles = null)
    {
        string json = _formatter.FormatResultAsSarif(mode, violations, cycles ?? Array.Empty<string>(), "1.2.3");
        return JsonDocument.Parse(json).RootElement;
    }

    private static JsonElement LogicalLocationOf(JsonElement result) =>
        result.GetProperty("locations")[0].GetProperty("logicalLocations")[0];

    [Test]
    public void BaselineComparisonSarif_ExposesStatusAndCanonicalIdentity()
    {
        var identity = new ArchitectureViolationIdentity(
            2, "method_body", "call", "rule", "App", "App.Service", "Run",
            "Infra", "Infra.Client", "Call", 1);
        var entry = new ArchitectureBaselineComparisonEntry(
            "strict_method_body", "rule", "App.Service", "Infra.Client.Call", "debt", identity);

        string json = ArchitectureBaselineSarifFormatter.Format(
            [new BaselineLifecycleEntry(entry, BaselineEntryLifecycle.New)], "1.2.3");
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement properties = result.GetProperty("properties");

        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("rule"));
            Assert.That(properties.GetProperty("baseline_status").GetString(), Is.EqualTo("new"));
            Assert.That(properties.GetProperty("identity_version").GetInt32(), Is.EqualTo(2));
            Assert.That(properties.GetProperty("occurrence").GetInt32(), Is.EqualTo(1));
            Assert.That(properties.GetProperty("source_assembly").GetString(), Is.EqualTo("App"));
            Assert.That(result.TryGetProperty("logicalLocations", out _), Is.False);
            Assert.That(result.GetProperty("locations")[0].GetProperty("logicalLocations")[0]
                .GetProperty("fullyQualifiedName").GetString(), Is.EqualTo("App.Service"));
            JsonElement normalized = properties.GetProperty("arch_linter_net");
            Assert.That(normalized.GetProperty("schema_version").GetInt32(), Is.EqualTo(ArchitectureFinding.CurrentSchemaVersion));
            Assert.That(normalized.GetProperty("kind").GetString(), Is.EqualTo("baseline"));
            Assert.That(normalized.GetProperty("baseline_state").GetString(), Is.EqualTo("new"));
            Assert.That(normalized.GetProperty("details").GetProperty("identity").GetProperty("occurrence").GetInt32(), Is.EqualTo(1));
        });
    }

    [Test]
    public void BaselineComparisonSarif_OrdersEqualDisplayEntriesByCanonicalIdentity()
    {
        ArchitectureBaselineComparisonEntry first = CreateBaselineComparisonEntry(occurrence: 1);
        ArchitectureBaselineComparisonEntry second = CreateBaselineComparisonEntry(occurrence: 0);
        string firstOrder = ArchitectureBaselineSarifFormatter.Format(
            [new BaselineLifecycleEntry(first, BaselineEntryLifecycle.New), new BaselineLifecycleEntry(second, BaselineEntryLifecycle.New)], "1.2.3");
        string secondOrder = ArchitectureBaselineSarifFormatter.Format(
            [new BaselineLifecycleEntry(second, BaselineEntryLifecycle.New), new BaselineLifecycleEntry(first, BaselineEntryLifecycle.New)], "1.2.3");

        Assert.That(firstOrder, Is.EqualTo(secondOrder));
    }

    [Test]
    public void BaselineComparisonSarif_OrdersLegacyEntriesByContractGroup()
    {
        var strict = new ArchitectureBaselineComparisonEntry("strict", "rule", "Source", "Forbidden", "debt");
        var audit = strict with { ContractGroup = "audit" };

        string firstOrder = ArchitectureBaselineSarifFormatter.Format(
            [new BaselineLifecycleEntry(strict, BaselineEntryLifecycle.Matched), new BaselineLifecycleEntry(audit, BaselineEntryLifecycle.Matched)], "1.2.3");
        string secondOrder = ArchitectureBaselineSarifFormatter.Format(
            [new BaselineLifecycleEntry(audit, BaselineEntryLifecycle.Matched), new BaselineLifecycleEntry(strict, BaselineEntryLifecycle.Matched)], "1.2.3");

        Assert.That(firstOrder, Is.EqualTo(secondOrder));
    }

    private static ArchitectureBaselineComparisonEntry CreateBaselineComparisonEntry(int occurrence)
    {
        return new ArchitectureBaselineComparisonEntry(
            "strict_method_body", "rule", "App.Service", "Infra.Client.Call", "debt",
            new ArchitectureViolationIdentity(
                2, "method_body", "call", "rule", "App", "App.Service", "Run",
                "Infra", "Infra.Client", "Call", occurrence));
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
    public void FormatResultAsSarif_CycleCarriesTheNormalizedFindingEnvelope()
    {
        JsonElement root = Run("strict", Array.Empty<ArchitectureViolation>(), ["[cycle-id] A -> B -> A"]);

        JsonElement normalized = root.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("properties").GetProperty("arch_linter_net");
        Assert.Multiple(() =>
        {
            Assert.That(normalized.GetProperty("schema_version").GetInt32(), Is.EqualTo(ArchitectureFinding.CurrentSchemaVersion));
            Assert.That(normalized.GetProperty("kind").GetString(), Is.EqualTo("cycle"));
            Assert.That(normalized.GetProperty("details").GetProperty("path").GetString(), Is.EqualTo("A -> B -> A"));
        });
    }

    [Test]
    public void FormatResultAsSarif_ViolationCarriesExactCiNormalizedFinding()
    {
        var identity = new ArchitectureViolationIdentity(
            2, "package_dependency", "package", "package-id", null, "Product.Domain",
            null, null, null, "Example.Package@1.0.0", 3, "Debug|net10.0");
        var violation = new ArchitectureViolation(
            "package", "package-id", "Product.Domain", "forbidden package group", ["Example.Package@1.0.0"])
        {
            Identity = identity,
            Identities = [identity],
            Payload = new PackageDependencyPayload("forbidden"),
        };
        var ciFormatter = new ArchitectureDiagnosticFormatter();
        using JsonDocument ci = JsonDocument.Parse(
            ciFormatter.FormatResultForCiArtifacts("strict", false, [violation], Array.Empty<string>()));
        using JsonDocument sarif = JsonDocument.Parse(
            _formatter.FormatResultAsSarif("strict", [violation], Array.Empty<string>(), "1.2.3"));

        JsonNode? ciFinding = JsonNode.Parse(ci.RootElement.GetProperty("violations")[0].GetRawText());
        JsonNode? sarifFinding = JsonNode.Parse(sarif.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("properties").GetProperty("arch_linter_net").GetRawText());

        Assert.That(JsonNode.DeepEquals(ciFinding, sarifFinding), Is.True);
    }

    [Test]
    public void FormatResultAsSarif_PreflightCarriesExactCiNormalizedFinding()
    {
        var preflight = new BuildStatePreflightDiagnostic(
            "preflight", "preflight-id", BuildStatePreflightState.MissingArtifact,
            new BuildStatePreflightEvidence("src/App.csproj", "App"));
        var ciFormatter = new ArchitectureDiagnosticFormatter();
        using JsonDocument ci = JsonDocument.Parse(ciFormatter.FormatResultForCiArtifacts(
            "strict",
            false,
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<ArchitectureClassificationRoleFact>(),
            classificationPathDeferred: null,
            preflightDiagnostics: [preflight]));
        using JsonDocument sarif = JsonDocument.Parse(_formatter.FormatResultAsSarif(
            "strict",
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            [preflight],
            "1.2.3"));

        JsonNode? ciFinding = JsonNode.Parse(ci.RootElement.GetProperty("preflight_diagnostics")[0].GetRawText());
        JsonNode? sarifFinding = JsonNode.Parse(sarif.RootElement.GetProperty("runs")[0].GetProperty("results")[0]
            .GetProperty("properties").GetProperty("arch_linter_net").GetRawText());

        Assert.That(JsonNode.DeepEquals(ciFinding, sarifFinding), Is.True);
    }

    [Test]
    public void FormatResultAsSarif_OptionalEmptyCoverage_PreservesTypedIdentity()
    {
        var summary = new ArchitectureCoverageSummary(
            "coverage", "coverage-id", "rule_input",
            new ArchitectureCoverageSummaryCounts(0, 0, 0, 0, 0) { OptionalEmpty = 1 },
            [], [], [], [], [])
        {
            OptionalEmptyItems =
            [
                new ArchitectureCoverageSummaryOptionalEmptyItem(
                    "rule:forbidden:future", "Future module is planned.", "future")
                {
                    ContractId = "rule",
                    Input = "forbidden",
                    Layer = "future"
                }
            ]
        };

        string json = _formatter.FormatResultAsSarif(
            "strict",
            Array.Empty<ArchitectureViolation>(),
            Array.Empty<string>(),
            Array.Empty<BuildStatePreflightDiagnostic>(),
            new[] { summary },
            "1.2.3");
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement item = document.RootElement.GetProperty("runs")[0].GetProperty("properties")
            .GetProperty("coverage_summary")[0].GetProperty("optional_empty_items")[0];

        Assert.Multiple(() =>
        {
            Assert.That(item.GetProperty("contract_id").GetString(), Is.EqualTo("rule"));
            Assert.That(item.GetProperty("input").GetString(), Is.EqualTo("forbidden"));
            Assert.That(item.GetProperty("layer").GetString(), Is.EqualTo("future"));
        });
    }

    [Test]
    public void FormatResultAsSarif_ContractWithId_UsesIdAsRuleId()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("My Contract", "my-rule", "Source.Type", "Forbidden.Namespace", _ref1)
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("my-rule"));
    }

    [Test]
    public void FormatResultAsSarif_ContextDependencyDiagnostic_PopulatesSourceAndReferences()
    {
        // Regression: ContextDependencyDiagnostic/ContextAllowOnlyDiagnostic were previously absent
        // from ArchitectureSarifFormatter's ExtractFields switch, so SARIF rendered them with an
        // empty source/forbidden-namespace/references triple instead of the real violation data.
        var violations = new List<ArchitectureViolation>
        {
            new("cross-domain", "cross-domain-id", "Source.Type", "role:DomainLayer", _ref1)
            {
                Payload = new ContextDependencyPayload()
            }
        };

        JsonElement result = Run("strict", violations).GetProperty("runs")[0].GetProperty("results")[0];

        Assert.That(result.GetProperty("message").GetProperty("text").GetString(),
            Does.Contain("Source.Type -> role:DomainLayer: ref1"));
    }

    [Test]
    public void FormatResultAsSarif_ContextDependencyWithWhenExpression_AddsRelatedLocation()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("cross-domain", "cross-domain-id", "Source.Type", "role:DomainLayer", _ref1)
            {
                Payload = new ContextDependencyPayload()
                {
                    WhenExpressions = new[] { new ExpressionParticipation(
                        "cross-domain", "forbidden", "target.metadataText[\"domain\"] != source.metadataText[\"domain\"]",
                        "contracts.strict_context_dependencies[0].forbidden[0]", ExpressionParticipationResult.Matched) },
                }
            }
        };

        JsonElement result = Run("strict", violations).GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement relatedLocations = result.GetProperty("relatedLocations");

        Assert.That(
            Enumerable.Range(0, relatedLocations.GetArrayLength())
                .Select(i => relatedLocations[i].GetProperty("message").GetProperty("text").GetString())
                .Any(text => text!.Contains("target.metadataText[\"domain\"] != source.metadataText[\"domain\"]")
                    && text.Contains("matched")),
            Is.True);
    }

    [Test]
    public void FormatResultAsSarif_ContextDependencyWithoutWhenExpression_HasNoRelatedLocations()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("cross-domain", "cross-domain-id", "Source.Type", "role:DomainLayer", _ref1)
            {
                Payload = new ContextDependencyPayload()
            }
        };

        JsonElement result = Run("strict", violations).GetProperty("runs")[0].GetProperty("results")[0];

        Assert.That(result.TryGetProperty("relatedLocations", out _), Is.False);
    }

    [Test]
    public void FormatResultAsSarif_ContractWithoutId_UsesNormalizedNameAsRuleId()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("<configuration>", null, "Source.Type", "Forbidden.Namespace", _ref1)
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
            new("My Contract", "my-rule", "Source.A", "Forbidden.Namespace", _ref1),
            new("My Contract", "my-rule", "Source.B", "Forbidden.Namespace", _ref2)
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
            new("My Contract", "my-rule", "Source.A", "Forbidden.Namespace", _ref1),
            new("My Contract (renamed)", "my-rule", "Source.B", "Forbidden.Namespace", _ref2)
        };

        JsonElement root = Run("strict", violations);

        JsonElement rules = root.GetProperty("runs")[0].GetProperty("tool").GetProperty("driver").GetProperty("rules");
        Assert.That(rules.GetArrayLength(), Is.EqualTo(1));
        Assert.That(rules[0].GetProperty("id").GetString(), Is.EqualTo("my-rule"));
    }

    [Test]
    public void FormatResultAsSarif_LayoutConventionDiagnostic_ExtractsFieldsIntoMessage()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("layout-rule", "layout-id", "Layout.Source", "forbidden type kind 'interface'", _ref1)
            { Payload = new LayoutConventionPayload(MatchedFilePath: "src/App/Services/Bad.cs") }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        string message = result.GetProperty("message").GetProperty("text").GetString()!;
        Assert.That(message, Does.Contain("Layout.Source"));
        Assert.That(message, Does.Contain("forbidden type kind 'interface'"));
        Assert.That(message, Does.Contain("ref1"));
    }

    [Test]
    public void FormatResultAsSarif_LayoutConventionDeclarationCount_ExposesStructuredDetails()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("layout-rule", "layout-id", "Layout.Source", "expected at most 1 source declaration(s), found 2", _ref1)
            {
                Payload = new LayoutConventionPayload(MatchedFilePath: "src/App/Services/OrderService.Part1.cs")
                {
                    ExpectedDeclarationCount = 1,
                    ActualDeclarationCount = 2,
                    DeclarationPaths = ["src/App/Services/OrderService.Part1.cs", "src/App/Services/OrderService.Part2.cs"],
                },
            },
        };

        JsonElement root = Run("strict", violations);

        JsonElement properties = root.GetProperty("runs")[0].GetProperty("results")[0].GetProperty("properties");
        Assert.Multiple(() =>
        {
            Assert.That(properties.GetProperty("expected_declaration_count").GetInt32(), Is.EqualTo(1));
            Assert.That(properties.GetProperty("actual_declaration_count").GetInt32(), Is.EqualTo(2));
            Assert.That(properties.GetProperty("declaration_paths")[1].GetString(),
                Is.EqualTo("src/App/Services/OrderService.Part2.cs"));
            Assert.That(properties.GetProperty("arch_linter_net").GetProperty("expected_declaration_count").GetInt32(),
                Is.EqualTo(1));
        });
    }

    // Regression: MatchedFilePath is a real repository-relative .cs path, unlike every other
    // family's SourceType (a fully-qualified type name) - it must produce a SARIF physicalLocation
    // so GitHub Code Scanning can anchor the finding to that file, not fall back to logicalLocations.
    [Test]
    public void FormatResultAsSarif_LayoutConventionDiagnostic_WithMatchedFilePath_UsesPhysicalLocation()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("layout-rule", "layout-id", "Layout.Source", "forbidden type kind 'interface'", _ref1)
            { Payload = new LayoutConventionPayload(MatchedFilePath: "src/App/Services/Bad.cs") }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.That(result.TryGetProperty("locations", out JsonElement locations), Is.True);
        Assert.That(
            locations[0].GetProperty("physicalLocation").GetProperty("artifactLocation").GetProperty("uri").GetString(),
            Is.EqualTo("src/App/Services/Bad.cs"));
        Assert.That(
            result.GetProperty("properties").GetProperty("arch_linter_net")
                .GetProperty("source_location").GetProperty("path").GetString(),
            Is.EqualTo("src/App/Services/Bad.cs"));
        Assert.That(result.TryGetProperty("logicalLocations", out _), Is.False);
    }

    // Regression: an "unavailable"/ambiguous layout diagnostic has no single resolved file
    // (MatchedFilePath is null) - it must fall back to the generic logicalLocations by type name,
    // not fabricate a physical location that doesn't exist.
    [Test]
    public void FormatResultAsSarif_LayoutConventionDiagnostic_WithoutMatchedFilePath_UsesLogicalLocation()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("layout-rule", "layout-id", "Layout.Source", "path-based layout checks unavailable", _ref1)
            { Payload = new LayoutConventionPayload(DataUnavailable: true) }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.That(LogicalLocationOf(result).GetProperty("fullyQualifiedName").GetString(), Is.EqualTo("Layout.Source"));
    }

    [Test]
    public void FormatResultAsSarif_StrictMode_LevelIsError()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", "my-rule", "Source.Type", "Forbidden.Namespace", _ref1)
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
            new("contract", "my-rule", "Source.Type", "Forbidden.Namespace", _ref1)
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
                _value)
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
                _value1)
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
            new("layer-rule", "layer-rule", "MyApp.Web.Foo", "protected layer 'Core'", _ref1)
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement logicalLocation = LogicalLocationOf(result);
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
            new("package-rule", "package-rule", "MyApp.Csproj", "forbidden-packages", _value2)
            {
                Payload = new PackageDependencyPayload("legacy-json")
            }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement logicalLocation = LogicalLocationOf(result);
        Assert.That(logicalLocation.GetProperty("kind").GetString(), Is.EqualTo("package"));
    }

    [Test]
    public void FormatResultAsSarif_PackageAllowOnlyViolation_LogicalLocationKindIsPackage()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("package-allow-only-rule", "package-allow-only-rule", "MyApp.Csproj", "outside allowed package groups",
                _newtonsoftJsonReference)
            {
                Payload = new PackageAllowOnlyPayload(_approvedInfraGroup)
            }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement logicalLocation = LogicalLocationOf(result);
        Assert.That(logicalLocation.GetProperty("kind").GetString(), Is.EqualTo("package"));
    }

    [Test]
    public void FormatResultAsSarif_FrameworkReferenceViolation_LogicalLocationKindIsFrameworkReference()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("framework-rule", "framework-rule", "MyApp.Csproj", "forbidden-frameworks", _value3)
            {
                Payload = new FrameworkReferencePayload("forbidden_web")
            }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement logicalLocation = LogicalLocationOf(result);
        Assert.That(logicalLocation.GetProperty("kind").GetString(), Is.EqualTo("framework-reference"));
    }

    [Test]
    public void FormatResultAsSarif_FrameworkReferenceAllowOnlyViolation_LogicalLocationKindIsFrameworkReference()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("framework-allow-only-rule", "framework-allow-only-rule", "MyApp.Csproj", "outside allowed framework groups",
                _value4)
            {
                Payload = new FrameworkReferenceAllowOnlyPayload(_value5)
            }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement logicalLocation = LogicalLocationOf(result);
        Assert.That(logicalLocation.GetProperty("kind").GetString(), Is.EqualTo("framework-reference"));
    }

    [Test]
    public void FormatResultAsSarif_CompositionViolation_PropertiesExposeSourceAssembly()
    {
        string[] forbiddenReferences = { "System.IServiceProvider.GetService" };
        var violations = new List<ArchitectureViolation>
        {
            new("service-locator-confined-to-composition", "service-locator-confined-to-composition",
                "Host.A.Program", "System.IServiceProvider.GetService", forbiddenReferences)
            {
                Payload = new CompositionPayload(
                    MatchedForbiddenApi: "System.IServiceProvider.GetService",
                    SourceMember: "Host.A.Program.Main",
                    SourceAssembly: "Host.A",
                    ExpectedCompositionBoundary: "namespaces: [Host.Composition]")
            }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement properties = result.GetProperty("properties");
        Assert.Multiple(() =>
        {
            Assert.That(properties.GetProperty("source_assembly").GetString(), Is.EqualTo("Host.A"));
            Assert.That(properties.GetProperty("source_member").GetString(), Is.EqualTo("Host.A.Program.Main"));
            Assert.That(properties.GetProperty("matched_forbidden_api").GetString(),
                Is.EqualTo("System.IServiceProvider.GetService"));
            Assert.That(properties.GetProperty("expected_composition_boundary").GetString(),
                Is.EqualTo("namespaces: [Host.Composition]"));
        });
    }

    [Test]
    public void FormatResultAsSarif_TypePlacementViolation_LogicalLocationKindIsType()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("type-placement-rule", "type-placement-rule", "MyApp.Foo", "expected-location", _ref1)
            {
                Payload = new TypePlacementPayload(ExpectedTypeLocation: "MyApp.Correct")
            }
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement logicalLocation = LogicalLocationOf(result);
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
                _value6)
        };

        JsonElement root = Run("strict", violations);

        JsonElement result = root.GetProperty("runs")[0].GetProperty("results")[0];
        JsonElement logicalLocation = LogicalLocationOf(result);
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
            Assert.That(LogicalLocationOf(result).GetProperty("fullyQualifiedName").GetString(),
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
    public void FormatResultAsSarif_TypedCycle_IncludesPolicyLocationEvidence()
    {
        var source = new ArchitecturePolicySourceDescriptor(
            "architecture/root.yml", "architecture/rules.yml", ArchitecturePolicyDocumentRole.Fragment,
            1, "architecture/root.yml", "rules.yml", ["architecture/root.yml", "architecture/rules.yml"]);
        var cycle = new ArchitectureCycleFinding("Cycle Rule", "cycle-check", "LayerA -> LayerB -> LayerA")
        {
            PolicyLocation = new ArchitecturePolicySourceLocation(
                source, "contracts.strict_cycles[0]", 7, 3, "cycle", "cycle-check", 9)
        };

        string json = ArchitectureSarifFormatter.FormatResultAsSarif(
            "strict", Array.Empty<ArchitectureViolation>(), [cycle], "1.2.3");
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("cycle-check"));
        Assert.That(result.GetProperty("relatedLocations")[0].GetProperty("physicalLocation")
            .GetProperty("artifactLocation").GetProperty("uri").GetString(), Is.EqualTo("architecture/rules.yml"));
    }

    [Test]
    public void FormatResultAsSarif_MixedViolationKindsAndCycles_OutputIsIndependentOfInputOrder()
    {
        var violationA = new ArchitectureViolation("a-contract", "a-rule", "Source.A", "Forbidden.A", _value7)
        {
            Payload = new ExternalDependencyPayload("external-group")
        };
        var violationB = new ArchitectureViolation("b-contract", "b-rule", "Source.B", "Forbidden.B", _value8);
        var cycle = "[m-rule] X -> Y -> X";

        string inOriginalOrder = _formatter.FormatResultAsSarif(
            "strict", new[] { violationB, violationA }, new[] { cycle }, "1.0.0");
        string inReversedOrder = _formatter.FormatResultAsSarif(
            "strict", new[] { violationA, violationB }, new[] { cycle }, "1.0.0");

        Assert.That(inOriginalOrder, Is.EqualTo(inReversedOrder));

        using var doc = JsonDocument.Parse(inOriginalOrder);
        JsonElement results = doc.RootElement.GetProperty("runs")[0].GetProperty("results");
        var ruleIds = results.EnumerateArray().Select(r => r.GetProperty("ruleId").GetString()).ToArray();
        Assert.That(ruleIds, Is.EqualTo(_value9));
    }
}
