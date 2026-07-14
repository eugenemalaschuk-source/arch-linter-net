using System.Text.Json;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureDiagnosticFormatterTests
{
    private static readonly ArchitectureDiagnosticFormatter _formatter = new();
    private static readonly string[] _reference1 = ["ref1"];
    private static readonly string[] _allowedApiImporters = ["Api"];
    private static readonly IReadOnlyCollection<string>[] _dependencyPaths = [["Source.Type", "Mid", "Forbidden.Namespace"]];
    private static readonly string[] _forbiddenNamespaceInternal = ["Forbidden.Namespace.Internal"];
    private static readonly string[] _coreInternal = ["Core.Internal"];
    private static readonly string[] _forbiddenApiReference = ["Forbidden.Api"];
    private static readonly string[] _typeReferences = ["Type.Ref"];
    private static readonly string[] _apiReferences = ["Api.Ref"];
    private static readonly string[] _attributeReferences = ["Attribute.Ref"];
    private static readonly string[] _inheritanceReferences = ["Inheritance.Ref"];
    private static readonly string[] _interfaceReferences = ["Interface.Ref"];
    private static readonly string[] _compositionReferences = ["Composition.Ref"];
    private static readonly string[] _projectReferences = ["Project.Ref"];
    private static readonly string[] _externalReferences = ["External.Ref"];
    private static readonly ArchitectureCoverageSummaryExcludedItem[] _excludedCoverageItems = [new("z-excluded", "generated")];
    private static readonly ArchitectureCoverageSummaryEvidenceItem[] _uncoveredCoverageItems = [new("a-uncovered", "a-evidence")];
    private static readonly ArchitectureCoverageSummaryEvidenceItem[] _staleCoverageItems = [new("b-stale", "b-evidence")];
    private static readonly ArchitectureCoverageSummaryEvidenceItem[] _unknownCoverageItems = [new("c-unknown", "c-evidence")];
    private static readonly ArchitectureCoverageSummaryEvidenceItem[] _coveredCoverageItems = [new("d-covered", "d-evidence")];
    private static readonly string[] _expectedRoleSubjectsSorted = ["MyApp.Alpha", "MyApp.Zeta"];
    private static readonly string[] _firstPolicyId = ["first-id"];
    private static readonly string[] _policyContractNames = ["first", "second"];
    private static readonly string[] _policyLayers = ["Core"];
    private static readonly ArchitectureViolation[] _coverageFinding = [new("coverage", "coverage-id", "Source", "Forbidden", ["Reference"])];

    [Test]
    public void FormatViolationsForHumans_DependencyDiagnostic_IncludesLayerContext()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "protected layer 'Core'", _reference1)
            {
                Payload = new DependencyPayload(
                    SourceLayer: "Web",
                    TargetLayer: "Core",
                    AllowedImporters: _allowedApiImporters)
            }
        };

        string output = _formatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("source_layer: Web"));
        Assert.That(output, Does.Contain("target_layer: Core"));
        Assert.That(output, Does.Contain("allowed_importers: [Api]"));
    }

    [Test]
    public void FormatViolationsForHumans_ConfigurationDiagnosticWithDependencyPaths_IncludesViaLines()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "Forbidden.Namespace", _reference1)
            {
                Payload = new ConfigurationPayload(
                    DependencyPaths: _dependencyPaths)
            }
        };

        string output = _formatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("via: Source.Type -> Mid -> Forbidden.Namespace"));
    }

    [Test]
    public void FormatViolationsForHumans_MatchedNamespacePrefix_AnnotatesNamespace()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "Forbidden.Namespace", _reference1)
            {
                MatchedNamespacePrefixes = _forbiddenNamespaceInternal
            }
        };

        string output = _formatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("matched Forbidden.Namespace.Internal"));
    }

    [Test]
    public void FormatResultForCiArtifacts_ConfigurationDiagnostic_IncludesTemplateAndContainerFields()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "Forbidden.Namespace", _reference1)
            {
                Payload = new ConfigurationPayload(
                    TemplateName: "asmdef-template",
                    ContainerNamespace: "MyApp.Modules")
            }
        };

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>());

        using var doc = JsonDocument.Parse(json);
        JsonElement violation = doc.RootElement.GetProperty("violations")[0];
        Assert.That(violation.GetProperty("template_name").GetString(), Is.EqualTo("asmdef-template"));
        Assert.That(violation.GetProperty("container_namespace").GetString(), Is.EqualTo("MyApp.Modules"));
    }

    [Test]
    public void FormatResultForCiArtifacts_LayerDiagnosticWithMatchedPrefixes_IncludesBothFieldGroups()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("contract", null, "Source.Type", "protected layer 'Core'", _reference1)
            {
                Payload = new DependencyPayload(
                    SourceLayer: "Web",
                    TargetLayer: "Core",
                    AllowedImporters: _allowedApiImporters),
                MatchedNamespacePrefixes = _coreInternal
            }
        };

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>());

        using var doc = JsonDocument.Parse(json);
        JsonElement violation = doc.RootElement.GetProperty("violations")[0];
        Assert.That(violation.GetProperty("source_layer").GetString(), Is.EqualTo("Web"));
        Assert.That(violation.GetProperty("matched_namespace_prefixes")[0].GetString(), Is.EqualTo("Core.Internal"));
    }

    [Test]
    public void FormatViolationsForHumans_CompositionDiagnostic_IncludesSourceMember()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("composition", null, "Source.Type", "Forbidden.Api", _forbiddenApiReference)
            {
                Payload = new CompositionPayload(
                    SourceMember: "Source.Type.Configure",
                    MatchedForbiddenApi: "Forbidden.Api",
                    ExpectedCompositionBoundary: "namespaces: [Composition]")
            }
        };

        string output = _formatter.FormatViolationsForHumans(violations);

        Assert.That(output, Does.Contain("source_member: Source.Type.Configure"));
    }

    [Test]
    public void FormatResultForCiArtifacts_CompositionDiagnostic_IncludesSourceMember()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("composition", null, "Source.Type", "Forbidden.Api", _forbiddenApiReference)
            {
                Payload = new CompositionPayload(
                    SourceMember: "Source.Type.Configure",
                    MatchedForbiddenApi: "Forbidden.Api")
            }
        };

        string json = _formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>());

        using var doc = JsonDocument.Parse(json);
        JsonElement violation = doc.RootElement.GetProperty("violations")[0];
        Assert.That(violation.GetProperty("source_member").GetString(), Is.EqualTo("Source.Type.Configure"));
    }

    [Test]
    public void FormatDiagnostics_AllPayloadKinds_IncludesHumanAndCiContext()
    {
        var violations = new List<ArchitectureViolation>
        {
            new("type", "type-id", "Type.Source", "Type.Forbidden", _typeReferences)
            { Payload = new TypePlacementPayload("Expected.Location", "Actual.Location", "Expected.Name", "Actual.Name") },
            new("api", "api-id", "Api.Source", "Api.Forbidden", _apiReferences)
            { Payload = new PublicApiSurfacePayload("public void Api()", true, "Api.Assembly", "public") },
            new("attribute", "attribute-id", "Attribute.Source", "Attribute.Forbidden", _attributeReferences)
            { Payload = new AttributeUsagePayload("ObsoleteAttribute", "forbidden", "Expected.Attribute", "Actual.Attribute") },
            new("inheritance", "inheritance-id", "Inheritance.Source", "Inheritance.Forbidden", _inheritanceReferences)
            { Payload = new InheritancePayload("Forbidden.Base", "public_api") },
            new("interface", "interface-id", "Interface.Source", "Interface.Forbidden", _interfaceReferences)
            { Payload = new InterfaceImplementationPayload("IForbidden", "missing", "Expected.Interface", "Actual.Interface") },
            new("composition", "composition-id", "Composition.Source", "Composition.Forbidden", _compositionReferences)
            { Payload = new CompositionPayload("Composition.Configure", "Forbidden.Api", "Composition boundary") },
            new("project", "project-id", "Project.Source", "Project.Forbidden", _projectReferences)
            { Payload = new ProjectMetadataPayload("forbidden_property", "Nullable", "enable", "disable", "src/App.csproj") },
            new("external", "external-id", "External.Source", "External.Forbidden", _externalReferences)
            { Payload = new ExternalDependencyPayload("vendor_sdk") }
        };

        string human = _formatter.FormatViolationsForHumans(violations);
        Assert.That(human, Does.Contain("expected_location: Expected.Location"));
        Assert.That(human, Does.Contain("reason: forbidden_public_constant"));
        Assert.That(human, Does.Contain("attribute: ObsoleteAttribute"));
        Assert.That(human, Does.Contain("forbidden_base_type: Forbidden.Base"));
        Assert.That(human, Does.Contain("interface: IForbidden"));
        Assert.That(human, Does.Contain("matched_api: Forbidden.Api"));
        Assert.That(human, Does.Contain("source_path: src/App.csproj"));
        Assert.That(human, Does.Contain("external_group: vendor_sdk"));

        using var document = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, violations, Array.Empty<string>()));
        JsonElement serialized = document.RootElement.GetProperty("violations");
        Assert.That(serialized.ToString(), Does.Contain("expected_type_location"));
        Assert.That(serialized.ToString(), Does.Contain("undeclared_api_signature"));
        Assert.That(serialized.ToString(), Does.Contain("matched_attribute"));
        Assert.That(serialized.ToString(), Does.Contain("forbidden_base_type"));
        Assert.That(serialized.ToString(), Does.Contain("matched_interface"));
        Assert.That(serialized.ToString(), Does.Contain("project_metadata_source_path"));
        Assert.That(serialized.ToString(), Does.Contain("forbidden_external_group"));
    }

    [Test]
    public void FormatCoverageAndPolicyResults_IncludesSortedDetails()
    {
        var summary = new ArchitectureCoverageSummary(
            "coverage", "coverage-id", "namespace",
            new ArchitectureCoverageSummaryCounts(1, 1, 1, 1, 1),
            _excludedCoverageItems,
            _uncoveredCoverageItems,
            _staleCoverageItems,
            _unknownCoverageItems,
            _coveredCoverageItems);
        var policy = new PolicyConsistencyDiagnostic(
            "policy", "policy-id", "duplicate", "conflicting rules",
            _firstPolicyId, _policyContractNames, _policyLayers)
        { RepresentativeType = "Core.Representative" };

        Assert.That(_formatter.FormatCoverageForHumans(_coverageFinding), Does.StartWith("Coverage findings:"));
        string humanSummary = _formatter.FormatCoverageSummaryForHumans(new List<ArchitectureCoverageSummary> { summary });
        Assert.That(humanSummary, Does.Contain("covered=1 excluded=1 uncovered=1 stale=1 unknown=1"));
        Assert.That(humanSummary, Does.Contain("uncovered: a-uncovered (a-evidence)"));
        Assert.That(_formatter.FormatPolicyConsistencyForHumans(new List<PolicyConsistencyDiagnostic> { policy }), Does.Contain("Core.Representative").Or.Contain("conflicting rules"));

        using var json = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            policyConsistencyFindings: new List<PolicyConsistencyDiagnostic> { policy }, coverageSummaries: new List<ArchitectureCoverageSummary> { summary }));
        Assert.That(json.RootElement.GetProperty("policy_consistency_findings")[0].GetProperty("representative_type").GetString(),
            Is.EqualTo("Core.Representative"));
        Assert.That(json.RootElement.GetProperty("coverage_summary")[0].GetProperty("covered_items")[0].GetProperty("item").GetString(),
            Is.EqualTo("d-covered"));
        Assert.That(json.RootElement.GetProperty("coverage_summary")[0].GetProperty("excluded_items")[0]
            .TryGetProperty("evidence", out _), Is.False);
    }

    [Test]
    public void FormatClassificationFactsForHumans_EmptyFacts_ReturnsEmptyString()
    {
        string output = _formatter.FormatClassificationFactsForHumans(
            Array.Empty<Model.ArchitectureClassificationConflict>(),
            Array.Empty<Model.ArchitectureClassificationMetadataFailure>());

        Assert.That(output, Is.Empty);
    }

    [Test]
    public void FormatClassificationFactsForHumans_PathDeferredNotice_IncludesEntryCountAndIssueReference()
    {
        var notice = new Model.ArchitectureClassificationPathDeferredNotice(3);

        string human = _formatter.FormatClassificationFactsForHumans(
            Array.Empty<Model.ArchitectureClassificationConflict>(),
            Array.Empty<Model.ArchitectureClassificationMetadataFailure>(),
            notice);

        Assert.That(human, Does.StartWith("Classification findings:"));
        Assert.That(human, Does.Contain("classification.path declares 3 entries"));
        Assert.That(human, Does.Contain("#171"));

        using var json = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            classificationRoles: Array.Empty<Model.ArchitectureClassificationRoleFact>(),
            classificationPathDeferred: notice));
        Assert.That(
            json.RootElement.GetProperty("classification_path_deferred").GetProperty("declared_entry_count").GetInt32(),
            Is.EqualTo(3));
    }

    [Test]
    public void FormatResultForCiArtifacts_NoPathDeferredNotice_OmitsClassificationPathDeferred()
    {
        using var json = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>()));

        Assert.That(json.RootElement.GetProperty("classification_path_deferred").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public void FormatClassificationFactsForHumans_ConflictsAndFailures_IncludesDetails()
    {
        var conflicts = new[]
        {
            new Model.ArchitectureClassificationConflict(
                "MyApp.Order", Model.ArchitectureClassificationSource.TypeAttribute, "DomainLayer", "InfrastructureLayer", null)
        };
        var failures = new[]
        {
            new Model.ArchitectureClassificationMetadataFailure(
                "MyApp.Order", Model.ArchitectureClassificationSource.TypeAttribute, "module", "named argument 'Module' was not explicitly supplied")
        };

        string human = _formatter.FormatClassificationFactsForHumans(conflicts, failures);

        Assert.That(human, Does.StartWith("Classification findings:"));
        Assert.That(human, Does.Contain("MyApp.Order"));
        Assert.That(human, Does.Contain("kept 'DomainLayer', discarded 'InfrastructureLayer'"));
        Assert.That(human, Does.Contain("module"));
        Assert.That(human, Does.Contain("named argument 'Module' was not explicitly supplied"));

        using var json = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            classificationConflicts: conflicts, classificationMetadataFailures: failures));
        Assert.That(json.RootElement.GetProperty("classification_conflicts")[0].GetProperty("subject").GetString(), Is.EqualTo("MyApp.Order"));
        Assert.That(json.RootElement.GetProperty("classification_metadata_failures")[0].GetProperty("metadata_key").GetString(), Is.EqualTo("module"));
    }

    [Test]
    public void FormatClassificationFactsForHumans_ImportedMappings_IncludePolicyLocations()
    {
        ArchitecturePolicySourceLocation primary = CreatePolicyLocation(
            "architecture/classification.yml", "classification.namespace[0]", sourceOrdinal: 1, encounterOrdinal: 3);
        ArchitecturePolicySourceLocation related = CreatePolicyLocation(
            "architecture/classification.yml", "classification.namespace[1]", sourceOrdinal: 1, encounterOrdinal: 4);
        var conflicts = new[]
        {
            new Model.ArchitectureClassificationConflict(
                "MyApp.Order", Model.ArchitectureClassificationSource.Namespace, "DomainLayer", "InfrastructureLayer", null)
            {
                PolicyLocation = primary,
                RelatedPolicyLocations = [related]
            }
        };
        var failures = new[]
        {
            new Model.ArchitectureClassificationMetadataFailure(
                "MyApp.Order", Model.ArchitectureClassificationSource.Namespace, "module", "unknown const")
            {
                PolicyLocation = CreatePolicyLocation(
                    "architecture/classification.yml", "classification.namespace[0].metadata.module", 1, 5)
            }
        };

        string human = _formatter.FormatClassificationFactsForHumans(conflicts, failures);

        Assert.That(human, Does.Contain("policy: architecture/classification.yml:classification.namespace[0]"));
        Assert.That(human, Does.Contain("related: architecture/classification.yml:classification.namespace[1]"));
        Assert.That(human, Does.Contain("classification.namespace[0].metadata.module"));

        using var json = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            classificationConflicts: conflicts, classificationMetadataFailures: failures));
        Assert.That(
            json.RootElement.GetProperty("classification_conflicts")[0]
                .GetProperty("policy_location").GetProperty("yaml_path").GetString(),
            Is.EqualTo("classification.namespace[0]"));
        Assert.That(
            json.RootElement.GetProperty("classification_metadata_failures")[0]
                .GetProperty("policy_location").GetProperty("yaml_path").GetString(),
            Is.EqualTo("classification.namespace[0].metadata.module"));
    }

    [Test]
    public void FormatClassificationFactsForHumans_MetadataOnlyConflict_IncludesMetadataDetail()
    {
        var conflicts = new[]
        {
            new Model.ArchitectureClassificationConflict(
                "MyApp.Order", Model.ArchitectureClassificationSource.TypeAttribute, "DomainLayer", "DomainLayer",
                "domain: 'Sales' vs 'Marketing'")
        };

        string human = _formatter.FormatClassificationFactsForHumans(conflicts, Array.Empty<Model.ArchitectureClassificationMetadataFailure>());

        Assert.That(human, Does.Contain("kept 'DomainLayer', discarded 'DomainLayer'"));
        Assert.That(human, Does.Contain("domain: 'Sales' vs 'Marketing'"));

        using var json = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            classificationConflicts: conflicts));
        Assert.That(
            json.RootElement.GetProperty("classification_conflicts")[0].GetProperty("metadata_detail").GetString(),
            Is.EqualTo("domain: 'Sales' vs 'Marketing'"));
    }

    [Test]
    public void FormatClassificationFactsForHumans_MultipleConflicts_OrdersDeterministically()
    {
        var conflicts = new[]
        {
            new Model.ArchitectureClassificationConflict(
                "MyApp.Order", Model.ArchitectureClassificationSource.TypeAttribute, "DomainLayer", "DomainLayer",
                "domain: 'Sales' vs 'Marketing'"),
            new Model.ArchitectureClassificationConflict(
                "MyApp.Order", Model.ArchitectureClassificationSource.TypeAttribute, "DomainLayer", "DomainLayer",
                "domain: 'Sales' vs 'Engineering'")
        };

        string firstOrder = _formatter.FormatClassificationFactsForHumans(
            conflicts, Array.Empty<Model.ArchitectureClassificationMetadataFailure>());
        string reversedOrder = _formatter.FormatClassificationFactsForHumans(
            conflicts.Reverse().ToArray(), Array.Empty<Model.ArchitectureClassificationMetadataFailure>());

        Assert.That(firstOrder, Is.EqualTo(reversedOrder));
    }

    [Test]
    public void FormatResultForCiArtifacts_ClassificationRoles_IncludesRoleMetadataAndSource()
    {
        var roles = new[]
        {
            new Model.ArchitectureClassificationRoleFact(
                "MyApp.Order", "DomainLayer", Model.ArchitectureClassificationSource.TypeAttribute,
                "MyApp.DomainMarkerAttribute", new Dictionary<string, object> { ["domain"] = "Sales" })
        };

        using var json = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            classificationRoles: roles));

        JsonElement role = json.RootElement.GetProperty("classification_roles")[0];
        Assert.That(role.GetProperty("subject").GetString(), Is.EqualTo("MyApp.Order"));
        Assert.That(role.GetProperty("role").GetString(), Is.EqualTo("DomainLayer"));
        Assert.That(role.GetProperty("source").GetString(), Is.EqualTo("TypeAttribute"));
        Assert.That(role.GetProperty("evidence").GetString(), Is.EqualTo("MyApp.DomainMarkerAttribute"));
        Assert.That(role.GetProperty("metadata").GetProperty("domain").GetString(), Is.EqualTo("Sales"));
    }

    [Test]
    public void FormatResultForCiArtifacts_NoClassificationRoles_IncludesEmptyArray()
    {
        using var json = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>()));

        Assert.That(json.RootElement.GetProperty("classification_roles").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public void FormatResultForCiArtifacts_MultipleClassificationRoles_OrdersBySubject()
    {
        var roles = new[]
        {
            new Model.ArchitectureClassificationRoleFact(
                "MyApp.Zeta", "DomainLayer", Model.ArchitectureClassificationSource.TypeAttribute, null, new Dictionary<string, object>()),
            new Model.ArchitectureClassificationRoleFact(
                "MyApp.Alpha", "DomainLayer", Model.ArchitectureClassificationSource.TypeAttribute, null, new Dictionary<string, object>())
        };

        using var json = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(),
            classificationRoles: roles));

        JsonElement.ArrayEnumerator classificationRoles = json.RootElement.GetProperty("classification_roles").EnumerateArray();
        List<string?> subjects = classificationRoles.Select(r => r.GetProperty("subject").GetString()).ToList();
        Assert.That(subjects, Is.EqualTo(_expectedRoleSubjectsSorted));
    }

    // Guards against the roles-overload silently resolving to IArchitectureDiagnosticFormatter's
    // default interface implementation (which drops classificationRoles for compatibility with
    // pre-existing third-party implementers) instead of ArchitectureDiagnosticFormatter's own
    // override, when called through the interface rather than the concrete type.
    [Test]
    public void FormatResultForCiArtifacts_CalledThroughInterface_UsesConcreteOverrideNotDefaultImplementation()
    {
        IArchitectureDiagnosticFormatter formatter = new ArchitectureDiagnosticFormatter();
        var roles = new[]
        {
            new Model.ArchitectureClassificationRoleFact(
                "MyApp.Order", "DomainLayer", Model.ArchitectureClassificationSource.TypeAttribute, null, new Dictionary<string, object>())
        };

        using var json = JsonDocument.Parse(formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(), roles));

        Assert.That(json.RootElement.GetProperty("classification_roles").GetArrayLength(), Is.EqualTo(1));
    }

    // A third-party IArchitectureDiagnosticFormatter implementer that predates the roles overload
    // and only implements the members that existed before it must still compile and function —
    // proving the roles overload's default interface implementation satisfies the interface
    // contract without forcing every implementer to add a new member.
    private sealed class PreExistingThirdPartyFormatter : IArchitectureDiagnosticFormatter
    {
        public string FormatViolationsForHumans(IReadOnlyCollection<ArchitectureViolation> violations) => string.Empty;
        public string FormatCyclesForHumans(IReadOnlyCollection<string> cycles) => string.Empty;
        public string FormatUnmatchedForHumans(IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation> unmatched) => string.Empty;
        public string FormatPolicyConsistencyForHumans(IReadOnlyCollection<PolicyConsistencyDiagnostic> findings) => string.Empty;
        public string FormatCoverageForHumans(IReadOnlyCollection<ArchitectureViolation> findings) => string.Empty;
        public string FormatCoverageSummaryForHumans(IReadOnlyCollection<ArchitectureCoverageSummary> summaries) => string.Empty;

        public string FormatClassificationFactsForHumans(
            IReadOnlyCollection<Model.ArchitectureClassificationConflict> conflicts,
            IReadOnlyCollection<Model.ArchitectureClassificationMetadataFailure> metadataFailures) => string.Empty;

        public string FormatResultForCiArtifacts(
            string mode, bool passed, IReadOnlyCollection<ArchitectureViolation> violations, IReadOnlyCollection<string> cycles,
            IReadOnlyCollection<ArchitectureViolation>? coverageFindings = null,
            IReadOnlyCollection<ArchitectureUnmatchedIgnoredViolation>? unmatched = null,
            IReadOnlyCollection<PolicyConsistencyDiagnostic>? policyConsistencyFindings = null,
            IReadOnlyCollection<ArchitectureCoverageSummary>? coverageSummaries = null,
            IReadOnlyCollection<Model.ArchitectureClassificationConflict>? classificationConflicts = null,
            IReadOnlyCollection<Model.ArchitectureClassificationMetadataFailure>? classificationMetadataFailures = null)
            => "pre-existing-implementation";

        // Deliberately does NOT implement the classificationRoles overload — relies entirely on
        // IArchitectureDiagnosticFormatter's default interface implementation.

        public string FormatViolationsForCiArtifacts(string contractName, string? contractId,
            IReadOnlyCollection<ArchitectureViolation> violations) => string.Empty;

        public string FormatCyclesForCiArtifacts(string contractName, string? contractId, IReadOnlyCollection<string> cycles) => string.Empty;
    }

    [Test]
    public void FormatResultForCiArtifacts_PreExistingImplementerWithoutRolesOverload_CompilesAndFallsBackToOriginalMember()
    {
        IArchitectureDiagnosticFormatter formatter = new PreExistingThirdPartyFormatter();
        var roles = new[]
        {
            new Model.ArchitectureClassificationRoleFact(
                "MyApp.Order", "DomainLayer", Model.ArchitectureClassificationSource.TypeAttribute, null, new Dictionary<string, object>())
        };

        string result = formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(), roles);

        Assert.That(result, Is.EqualTo("pre-existing-implementation"));
    }

    // A third-party implementer that predates the classificationPathDeferred overloads must still
    // compile and function — proving those overloads' default interface implementations satisfy the
    // interface contract without forcing every implementer to add them, chaining down through the
    // roles overload's own default implementation to the original member (#307 review: patch coverage).
    [Test]
    public void FormatResultForCiArtifacts_PreExistingImplementerWithoutPathDeferredOverload_FallsBackThroughRolesOverload()
    {
        IArchitectureDiagnosticFormatter formatter = new PreExistingThirdPartyFormatter();
        var roles = new[]
        {
            new Model.ArchitectureClassificationRoleFact(
                "MyApp.Order", "DomainLayer", Model.ArchitectureClassificationSource.TypeAttribute, null, new Dictionary<string, object>())
        };
        var pathDeferred = new Model.ArchitectureClassificationPathDeferredNotice(1);

        string result = formatter.FormatResultForCiArtifacts(
            "strict", true, Array.Empty<ArchitectureViolation>(), Array.Empty<string>(), roles, pathDeferred);

        Assert.That(result, Is.EqualTo("pre-existing-implementation"));
    }

    [Test]
    public void FormatClassificationFactsForHumans_PreExistingImplementerWithoutPathDeferredOverload_FallsBackToOriginalMember()
    {
        IArchitectureDiagnosticFormatter formatter = new PreExistingThirdPartyFormatter();
        var pathDeferred = new Model.ArchitectureClassificationPathDeferredNotice(1);

        string result = formatter.FormatClassificationFactsForHumans(
            Array.Empty<Model.ArchitectureClassificationConflict>(),
            Array.Empty<Model.ArchitectureClassificationMetadataFailure>(),
            pathDeferred);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void FormatCyclesForHumans_MultipleCycles_SortedAlphabetically()
    {
        var cycles = new[] { "Z -> Y -> Z", "A -> B -> A" };

        string output = _formatter.FormatCyclesForHumans(cycles);

        Assert.That(output, Is.EqualTo("- A -> B -> A" + Environment.NewLine + "- Z -> Y -> Z"));
    }

    [Test]
    public void FormatCyclesForCiArtifacts_IncludesCyclePaths()
    {
        var cycles = new[] { "A -> B -> A" };

        string json = _formatter.FormatCyclesForCiArtifacts("cycle-contract", "cycle-check", cycles);

        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetProperty("cycles")[0].GetString(), Is.EqualTo("A -> B -> A"));
        Assert.That(doc.RootElement.GetProperty("contract_id").GetString(), Is.EqualTo("cycle-check"));
    }

    [Test]
    public void FormatUnmatchedForHumans_NoEntries_ReturnsEmptyString()
    {
        string output = _formatter.FormatUnmatchedForHumans(
            Array.Empty<ArchitectureUnmatchedIgnoredViolation>());

        Assert.That(output, Is.Empty);
    }

    [Test]
    public void FormatUnmatchedForHumans_Entry_IncludesReasonAndSourceType()
    {
        var unmatched = new List<ArchitectureUnmatchedIgnoredViolation>
        {
            new("contract", "contract-id", 0, "Source.Type", "Forbidden.Ref", "stale ignore")
        };

        string output = _formatter.FormatUnmatchedForHumans(unmatched);

        Assert.That(output, Does.Contain("source_type: Source.Type"));
        Assert.That(output, Does.Contain("forbidden_reference: Forbidden.Ref"));
        Assert.That(output, Does.Contain("reason: stale ignore"));
    }

    [Test]
    public void PolicyProvenance_IsIncludedInHumanAndCiDiagnostics()
    {
        ArchitecturePolicySourceLocation primary = CreatePolicyLocation(
            "architecture/fragments/policy.yml", "contracts.strict[10]", sourceOrdinal: 1, encounterOrdinal: 10);
        ArchitecturePolicySourceLocation related = CreatePolicyLocation(
            "architecture/fragments/policy.yml", "contracts.strict[2]", sourceOrdinal: 1, encounterOrdinal: 2);
        var violation = new ArchitectureViolation("contract", "contract-id", "Source.Type", "Forbidden.Namespace", _reference1)
        {
            PolicyLocation = primary,
            RelatedPolicyLocations = [related]
        };
        var unmatched = new ArchitectureUnmatchedIgnoredViolation(
            "contract", "contract-id", 0, "Source.Type", "Forbidden.Ref", "stale ignore")
        {
            PolicyLocation = primary
        };
        var consistency = new PolicyConsistencyDiagnostic(
            "contract", "contract-id", "duplicate", "conflicting rules", [], [], [])
        {
            PolicyLocation = primary,
            RelatedPolicyLocations = [related]
        };
        var cycle = new ArchitectureCycleFinding("cycle contract", "cycle-id", "A -> B -> A")
        {
            PolicyLocation = primary
        };

        Assert.Multiple(() =>
        {
            Assert.That(_formatter.FormatViolationsForHumans([violation]), Does.Contain(
                "policy: architecture/fragments/policy.yml:contracts.strict[10]"));
            Assert.That(_formatter.FormatViolationsForHumans([violation]), Does.Contain(
                "related: architecture/fragments/policy.yml:contracts.strict[2]"));
            Assert.That(_formatter.FormatUnmatchedForHumans([unmatched]), Does.Contain(
                "policy: architecture/fragments/policy.yml:contracts.strict[10]"));
            Assert.That(_formatter.FormatPolicyConsistencyForHumans([consistency]), Does.Contain(
                "policy: architecture/fragments/policy.yml:contracts.strict[10]"));
            Assert.That(_formatter.FormatCyclesForHumans([cycle]), Does.Contain(
                "policy: architecture/fragments/policy.yml:contracts.strict[10]"));
        });

        using JsonDocument document = JsonDocument.Parse(_formatter.FormatResultForCiArtifacts(
            "strict", false, [violation], Array.Empty<string>(), [cycle], unmatched: [unmatched],
            policyConsistencyFindings: [consistency]));
        JsonElement policyLocation = document.RootElement.GetProperty("violations")[0].GetProperty("policy_location");
        JsonElement relatedLocation = document.RootElement.GetProperty("policy_consistency_findings")[0]
            .GetProperty("related_policy_locations")[0];
        JsonElement cycleLocation = document.RootElement.GetProperty("cycle_diagnostics")[0]
            .GetProperty("policy_location");

        Assert.Multiple(() =>
        {
            Assert.That(policyLocation.GetProperty("source_path").GetString(),
                Is.EqualTo("architecture/fragments/policy.yml"));
            Assert.That(policyLocation.GetProperty("contract_family").GetString(), Is.EqualTo("dependency"));
            Assert.That(policyLocation.GetProperty("contract_id").GetString(), Is.EqualTo("contract-id"));
            Assert.That(policyLocation.GetProperty("import_chain")[1].GetString(),
                Is.EqualTo("architecture/fragments/policy.yml"));
            Assert.That(relatedLocation.GetProperty("yaml_path").GetString(), Is.EqualTo("contracts.strict[2]"));
            Assert.That(document.RootElement.GetProperty("unmatched_ignored_violations")[0]
                .GetProperty("policy_location").GetProperty("source_ordinal").GetInt32(), Is.EqualTo(1));
            Assert.That(cycleLocation.GetProperty("yaml_path").GetString(), Is.EqualTo("contracts.strict[10]"));
        });
    }

    private static ArchitecturePolicySourceLocation CreatePolicyLocation(
        string sourcePath,
        string yamlPath,
        int sourceOrdinal,
        int encounterOrdinal)
    {
        var source = new ArchitecturePolicySourceDescriptor(
            "architecture/root.yml", sourcePath, ArchitecturePolicyDocumentRole.Fragment, sourceOrdinal,
            "architecture/root.yml", sourcePath, ["architecture/root.yml", sourcePath]);
        return new ArchitecturePolicySourceLocation(
            source, yamlPath, 3, 5, "dependency", "contract-id", encounterOrdinal);
    }
}
