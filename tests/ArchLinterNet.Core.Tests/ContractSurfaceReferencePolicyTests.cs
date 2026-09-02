using System.Reflection;
using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Scanning;
using ArchLinterNet.Testing;
using NUnit.Framework;
using Domain = ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Domain;
using Editor = ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Library.Editor;
using Fixtures = ArchLinterNet.Core.Tests.ReferencePolicyFixtures;
using Persistence = ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Persistence;
using Runtime = ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Library.Runtime;
using Transport = ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Transport;
using V1 = ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Contracts.V1;
using V2 = ArchLinterNet.Core.Tests.ReferencePolicyFixtures.Server.Contracts.V2;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ContractSurfaceReferencePolicyTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-reference-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Test]
    public void Load_ReferencePolicy_ComposesExistingFamiliesAndKeepsApiMembershipOrthogonalToRole()
    {
        string policyPath = WritePolicy();
        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);

        Assert.Multiple(() =>
        {
            Assert.That(document.Contracts.StrictPublicApiSurface.Select(contract => contract.Id),
                Is.EqualTo(["server-v1-reviewed-surface"]));
            Assert.That(document.Contracts.AuditPublicApiSurface.Select(contract => contract.Id),
                Is.EqualTo(["runtime-reviewed-surface"]));
            Assert.That(document.Contracts.StrictAttributeUsage.Select(contract => contract.Id),
                Is.EqualTo(["transport-marker-placement"]));
            Assert.That(document.Contracts.AuditAttributeUsage.Select(contract => contract.Id),
                Is.EqualTo(["serialization-marker-placement"]));
            Assert.That(document.Contracts.StrictContractSurfaceExposure.Select(contract => contract.Id),
                Is.EqualTo(["server-v1-no-internal-types"]));
            Assert.That(document.Contracts.AuditContractSurfaceExposure.Select(contract => contract.Id),
                Is.EqualTo(["runtime-no-editor-types"]));
            Assert.That(document.Contracts.StrictVersionedContractSurfaceIsolation.Select(contract => contract.Id),
                Is.EqualTo(["server-v1-isolation"]));
            Assert.That(document.Contracts.AuditVersionedContractSurfaceIsolation.Select(contract => contract.Id),
                Is.EqualTo(["server-v1-isolation-audit"]));
            Assert.That(document.Classification.Attributes.Select(mapping => mapping.Attribute),
                Does.Not.Contain(Fixtures.ContractSurfaceReferencePolicyTestFixtures.PublicApiMarkerName));
        });

        (ArchitectureContractExecutionResult result, ArchitectureContractRunner runner) =
            Execute(document, "strict");
        ArchitectureTypeClassificationResult role;

        Assert.Multiple(() =>
        {
            Assert.That(runner.Session.RoleIndex.TryGetRole(typeof(V1.OrderContractV1), out role), Is.True);
            Assert.That(role.Role, Is.EqualTo("ValueObject"),
                "PublicApiContract is membership-only and must not replace the type's role.");
            Assert.That(runner.Session.RoleIndex.TryGetRole(typeof(Runtime.RuntimeEditorBridge), out role), Is.True);
            Assert.That(role.Role, Is.EqualTo("ValueObject"),
                "Interface-selected runtime membership must also preserve the independently mapped role.");

            Assert.That(result.Violations, Has.Some.Matches<ArchitectureViolation>(violation =>
                violation.ContractId == "server-v1-reviewed-surface"
                && (violation.Payload as PublicApiSurfacePayload)?.UnselectedFirstPartyDependency ==
                typeof(Domain.OrderEntity).FullName));
            Assert.That(result.Violations, Has.Some.Matches<ArchitectureViolation>(violation =>
                violation.ContractId == "server-v1-no-internal-types"
                && (violation.Payload as ContractSurfaceExposurePayload)?.TargetTypeName ==
                typeof(Domain.OrderEntity).FullName
                && (violation.Payload as ContractSurfaceExposurePayload)?.ExposurePath.Contains(
                    "generic_argument", StringComparison.Ordinal) == true));
            Assert.That(result.Violations, Has.Some.Matches<ArchitectureViolation>(violation =>
                violation.ContractId == "server-v1-no-internal-types"
                && (violation.Payload as ContractSurfaceExposurePayload)?.TargetTypeName ==
                typeof(Persistence.OrderRecord).FullName));
            Assert.That(result.Violations, Has.Some.Matches<ArchitectureViolation>(violation =>
                violation.ContractId == "server-v1-isolation"
                && (violation.Payload as ContractSurfaceExposurePayload)?.TargetTypeName ==
                typeof(V2.OrderContractV2).FullName));
            Assert.That(result.Violations, Has.Some.Matches<ArchitectureViolation>(violation =>
                violation.ContractId == "server-v1-isolation"
                && (violation.Payload as ContractSurfaceExposurePayload)?.TargetTypeName ==
                typeof(Transport.TransportEnvelope<>).FullName));
            Assert.That(result.Violations, Has.Some.Matches<ArchitectureViolation>(violation =>
                violation.ContractId == "transport-marker-placement"
                && violation.SourceType == typeof(Persistence.OrderRecord).FullName
                && (violation.Payload as AttributeUsagePayload)?.MatchedAttribute ==
                Fixtures.ContractSurfaceReferencePolicyTestFixtures.TransportMarkerName));
            Assert.That(result.Violations, Has.Some.Matches<ArchitectureViolation>(violation =>
                violation.ContractId == "transport-marker-placement"
                && violation.SourceType == typeof(Domain.OrderEntity).FullName
                && (violation.Payload as AttributeUsagePayload)?.MatchedAttribute ==
                Fixtures.ContractSurfaceReferencePolicyTestFixtures.TransportMarkerName));
            Assert.That(EvaluableRecords(result).Select(record => record.ControlIdentity),
                Is.EquivalentTo(["server-v1-no-internal-types", "server-v1-isolation"]));
        });
    }

    [Test]
    public void Execute_AuditReferencePolicy_ReusesTypedFactsAndProjectsThroughAllCurrentSurfaces()
    {
        string policyPath = WritePolicy();
        ArchitectureContractDocument document = new ArchitecturePolicyDocumentLoader().Load(policyPath);
        (ArchitectureContractExecutionResult strict, ArchitectureContractRunner runner) = Execute(document, "strict");
        ArchitectureContractExecutionResult audit = Execute(runner, "audit");

        ContractSurfaceExposurePayload strictNested = FindExposure(
            strict, "server-v1-no-internal-types", typeof(Domain.OrderEntity), "generic_argument");
        ContractSurfaceExposurePayload auditEditor = FindExposure(
            audit, "runtime-no-editor-types", typeof(Editor.EditorSettings));
        ContractSurfaceExposurePayload strictVersioned = FindExposure(
            strict, "server-v1-isolation", typeof(V2.OrderContractV2), "generic_argument");
        ContractSurfaceExposurePayload auditVersioned = FindExposure(
            audit, "server-v1-isolation-audit", typeof(V2.OrderContractV2), "generic_argument");

        Assert.Multiple(() =>
        {
            Assert.That(strictNested.ExposurePath, Does.Contain("generic_argument"));
            Assert.That(strictNested.SourceSurface, Is.EqualTo("exported"));
            Assert.That(strictNested.ReviewedPublicApiSurface, Is.EqualTo("server-v1-reviewed-surface"));
            Assert.That(auditEditor.ExposurePath, Does.Contain("member:Property:EditorSettings"));
            Assert.That(auditEditor.ReviewedPublicApiSurface, Is.EqualTo("runtime-reviewed-surface"));
            Assert.That(strictVersioned.ExposurePath, Is.EqualTo(auditVersioned.ExposurePath));
            Assert.That(strictVersioned.CanonicalExposurePath, Is.EqualTo(auditVersioned.CanonicalExposurePath));
            Assert.That(strictVersioned.TargetAssemblyName, Is.EqualTo(auditVersioned.TargetAssemblyName));

            Assert.That(audit.Violations, Has.Some.Matches<ArchitectureViolation>(violation =>
                violation.ContractId == "serialization-marker-placement"
                && violation.SourceType == typeof(Editor.EditorSettings).FullName
                && (violation.Payload as AttributeUsagePayload)?.MatchedAttribute ==
                Fixtures.ContractSurfaceReferencePolicyTestFixtures.SerializationMarkerName));
            Assert.That(audit.Violations, Has.Some.Matches<ArchitectureViolation>(violation =>
                violation.ContractId == "runtime-no-editor-types"
                && (violation.Payload as ContractSurfaceExposurePayload)?.TargetTypeName ==
                typeof(Editor.EditorSettings).FullName));
            Assert.That(EvaluableRecords(audit).Select(record => record.ControlIdentity),
                Is.EquivalentTo(["runtime-no-editor-types", "server-v1-isolation-audit"]));
        });

        AssertProjectionParity(
            strict, "strict", "server-v1-no-internal-types", typeof(Domain.OrderEntity), "generic_argument");
        AssertProjectionParity(audit, "audit", "runtime-no-editor-types", typeof(Editor.EditorSettings));

        using ArchitectureValidationSnapshotSession testingSnapshot =
            ArchitectureAssertions.FromPolicy(policyPath).CreateSnapshot();
        ArchitectureValidationResult testingStrict = testingSnapshot.ValidateStrict();
        ArchitectureValidationResult testingAudit = testingSnapshot.ValidateAudit();
        ArchitectureFinding strictTestingFinding = FindTestingExposure(
            testingStrict, "server-v1-no-internal-types", typeof(Domain.OrderEntity), "generic_argument");
        ArchitectureFinding auditTestingFinding = FindTestingExposure(
            testingAudit, "runtime-no-editor-types", typeof(Editor.EditorSettings));

        Assert.Multiple(() =>
        {
            Assert.That(strictTestingFinding.Details, Is.TypeOf<ContractSurfaceExposureDiagnostic>());
            Assert.That(((ContractSurfaceExposureDiagnostic)strictTestingFinding.Details).ExposurePath,
                Is.EqualTo(strictNested.ExposurePath));
            Assert.That(((ContractSurfaceExposureDiagnostic)auditTestingFinding.Details).ExposurePath,
                Is.EqualTo(auditEditor.ExposurePath));
            Assert.That(strictTestingFinding.Identity!.TargetType, Is.EqualTo(typeof(Domain.OrderEntity).FullName));
            Assert.That(auditTestingFinding.Identity!.TargetType, Is.EqualTo(typeof(Editor.EditorSettings).FullName));
        });
    }

    private string WritePolicy()
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, Fixtures.ContractSurfaceReferencePolicyTestFixtures.PolicyYaml);
        return path;
    }

    private static (ArchitectureContractExecutionResult Result, ArchitectureContractRunner Runner) Execute(
        ArchitectureContractDocument document, string mode)
    {
        Assembly assembly = typeof(ContractSurfaceReferencePolicyTests).Assembly;
        var runner = new ArchitectureContractRunner(
            new ArchitectureAnalysisContext("/tmp", [assembly], Array.Empty<string>(), Array.Empty<string>()),
            document);
        return (Execute(runner, mode), runner);
    }

    private static ArchitectureContractExecutionResult Execute(
        ArchitectureContractRunner runner, string mode) => new ArchitectureContractExecutor().Execute(
            runner.Session, mode, new ArchitectureContractHandlerRegistry());

    private static ArchitectureApplicabilityRecord[] EvaluableRecords(ArchitectureContractExecutionResult result)
    {
        return result.ApplicabilityRecords
            .Where(record => record.State == ArchitectureApplicabilityRecordState.Evaluable
                && record.Family is "attribute_usage" or "contract_surface_exposure"
                    or "versioned_contract_surface_isolation" or "public_api_surface")
            .ToArray();
    }

    private static ContractSurfaceExposurePayload FindExposure(
        ArchitectureContractExecutionResult result, string contractId, Type targetType, string? requiredPathSegment = null)
    {
        return result.Violations
            .Where(candidate =>
                candidate.ContractId == contractId
                && (candidate.Payload as ContractSurfaceExposurePayload)?.TargetTypeName == targetType.FullName
                && (requiredPathSegment is null || (candidate.Payload as ContractSurfaceExposurePayload)?.ExposurePath.Contains(
                    requiredPathSegment, StringComparison.Ordinal) == true))
            .Select(candidate => (ContractSurfaceExposurePayload)candidate.Payload!)
            .OrderBy(payload => payload.CanonicalExposurePath, StringComparer.Ordinal)
            .First();
    }

    private static void AssertProjectionParity(
        ArchitectureContractExecutionResult result, string mode, string contractId, Type targetType,
        string? requiredPathSegment = null)
    {
        ArchitectureViolation violation = result.Violations
            .Where(candidate =>
                candidate.ContractId == contractId
                && (candidate.Payload as ContractSurfaceExposurePayload)?.TargetTypeName == targetType.FullName
                && (requiredPathSegment is null || (candidate.Payload as ContractSurfaceExposurePayload)?.ExposurePath.Contains(
                    requiredPathSegment, StringComparison.Ordinal) == true))
            .OrderBy(candidate => ((ContractSurfaceExposurePayload)candidate.Payload!).CanonicalExposurePath,
                StringComparer.Ordinal)
            .First();
        var payload = (ContractSurfaceExposurePayload)violation.Payload!;
        ArchitectureFinding finding = ArchitectureFindingMapper.FromViolation(violation, mode);
        string human = new ArchitectureDiagnosticFormatter().FormatViolationsForHumans([violation]);
        string json = JsonSerializer.Serialize(ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding));
        using JsonDocument jsonDocument = JsonDocument.Parse(json);
        string sarif = new ArchitectureSarifFormatter().FormatResultAsSarif(
            mode, [violation], Array.Empty<string>(), "reference-policy-tests");
        using JsonDocument sarifDocument = JsonDocument.Parse(sarif);
        JsonElement sarifProperties = sarifDocument.RootElement.GetProperty("runs")[0]
            .GetProperty("results")[0].GetProperty("properties");

        Assert.Multiple(() =>
        {
            Assert.That(human, Does.Contain($"target_type: {payload.TargetTypeName}"));
            Assert.That(human, Does.Contain($"exposure_path: {payload.ExposurePath}"));
            Assert.That(jsonDocument.RootElement.GetProperty("kind").GetString(),
                Is.EqualTo("contract_surface_exposure"));
            Assert.That(jsonDocument.RootElement.GetProperty("target_type").GetString(),
                Is.EqualTo(payload.TargetTypeName));
            Assert.That(jsonDocument.RootElement.GetProperty("exposure_path").GetString(),
                Is.EqualTo(payload.ExposurePath));
            Assert.That(sarifProperties.GetProperty("target_type").GetString(), Is.EqualTo(payload.TargetTypeName));
            Assert.That(sarifProperties.GetProperty("exposure_path").GetString(), Is.EqualTo(payload.ExposurePath));
            Assert.That(sarifProperties.GetProperty("canonical_exposure_path").GetString(),
                Is.EqualTo(payload.CanonicalExposurePath));
        });
    }

    private static ArchitectureFinding FindTestingExposure(
        ArchitectureValidationResult result, string contractId, Type targetType, string? requiredPathSegment = null)
    {
        return result.Findings
            .Where(finding =>
                finding.ContractId == contractId
                && finding.Details is ContractSurfaceExposureDiagnostic diagnostic
                && diagnostic.TargetTypeName == targetType.FullName
                && (requiredPathSegment is null || diagnostic.ExposurePath?.Contains(
                    requiredPathSegment, StringComparison.Ordinal) == true))
            .OrderBy(finding => finding.Identity!.TargetMember, StringComparer.Ordinal)
            .First();
    }
}
