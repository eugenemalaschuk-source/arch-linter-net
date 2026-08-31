using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ContractSurfaceExposureEvaluationTests
{
    [Test]
    public void DiagnosticKind_AppendsExposureKindWithoutRenumberingExistingKinds()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)ArchitectureDiagnosticKind.PublicApiSurface, Is.EqualTo(8));
            Assert.That((int)ArchitectureDiagnosticKind.AttributeUsage, Is.EqualTo(9));
            Assert.That((int)ArchitectureDiagnosticKind.ImportedExternalDiagnostic, Is.EqualTo(26));
            Assert.That((int)ArchitectureDiagnosticKind.ContractSurfaceExposure, Is.EqualTo(27));
        });
    }

    [Test]
    public void Execute_ReviewedPublicApiRootReportsNestedGenericExposureWithoutChangingRole()
    {
        Assembly assembly = typeof(SurfaceExposurePublicContract).Assembly;
        string assemblyName = assembly.GetName().Name!;
        var reviewedSurface = new ArchitecturePublicApiSurfaceContract
        {
            Id = "reviewed-api",
            Name = "reviewed-api",
            Assemblies = [assemblyName],
            SurfaceSelector = new ArchitecturePublicApiSurfaceSelector
            {
                NameSuffix = nameof(SurfaceExposurePublicContract),
            },
        };
        var exposure = new ArchitectureContractSurfaceExposureContract
        {
            Id = "no-forbidden-contract-types",
            Name = "no-forbidden-contract-types",
            Source = new ArchitectureContractSurfaceExposureSource
            {
                PublicApiSurface = reviewedSurface.Id,
            },
            Forbidden =
            [
                new ArchitecturePublicApiSurfaceSelector
                {
                    NameSuffix = nameof(SurfaceExposureForbiddenType),
                },
            ],
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "contract-surface-exposure",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Classification = new ArchitectureClassificationConfiguration
            {
                Attributes =
                {
                    new ArchitectureAttributeClassificationMapping
                    {
                        Attribute = typeof(SurfaceExposureValueObjectAttribute).FullName!,
                        Role = "ValueObject",
                    },
                },
            },
            Contracts = new ArchitectureContractGroups
            {
                AuditPublicApiSurface = [reviewedSurface],
                StrictContractSurfaceExposure = [exposure],
            },
        };
        var runner = new ArchitectureContractRunner(
            new ArchitectureAnalysisContext("/tmp", [assembly], Array.Empty<string>(), Array.Empty<string>()),
            document);

        ArchitectureContractExecutionResult result = new ArchitectureContractExecutor().Execute(
            runner.Session,
            "strict",
            new ArchitectureContractHandlerRegistry());

        ArchitectureViolation[] exposureViolations = result.Violations
            .Where(candidate => candidate.ContractId == exposure.Id)
            .ToArray();
        ArchitectureViolation violation = exposureViolations.Single(candidate =>
            (candidate.Payload as ContractSurfaceExposurePayload)?.ExposurePath.Contains(
                "generic_argument", StringComparison.Ordinal) == true);
        var payload = (ContractSurfaceExposurePayload)violation.Payload!;
        ArchitectureFinding finding = ArchitectureFindingMapper.FromViolation(violation, "strict");
        var diagnostic = (ContractSurfaceExposureDiagnostic)finding.Details;
        ArchitectureTypeClassificationResult role;

        Assert.Multiple(() =>
        {
            Assert.That(payload.TargetTypeName, Is.EqualTo(typeof(SurfaceExposureForbiddenType).FullName));
            Assert.That(payload.ExposurePath, Does.Contain("generic_argument"));
            Assert.That(payload.ReviewedPublicApiSurface, Is.EqualTo(reviewedSurface.Id));
            Assert.That(exposureViolations.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(diagnostic.ExposurePath, Is.EqualTo(payload.ExposurePath));
            Assert.That(finding.Identity!.ContractFamily, Is.EqualTo("contract_surface_exposure"));
            Assert.That(finding.Identity.TargetMember, Is.EqualTo(payload.CanonicalExposurePath));
            Assert.That(result.ApplicabilityExpectedEntries,
                Has.Some.Matches<ArchitectureApplicabilityExpectedEntry>(entry =>
                    entry.Family == "contract_surface_exposure" && entry.ControlIdentity == exposure.Id));
            Assert.That(result.ApplicabilityRecords,
                Has.Some.Matches<ArchitectureApplicabilityRecord>(record =>
                    record.Family == "contract_surface_exposure"
                    && record.ControlIdentity == exposure.Id
                    && record.State == ArchitectureApplicabilityRecordState.Evaluable));
            Assert.That(runner.Session.RoleIndex.TryGetRole(typeof(SurfaceExposurePublicContract), out role), Is.True);
            Assert.That(role.Role, Is.EqualTo("ValueObject"));
            Assert.That(runner.BaselineCandidates
                .Where(candidate => candidate.Identity?.ContractFamily == "contract_surface_exposure")
                .Select(candidate => candidate.Identity!.TargetMember)
                .Distinct(StringComparer.Ordinal)
                .Count(), Is.GreaterThanOrEqualTo(2));
        });
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class SurfaceExposureValueObjectAttribute : Attribute;

[SurfaceExposureValueObject]
public sealed class SurfaceExposurePublicContract
{
    public SurfaceExposureForbiddenType Direct => new();

    public SurfaceExposureEnvelope<SurfaceExposureForbiddenType> Read() => new();
}

public sealed class SurfaceExposureEnvelope<T>;

public sealed class SurfaceExposureForbiddenType;
