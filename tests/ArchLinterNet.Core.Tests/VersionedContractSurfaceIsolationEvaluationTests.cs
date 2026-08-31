using System.Reflection;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Execution.Abstractions;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Scanning;
using NUnit.Framework;
using Transport = ArchLinterNet.Core.Tests.VersionedIsolation.Transport;
using V1 = ArchLinterNet.Core.Tests.VersionedIsolation.V1;
using V2 = ArchLinterNet.Core.Tests.VersionedIsolation.V2;

namespace ArchLinterNet.Core.Tests
{

    [TestFixture]
    public sealed class VersionedContractSurfaceIsolationEvaluationTests
    {
        [Test]
        public void Execute_StrictIsolationReportsDirectNestedAndInternalSurfaceLeaksWithStableTargetIdentity()
        {
            ArchitectureContractExecutionResult result = Execute(CreateContract());
            ArchitectureViolation[] violations = result.Violations
                .Where(violation => violation.ContractId == "v1-isolation")
                .ToArray();
            ContractSurfaceExposurePayload[] payloads = violations
                .Select(violation => AssertPayload(violation))
                .ToArray();

            ContractSurfaceExposurePayload nested = payloads.First(payload =>
                payload.TargetTypeName == typeof(V2.Customer).FullName
                && payload.ExposurePath.Contains("generic_argument", StringComparison.Ordinal));
            ContractSurfaceExposurePayload v2 = payloads.First(payload =>
                payload.TargetTypeName == typeof(V2.Customer).FullName);
            ContractSurfaceExposurePayload transport = payloads.First(payload =>
                payload.TargetTypeName == typeof(Transport.Customer).FullName);

            Assert.Multiple(() =>
            {
                Assert.That(violations, Has.Length.GreaterThanOrEqualTo(3));
                Assert.That(payloads, Has.All.Matches<ContractSurfaceExposurePayload>(payload =>
                    payload.SourceSurface == "v1-contracts"));
                Assert.That(nested.CanonicalExposurePath, Does.Contain("generic_argument"));
                Assert.That(v2.TargetAssemblyName, Is.EqualTo(transport.TargetAssemblyName));
                Assert.That(v2.TargetTypeName, Is.Not.EqualTo(transport.TargetTypeName));
                Assert.That(result.ApplicabilityExpectedEntries,
                    Has.Some.Matches<ArchitectureApplicabilityExpectedEntry>(entry =>
                        entry.Family == "versioned_contract_surface_isolation"
                        && entry.ControlIdentity == "v1-isolation"));
                Assert.That(result.ApplicabilityRecords,
                    Has.Some.Matches<ArchitectureApplicabilityRecord>(record =>
                        record.Family == "versioned_contract_surface_isolation"
                        && record.ControlIdentity == "v1-isolation"
                        && record.State == ArchitectureApplicabilityRecordState.Evaluable));
            });
        }

        [Test]
        public void Execute_AuditIsolationUsesExposurePayloadAndBaselineIdentity()
        {
            ArchitectureVersionedContractSurfaceIsolationContract contract = CreateContract();
            var document = CreateDocument(contract, audit: true);
            Assembly assembly = typeof(V1.Contract).Assembly;
            var runner = new ArchitectureContractRunner(
                new ArchitectureAnalysisContext("/tmp", [assembly], Array.Empty<string>(), Array.Empty<string>()),
                document);

            ArchitectureContractExecutionResult result = new ArchitectureContractExecutor().Execute(
                runner.Session,
                "audit",
                new ArchitectureContractHandlerRegistry());
            ArchitectureViolation violation = result.Violations.First(candidate =>
                candidate.ContractId == contract.Id
                && (candidate.Payload as ContractSurfaceExposurePayload)?.TargetTypeName == typeof(V2.Customer).FullName);

            Assert.Multiple(() =>
            {
                Assert.That(violation.Payload, Is.TypeOf<ContractSurfaceExposurePayload>());
                Assert.That(runner.BaselineCandidates,
                    Has.Some.Matches<ArchitectureBaselineCandidate>(candidate =>
                        candidate.Identity?.ContractFamily == "versioned_contract_surface_isolation"
                        && candidate.Identity.TargetType == typeof(V2.Customer).FullName));
                Assert.That(result.ApplicabilityRecords,
                    Has.Some.Matches<ArchitectureApplicabilityRecord>(record =>
                        record.ControlIdentity == contract.Id
                        && record.State == ArchitectureApplicabilityRecordState.Evaluable));
            });
        }

