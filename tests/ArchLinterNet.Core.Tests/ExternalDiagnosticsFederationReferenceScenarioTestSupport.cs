using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Shared evidence repository lifecycle and SARIF builders for external-diagnostics fixtures.
/// It intentionally contains no test methods; each concrete fixture owns its scenarios.
/// </summary>
public abstract class ExternalDiagnosticsFederationReferenceScenarioTestSupport
{
    private protected SarifEvidenceTestRepository Repository { get; private set; } = null!;

    [SetUp]
    public void SetUpExternalDiagnosticsFixture()
    {
        Repository = new SarifEvidenceTestRepository();
    }

    [TearDown]
    public void TearDownExternalDiagnosticsFixture()
    {
        Repository.Dispose();
    }

    protected SarifEvidenceReadResult Read(
        ArchitectureExternalEvidenceRequirement requirement,
        string path,
        SarifEvidenceProducerContext? producer = null,
        string assessmentScope = "scope")
    {
        producer ??= new SarifEvidenceProducerContext(requirement.Id, "repo", "revision", assessmentScope);
        return new SarifEvidenceReader().Read(
            requirement,
            Repository.Root,
            new SarifEvidenceArtifactReference(path, requirement.Id, producer),
            new SarifEvidenceAssessmentContext("repo", "revision", assessmentScope));
    }

    protected static SarifExternalDiagnosticSelectionResult Select(SarifEvidenceReadResult read) =>
        new SarifExternalDiagnosticSelector().Select([new SarifExternalDiagnosticSelectionInput(read)]);

    protected static ArchitectureExternalEvidenceRequirement Requirement(
        string id,
        Dictionary<string, string>? severity = null,
        IReadOnlyList<string>? ruleIds = null) => new()
        {
            Id = id,
            Format = "sarif",
            Required = true,
            Tool = "Synthetic.Scanner",
            ToolVersion = "1.0",
            Run = "assessment-42",
            RequireRepository = true,
            RequireRevision = true,
            RequireScope = true,
            DiagnosticFilter = new ArchitectureExternalEvidenceDiagnosticFilter
            {
                RuleIds = ruleIds?.ToList() ?? [],
                Severity = severity ?? new Dictionary<string, string> { ["error"] = "strict" },
            },
        };

    protected static string Results(params string[] results) => "[" + string.Join(",", results) + "]";

    protected static string Result(
        string ruleId,
        string level,
        string path,
        string message,
        string? fingerprint = null,
        string? partialFingerprint = null) =>
        "{\"ruleId\":\"" + ruleId + "\",\"message\":{\"text\":\"" + message + "\"},\"level\":\"" + level
        + "\",\"properties\":{\"project\":\"App\"},\"locations\":[{\"physicalLocation\":{\"artifactLocation\":{\"uri\":\""
        + path + "\"},\"region\":{\"startLine\":7,\"startColumn\":3}}}]"
        + (fingerprint is null ? string.Empty : ",\"fingerprints\":" + fingerprint)
        + (partialFingerprint is null ? string.Empty : ",\"partialFingerprints\":" + partialFingerprint)
        + "}";

    protected static string Sarif(
        string results,
        string? repository = "repo",
        string? revision = "revision",
        string invocation = "true",
        bool includeInvocations = true,
        string? marker = null)
    {
        string[] bindings = [];
        if (repository is not null)
        {
            bindings = [.. bindings, "\"repositoryUri\":\"" + repository + "\""];
        }

        if (revision is not null)
        {
            bindings = [.. bindings, "\"revisionId\":\"" + revision + "\""];
        }

        string provenance = bindings.Length == 0 ? "[]" : "[{" + string.Join(",", bindings) + "}]";
        string invocationJson = includeInvocations
            ? "\"invocations\":[{\"executionSuccessful\":" + invocation + "}],"
            : string.Empty;
        string markerJson = marker is null ? string.Empty : ",\"properties\":{\"marker\":\"" + marker + "\"}";
        return "{\"version\":\"2.1.0\",\"runs\":[{\"tool\":{\"driver\":{\"name\":\"Synthetic.Scanner\",\"version\":\"1.0\","
            + "\"rules\":[{\"id\":\"SEC100\",\"properties\":{\"tags\":[\"security\"]}},{\"id\":\"PUBLICAPI001\",\"properties\":{\"tags\":[\"compatibility\"]}}]}},"
            + "\"automationDetails\":{\"id\":\"assessment-42\"}," + invocationJson
            + "\"versionControlProvenance\":" + provenance + markerJson + ",\"results\":" + results + "}]}";
    }
}
