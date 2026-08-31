using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Discovery;
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

    [Test]
    public void Execute_FrameworkTargetOutsideAnalysisUniverse_IsReported()
    {
        Assembly assembly = typeof(SurfaceExposurePublicContract).Assembly;
        string assemblyName = assembly.GetName().Name!;
        var exposure = new ArchitectureContractSurfaceExposureContract
        {
            Id = "no-framework-collections",
            Name = "no-framework-collections",
            Source = new ArchitectureContractSurfaceExposureSource
            {
                Assemblies = [assemblyName],
                TypesMatching = new ArchitecturePublicApiSurfaceSelector
                {
                    NameSuffix = nameof(SurfaceExposurePublicContract),
                },
            },
            Forbidden =
            [
                new ArchitecturePublicApiSurfaceSelector
                {
                    Namespace = "System.Collections.Generic",
                    NameSuffix = "List`1",
                },
            ],
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "external-contract-surface-exposure",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Contracts = new ArchitectureContractGroups
            {
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
        ArchitectureViolation violation = result.Violations.First(candidate =>
            candidate.ContractId == exposure.Id
            && (candidate.Payload as ContractSurfaceExposurePayload)?.TargetTypeName == typeof(List<>).FullName);

        Assert.Multiple(() =>
        {
            Assert.That(violation.ForbiddenReferences.Single(), Does.Contain(typeof(List<>).Assembly.FullName));
            Assert.That(result.ApplicabilityRecords,
                Has.Some.Matches<ArchitectureApplicabilityRecord>(record =>
                    record.ControlIdentity == exposure.Id
                    && record.State == ArchitectureApplicabilityRecordState.Evaluable));
        });
    }

    [Test]
    public void Execute_IgnoredViolation_IsExcludedButRecordStaysEvaluable()
    {
        Assembly assembly = typeof(SurfaceExposurePublicContract).Assembly;
        string assemblyName = assembly.GetName().Name!;
        var exposure = new ArchitectureContractSurfaceExposureContract
        {
            Id = "no-framework-collections",
            Name = "no-framework-collections",
            Source = new ArchitectureContractSurfaceExposureSource
            {
                Assemblies = [assemblyName],
                TypesMatching = new ArchitecturePublicApiSurfaceSelector
                {
                    NameSuffix = nameof(SurfaceExposurePublicContract),
                },
            },
            Forbidden =
            [
                new ArchitecturePublicApiSurfaceSelector
                {
                    Namespace = "System.Collections.Generic",
                    NameSuffix = "List`1",
                },
            ],
            IgnoredViolations = [new ArchitectureIgnoredViolation { SourceType = "*", ForbiddenReference = "*", Reason = "test" }],
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "ignored-contract-surface-exposure",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Contracts = new ArchitectureContractGroups
            {
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

        Assert.Multiple(() =>
        {
            Assert.That(result.Violations.Any(candidate => candidate.ContractId == exposure.Id), Is.False);
            Assert.That(result.ApplicabilityRecords,
                Has.Some.Matches<ArchitectureApplicabilityRecord>(record =>
                    record.ControlIdentity == exposure.Id
                    && record.State == ArchitectureApplicabilityRecordState.Evaluable));
        });
    }

    [Test]
    public void Execute_ForbiddenSelectorMatchesNothing_IsUnassessable()
    {
        Assembly assembly = typeof(SurfaceExposurePublicContract).Assembly;
        string assemblyName = assembly.GetName().Name!;
        var exposure = new ArchitectureContractSurfaceExposureContract
        {
            Id = "no-framework-collections",
            Name = "no-framework-collections",
            Source = new ArchitectureContractSurfaceExposureSource
            {
                Assemblies = [assemblyName],
                TypesMatching = new ArchitecturePublicApiSurfaceSelector
                {
                    NameSuffix = nameof(SurfaceExposurePublicContract),
                },
            },
            Forbidden =
            [
                new ArchitecturePublicApiSurfaceSelector
                {
                    NameSuffix = "NoSuchTypeSuffixExists12345",
                },
            ],
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "unmatched-forbidden-contract-surface-exposure",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Contracts = new ArchitectureContractGroups
            {
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

        ArchitectureApplicabilityRecord record = result.ApplicabilityRecords.Single(r => r.ControlIdentity == exposure.Id);
        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Has.Some.EqualTo(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput));
        });
    }

    [Test]
    public void Execute_AssemblyFilterExcludesOtherTargetAssembly_OnlyScansSelectedAssembly()
    {
        Assembly assembly = typeof(SurfaceExposurePublicContract).Assembly;
        string assemblyName = assembly.GetName().Name!;
        Assembly otherAssembly = typeof(ArchitectureContractDocument).Assembly;
        string otherAssemblyName = otherAssembly.GetName().Name!;
        var exposure = new ArchitectureContractSurfaceExposureContract
        {
            Id = "no-framework-collections",
            Name = "no-framework-collections",
            Source = new ArchitectureContractSurfaceExposureSource
            {
                Assemblies = [assemblyName],
                TypesMatching = new ArchitecturePublicApiSurfaceSelector
                {
                    NameSuffix = nameof(SurfaceExposurePublicContract),
                },
            },
            Forbidden =
            [
                new ArchitecturePublicApiSurfaceSelector
                {
                    Namespace = "System.Collections.Generic",
                    NameSuffix = "List`1",
                },
            ],
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "assembly-filtered-contract-surface-exposure",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName, otherAssemblyName] },
            Contracts = new ArchitectureContractGroups
            {
                StrictContractSurfaceExposure = [exposure],
            },
        };
        var runner = new ArchitectureContractRunner(
            new ArchitectureAnalysisContext("/tmp", [assembly, otherAssembly], Array.Empty<string>(), Array.Empty<string>()),
            document);

        ArchitectureContractExecutionResult result = new ArchitectureContractExecutor().Execute(
            runner.Session,
            "strict",
            new ArchitectureContractHandlerRegistry());
        ArchitectureViolation violation = result.Violations.First(candidate =>
            candidate.ContractId == exposure.Id
            && (candidate.Payload as ContractSurfaceExposurePayload)?.TargetTypeName == typeof(List<>).FullName);

        Assert.That(((ContractSurfaceExposurePayload)violation.Payload!).SourceAssemblyName, Does.StartWith(assemblyName));
    }

    [Test]
    public void Execute_ProjectBasedSourceSelector_OnlyScansAssemblyOwnedByResolvedProject()
    {
        Assembly assembly = typeof(SurfaceExposurePublicContract).Assembly;
        string assemblyName = assembly.GetName().Name!;
        Assembly otherAssembly = typeof(ArchitectureContractDocument).Assembly;
        string otherAssemblyName = otherAssembly.GetName().Name!;
        var projectDiscovery = new ProjectDiscoveryResult(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ArchitectureProjectDiscoveryDiagnostic>())
        {
            DiscoveredProjects =
            [
                new ArchitectureDiscoveredProject(
                    $"src/SurfaceExposureProject/SurfaceExposureProject.csproj", assemblyName, Array.Empty<string>()),
            ],
        };
        var exposure = new ArchitectureContractSurfaceExposureContract
        {
            Id = "no-framework-collections",
            Name = "no-framework-collections",
            Source = new ArchitectureContractSurfaceExposureSource
            {
                Projects = ["SurfaceExposureProject"],
                TypesMatching = new ArchitecturePublicApiSurfaceSelector
                {
                    NameSuffix = nameof(SurfaceExposurePublicContract),
                },
            },
            Forbidden =
            [
                new ArchitecturePublicApiSurfaceSelector
                {
                    Namespace = "System.Collections.Generic",
                    NameSuffix = "List`1",
                },
            ],
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "project-filtered-contract-surface-exposure",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName, otherAssemblyName] },
            Contracts = new ArchitectureContractGroups
            {
                StrictContractSurfaceExposure = [exposure],
            },
        };
        var runner = new ArchitectureContractRunner(
            new ArchitectureAnalysisContext(
                "/tmp", [assembly, otherAssembly], Array.Empty<string>(), Array.Empty<string>(),
                projectDiscovery: projectDiscovery),
            document);

        ArchitectureContractExecutionResult result = new ArchitectureContractExecutor().Execute(
            runner.Session,
            "strict",
            new ArchitectureContractHandlerRegistry());
        ArchitectureViolation violation = result.Violations.First(candidate =>
            candidate.ContractId == exposure.Id
            && (candidate.Payload as ContractSurfaceExposurePayload)?.TargetTypeName == typeof(List<>).FullName);

        Assert.Multiple(() =>
        {
            Assert.That(((ContractSurfaceExposurePayload)violation.Payload!).SourceAssemblyName, Does.StartWith(assemblyName));
            Assert.That(result.ApplicabilityRecords,
                Has.Some.Matches<ArchitectureApplicabilityRecord>(record =>
                    record.ControlIdentity == exposure.Id
                    && record.State == ArchitectureApplicabilityRecordState.Evaluable));
        });
    }

    [Test]
    public void Execute_ContractNotSelected_SkipsExposureEvaluationEntirely()
    {
        Assembly assembly = typeof(SurfaceExposurePublicContract).Assembly;
        string assemblyName = assembly.GetName().Name!;
        var exposure = new ArchitectureContractSurfaceExposureContract
        {
            Id = "no-framework-collections",
            Name = "no-framework-collections",
            Source = new ArchitectureContractSurfaceExposureSource
            {
                Assemblies = [assemblyName],
                TypesMatching = new ArchitecturePublicApiSurfaceSelector
                {
                    NameSuffix = nameof(SurfaceExposurePublicContract),
                },
            },
            Forbidden =
            [
                new ArchitecturePublicApiSurfaceSelector
                {
                    Namespace = "System.Collections.Generic",
                    NameSuffix = "List`1",
                },
            ],
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "external-contract-surface-exposure",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Contracts = new ArchitectureContractGroups
            {
                StrictContractSurfaceExposure = [exposure],
            },
        };
        var runner = new ArchitectureContractRunner(
            new ArchitectureAnalysisContext("/tmp", [assembly], Array.Empty<string>(), Array.Empty<string>()),
            document,
            selectedContractIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "some-other-contract" });

        ArchitectureContractExecutionResult result = new ArchitectureContractExecutor().Execute(
            runner.Session,
            "strict",
            new ArchitectureContractHandlerRegistry());

        Assert.Multiple(() =>
        {
            Assert.That(result.Violations.Any(candidate => candidate.ContractId == exposure.Id), Is.False);
            Assert.That(result.ApplicabilityRecords.Any(record => record.ControlIdentity == exposure.Id), Is.False);
        });
    }

    [Test]
    public void Execute_ReviewedPublicApiSnapshotErrorDoesNotMakeExposureUnassessable()
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
            ApiSnapshotError = "The reviewed snapshot is unavailable.",
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
            Name = "snapshot-independent-contract-surface-exposure",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface = [reviewedSurface],
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

        Assert.Multiple(() =>
        {
            Assert.That(result.Violations, Has.Some.Matches<ArchitectureViolation>(violation =>
                violation.ContractId == exposure.Id));
            Assert.That(result.ApplicabilityRecords,
                Has.Some.Matches<ArchitectureApplicabilityRecord>(record =>
                    record.ControlIdentity == exposure.Id
                    && record.State == ArchitectureApplicabilityRecordState.Evaluable));
        });
    }

    [Test]
    public void Execute_PartiallyLoadableTypeUniverseWithUnloadableFieldRoot_IsUnassessable()
    {
        using UnloadableFieldFixture fixture = UnloadableFieldFixture.Create(
            includeUnloadableType: true, includeUnloadableField: false);
        string assemblyName = fixture.ConsumerAssembly.GetName().Name!;
        var exposure = new ArchitectureContractSurfaceExposureContract
        {
            Id = "no-forbidden-contract-types",
            Name = "no-forbidden-contract-types",
            Source = new ArchitectureContractSurfaceExposureSource
            {
                Assemblies = [assemblyName],
                TypesMatching = new ArchitecturePublicApiSurfaceSelector { NameSuffix = "FieldReference" },
            },
            Forbidden =
            [
                new ArchitecturePublicApiSurfaceSelector { NameSuffix = nameof(SurfaceExposureForbiddenType) },
            ],
        };
        var document = new ArchitectureContractDocument
        {
            Version = 1,
            Name = "partially-loadable-type-universe-contract-surface-exposure",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Contracts = new ArchitectureContractGroups
            {
                StrictContractSurfaceExposure = [exposure],
            },
        };
        var runner = new ArchitectureContractRunner(
            new ArchitectureAnalysisContext(Path.GetTempPath(), [fixture.ConsumerAssembly], Array.Empty<string>(), Array.Empty<string>()),
            document);

        ArchitectureContractExecutionResult result = new ArchitectureContractExecutor().Execute(
            runner.Session,
            "strict",
            new ArchitectureContractHandlerRegistry());

        ArchitectureApplicabilityRecord record = result.ApplicabilityRecords.Single(r => r.ControlIdentity == exposure.Id);
        Assert.Multiple(() =>
        {
            Assert.That(runner.Session.TypeIndex.HasCompleteTypeUniverse, Is.False);
            Assert.That(record.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
            Assert.That(record.Reasons.Select(reason => reason.Code),
                Has.Some.EqualTo(ArchitectureApplicabilityReasonCodes.MissingRequiredInput));
        });
    }

    [Test]
    public void Execute_UnknownPublicApiSurfaceReference_IsUnassessable()
    {
        Assembly assembly = typeof(SurfaceExposurePublicContract).Assembly;
        string assemblyName = assembly.GetName().Name!;
        var exposure = new ArchitectureContractSurfaceExposureContract
        {
            Id = "no-forbidden-contract-types",
            Name = "no-forbidden-contract-types",
            Source = new ArchitectureContractSurfaceExposureSource
            {
                PublicApiSurface = "does-not-exist",
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
            Name = "unknown-public-api-surface-reference",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Contracts = new ArchitectureContractGroups
            {
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

        Assert.That(result.ApplicabilityRecords,
            Has.Some.Matches<ArchitectureApplicabilityRecord>(record =>
                record.ControlIdentity == exposure.Id
                && record.State == ArchitectureApplicabilityRecordState.Unassessable));
    }

    [Test]
    public void Execute_ReviewedPublicApiSurfaceWithUnresolvedAssembly_IsUnassessable()
    {
        Assembly assembly = typeof(SurfaceExposurePublicContract).Assembly;
        string assemblyName = assembly.GetName().Name!;
        var reviewedSurface = new ArchitecturePublicApiSurfaceContract
        {
            Id = "reviewed-api",
            Name = "reviewed-api",
            Assemblies = [assemblyName, "ArchLinterNet.DoesNotExist"],
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
            Name = "reviewed-public-api-surface-unresolved-assembly",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface = [reviewedSurface],
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

        Assert.That(result.ApplicabilityRecords,
            Has.Some.Matches<ArchitectureApplicabilityRecord>(record =>
                record.ControlIdentity == exposure.Id
                && record.State == ArchitectureApplicabilityRecordState.Unassessable));
    }

    [Test]
    public void Execute_ReviewedPublicApiSurfaceWithMultipleMatchingRoots_IsEvaluable()
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
                NamePrefix = "SurfaceExposure",
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
            Name = "reviewed-public-api-surface-multiple-roots",
            Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
            Contracts = new ArchitectureContractGroups
            {
                StrictPublicApiSurface = [reviewedSurface],
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

        Assert.Multiple(() =>
        {
            Assert.That(result.Violations, Has.Some.Matches<ArchitectureViolation>(violation =>
                violation.ContractId == exposure.Id));
            Assert.That(result.ApplicabilityRecords,
                Has.Some.Matches<ArchitectureApplicabilityRecord>(record =>
                    record.ControlIdentity == exposure.Id
                    && record.State == ArchitectureApplicabilityRecordState.Evaluable));
        });
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class SurfaceExposureValueObjectAttribute : Attribute;

[SurfaceExposureValueObject]
public sealed class SurfaceExposurePublicContract
{
    public static SurfaceExposureForbiddenType Direct => new();

    public static SurfaceExposureEnvelope<SurfaceExposureForbiddenType> Read() => new();

    public static List<string> FrameworkValues => [];
}

public sealed class SurfaceExposureEnvelope<T>;

public sealed class SurfaceExposureForbiddenType;