        [Test]
        public void Execute_ForbiddenSurfaceWithZeroMatchesIsUnassessable()
        {
            ArchitectureVersionedContractSurfaceIsolationContract contract = CreateContract();
            contract.Surfaces.Single(surface => surface.Id == "v2-contracts").TypesMatching =
                new ArchitecturePublicApiSurfaceSelector { NameSuffix = "NoVersionedIsolationTypeMatches" };

            ArchitectureContractExecutionResult result = Execute(contract);
            ArchitectureApplicabilityRecord record = result.ApplicabilityRecords.Single(item => item.ControlIdentity == contract.Id);

            Assert.Multiple(() =>
            {
                Assert.That(record.Family, Is.EqualTo("versioned_contract_surface_isolation"));
                Assert.That(record.State, Is.EqualTo(ArchitectureApplicabilityRecordState.Unassessable));
                Assert.That(record.Reasons.Select(reason => reason.Code),
                    Has.Some.EqualTo(ArchitectureApplicabilityReasonCodes.UnexpectedEmptyInput));
            });
        }

        private static ArchitectureContractExecutionResult Execute(ArchitectureVersionedContractSurfaceIsolationContract contract)
        {
            Assembly assembly = typeof(V1.Contract).Assembly;
            var runner = new ArchitectureContractRunner(
                new ArchitectureAnalysisContext("/tmp", [assembly], Array.Empty<string>(), Array.Empty<string>()),
                CreateDocument(contract, audit: false));

            return new ArchitectureContractExecutor().Execute(
                runner.Session,
                "strict",
                new ArchitectureContractHandlerRegistry());
        }

        private static ArchitectureContractDocument CreateDocument(
            ArchitectureVersionedContractSurfaceIsolationContract contract,
            bool audit)
        {
            string assemblyName = typeof(V1.Contract).Assembly.GetName().Name!;
            return new ArchitectureContractDocument
            {
                Version = 1,
                Name = "versioned-contract-surface-isolation",
                Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = [assemblyName] },
                Contracts = new ArchitectureContractGroups
                {
                    StrictVersionedContractSurfaceIsolation = audit ? [] : [contract],
                    AuditVersionedContractSurfaceIsolation = audit ? [contract] : [],
                },
            };
        }

        private static ArchitectureVersionedContractSurfaceIsolationContract CreateContract() =>
            new()
            {
                Id = "v1-isolation",
                Name = "v1-isolation",
                Surfaces =
                [
                    new ArchitectureVersionedContractSurfaceIsolationSurface
                {
                    Id = "v1-contracts",
                    TypesMatching = new ArchitecturePublicApiSurfaceSelector { Namespace = typeof(V1.Contract).Namespace! },
                },
                new ArchitectureVersionedContractSurfaceIsolationSurface
                {
                    Id = "v2-contracts",
                    TypesMatching = new ArchitecturePublicApiSurfaceSelector { Namespace = typeof(V2.Customer).Namespace! },
                },
                new ArchitectureVersionedContractSurfaceIsolationSurface
                {
                    Id = "transport-implementation",
                    TypesMatching = new ArchitecturePublicApiSurfaceSelector { Namespace = typeof(Transport.Customer).Namespace! },
                },
                ],
                SourceSurface = "v1-contracts",
                ForbiddenSurfaces = ["v2-contracts", "transport-implementation"],
            };

        private static ContractSurfaceExposurePayload AssertPayload(ArchitectureViolation violation)
        {
            Assert.That(violation.Payload, Is.TypeOf<ContractSurfaceExposurePayload>());
            return (ContractSurfaceExposurePayload)violation.Payload!;
        }
    }

}

namespace ArchLinterNet.Core.Tests.VersionedIsolation.V1
{
    public sealed class Contract
    {
        public static V2.Customer Direct => new();

        public static Envelope<V2.Customer> Nested => new();

        public static Transport.Customer TransportCustomer => new();
    }

    public sealed class Envelope<T>;
}

namespace ArchLinterNet.Core.Tests.VersionedIsolation.V2
{
    public sealed class Customer;
}

namespace ArchLinterNet.Core.Tests.VersionedIsolation.Transport
{
    public sealed class Customer;
}
