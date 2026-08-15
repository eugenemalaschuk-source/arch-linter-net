using ArchLinterNet.Core.Caching;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #365's acceptance scenario, proven at the layer this repository's environment can actually
// exercise today: "a hit reproduces byte-identical findings/ordering/exit-category to an uncached
// run" (review finding #1). A full same-repo two-CLI-run wall-clock demonstration is not possible
// yet — EvaluatedBuildInputManifestCollector always reports CacheIneligible for real MSBuild
// evidence (see AnalysisCachePopulationTests.TryPopulate_RealProject_IsIneligibleBuildInputToday,
// unchanged, pre-existing, and #406's own explicitly scoped future work) — so
// AnalysisCacheStore.Put can never actually persist an entry for a genuinely discovered project in
// this repository's current environment, and consequently ArchitectureAnalysisSnapshot's cache-hit
// short-circuit (TryEvaluateFromCache) cannot yet be observed producing a live Hit through the
// full CLI/Testing pipeline either. This test instead proves the exact mechanism that seam relies
// on — Put a real ValidationOutcome-shaped AnalysisCacheOutcomeV1 (with a real, non-trivial,
// polymorphic-payload-bearing violation), TryGet it back, and reconstruct a ValidationOutcome via
// AnalysisCacheOutcomeMapper — is faithful: the reconstructed outcome's Violations (including
// Payload/Identity), Cycles, UnmatchedIgnoredViolations, PolicyConsistencyFindings,
// ClassificationConflicts/MetadataFailures, ordering, and Passed (which determines exit category)
// are exactly what was cached, byte for byte.
[TestFixture]
public sealed class AnalysisCacheHitReconstructionTests
{
    private static readonly string[] _value = { "MyApp.Infrastructure.Db.OrderRepository" };
    private static readonly string[] _value1 = { "MyApp.Application" };
    private static readonly string[] _value2 = { "MyApp.Infrastructure.Db.CustomerRepository" };
    private static readonly string[] _value3 = { "A -> B -> A" };
    private static readonly string[] _value4 = { "R001" };
    private static readonly string[] _value5 = { "no_infra_from_domain" };
    private static readonly string[] _value6 = { "Domain" };
    private static ArchitectureViolation[] BuildOriginalViolationsInDeterministicOrder()
    {
        return new[]
        {
            new ArchitectureViolation(
                "no_infra_from_domain", "R001", "MyApp.Domain.Order", "MyApp.Infrastructure",
                _value)
            {
                Payload = new DependencyPayload("Domain", "Infrastructure", _value1),
                Identity = new ArchitectureViolationIdentity(
                    ArchitectureViolationIdentity.CurrentVersion, "layers", "dependency", "R001",
                    "MyApp.Domain", "MyApp.Domain.Order", null, "MyApp.Infrastructure",
                    "MyApp.Infrastructure.Db.OrderRepository", null, 0),
            },
            new ArchitectureViolation(
                "no_infra_from_domain", "R001", "MyApp.Domain.Customer", "MyApp.Infrastructure",
                _value2)
            {
                Payload = new DependencyPayload("Domain", "Infrastructure", null),
                Identity = new ArchitectureViolationIdentity(
                    ArchitectureViolationIdentity.CurrentVersion, "layers", "dependency", "R001",
                    "MyApp.Domain", "MyApp.Domain.Customer", null, "MyApp.Infrastructure",
                    "MyApp.Infrastructure.Db.CustomerRepository", null, 0),
            },
        };
    }

    private static ValidationOutcome BuildOriginalOutcome()
    {
        ArchitectureViolation[] violations = BuildOriginalViolationsInDeterministicOrder();
        return new ValidationOutcome(
            Passed: false,
            Violations: violations,
            Cycles: _value3,
            CoverageFindings: Array.Empty<ArchitectureViolation>(),
            CoverageConfig: "off",
            UnmatchedIgnoredViolations: new[]
            {
                new ArchitectureUnmatchedIgnoredViolation(
                    "no_infra_from_domain", "R001", 0, "MyApp.Domain.Legacy", "MyApp.Infrastructure.Old", "no-longer-matched"),
            },
            UnmatchedIgnoredViolationsConfig: "warn",
            PolicyConsistencyFindings: new[]
            {
                new PolicyConsistencyDiagnostic(
                    "no_infra_from_domain", "R001", "duplicate-contract-id", "duplicate id detected",
                    _value4, _value5, _value6),
            },
            PolicyConsistencyConfig: "warn",
            CoverageSummaries: new[]
            {
                new ArchitectureCoverageSummary(
                    "no_infra_from_domain", "R001", "namespace",
                    new ArchitectureCoverageSummaryCounts(Covered: 1, Excluded: 0, Uncovered: 1, Stale: 0, Unknown: 0),
                    ExcludedItems: Array.Empty<ArchitectureCoverageSummaryExcludedItem>(),
                    UncoveredItems: new[] { new ArchitectureCoverageSummaryEvidenceItem("MyApp.Domain.Order", "no matching rule") },
                    StaleItems: Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
                    UnknownItems: Array.Empty<ArchitectureCoverageSummaryEvidenceItem>(),
                    CoveredItems: new[] { new ArchitectureCoverageSummaryEvidenceItem("MyApp.Domain.Customer", "layers.domain") }),
            },
            ClassificationConflicts: new[]
            {
                new ArchitectureClassificationConflict("MyApp.Domain.Order", ArchitectureClassificationSource.TypeAttribute, "Domain", "Infrastructure", "role: Domain vs Infrastructure"),
            },
            ClassificationMetadataFailures: new[]
            {
                new ArchitectureClassificationMetadataFailure("MyApp.Domain.Order", ArchitectureClassificationSource.TypeAttribute, "team", "missing-value"),
            })
        {
            // Finding #6: these five fields were previously omitted from the cache envelope
            // entirely — non-trivial data here so the canonical-JSON round-trip equality assertion
            // below actually exercises them, not merely their empty/default shape.
            ClassificationRoles = new[]
            {
                new ArchitectureClassificationRoleFact(
                    "MyApp.Domain.Order", "Domain", ArchitectureClassificationSource.TypeAttribute, "MyApp.Domain.DomainAttribute",
                    new Dictionary<string, object> { ["team"] = "payments", ["stable"] = true, ["weight"] = 1.5m }),
            },
            ClassificationPathDeferred = new ArchitectureClassificationPathDeferredNotice(2),
            CycleFindings = new[]
            {
                new ArchitectureCycleFinding("no_cycles", "R010", "MyApp.A -> MyApp.B -> MyApp.A"),
            },
            SubtractiveMatcherParticipation = new[]
            {
                new ArchitectureSubtractiveMatcherParticipation(
                    "no_infra_from_domain", "no_infra_from_domain", "exclude_types_matching", 0, Matched: true),
            },
        };
    }

    [Test]
    public void PutThenTryGetThenReconstruct_ProducesByteIdenticalFindingsOrderingAndExitCategory()
    {
        string root = Path.Combine(Path.GetTempPath(), "arch-linter-net-cache-hit-reconstruction-tests", Guid.NewGuid().ToString("N"));
        AnalysisCacheLocation location = new(root, AnalysisCacheMode.ExplicitPath);
        try
        {
            ValidationOutcome original = BuildOriginalOutcome();
            AnalysisCacheOutcomeV1 cacheOutcome = AnalysisCacheOutcomeMapper.ToCacheOutcome(original);

            AnalysisCacheKey key = new("policy-digest", "strict", null, "contracts-digest", "workspace-digest", null, null, null, null);
            AnalysisCacheProjectManifest[] manifests =
            {
                new("src/Domain/Domain.csproj", "manifest-digest", ArchLinterNet.Core.BuildState.CacheEligibility.VerifiedCacheEligible),
            };

            AnalysisCacheStore.PutResult putResult = AnalysisCacheStore.Put(location, key, manifests, cacheOutcome);
            Assert.That(putResult.RejectReason, Is.Null);

            AnalysisCacheLookupResult lookup = AnalysisCacheStore.TryGet(location, key, manifests);
            Assert.That(lookup.Outcome, Is.EqualTo(AnalysisCacheLookupOutcome.Hit));

            ValidationOutcome reconstructed = AnalysisCacheOutcomeMapper.FromCacheOutcome(
                lookup.Entry!.Outcome, "/repo", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                ArchitectureSourceExpansionInventory.Empty);

            // Exit category: Passed determines strict/audit success vs failure exit code.
            Assert.That(reconstructed.Passed, Is.EqualTo(original.Passed));

            // Findings + ordering + identity: every violation, in the same order, with the same
            // closed-set Payload and the same baseline Identity.
            Assert.That(reconstructed.Violations.Count, Is.EqualTo(original.Violations.Count));
            ArchitectureViolation[] originalList = original.Violations.ToArray();
            ArchitectureViolation[] reconstructedList = reconstructed.Violations.ToArray();
            for (int i = 0; i < originalList.Length; i++)
            {
                Assert.That(reconstructedList[i].ContractName, Is.EqualTo(originalList[i].ContractName), $"violation[{i}].ContractName");
                Assert.That(reconstructedList[i].SourceType, Is.EqualTo(originalList[i].SourceType), $"violation[{i}].SourceType");
                Assert.That(reconstructedList[i].ForbiddenReferences, Is.EqualTo(originalList[i].ForbiddenReferences), $"violation[{i}].ForbiddenReferences");
                Assert.That(reconstructedList[i].Identity, Is.EqualTo(originalList[i].Identity), $"violation[{i}].Identity");
                Assert.That(reconstructedList[i].Payload, Is.InstanceOf<DependencyPayload>(), $"violation[{i}].Payload type");
                DependencyPayload originalPayload = (DependencyPayload)originalList[i].Payload!;
                DependencyPayload reconstructedPayload = (DependencyPayload)reconstructedList[i].Payload!;
                Assert.That(reconstructedPayload.SourceLayer, Is.EqualTo(originalPayload.SourceLayer), $"violation[{i}].Payload.SourceLayer");
                Assert.That(reconstructedPayload.TargetLayer, Is.EqualTo(originalPayload.TargetLayer), $"violation[{i}].Payload.TargetLayer");
                Assert.That(
                    reconstructedPayload.AllowedImporters ?? Array.Empty<string>(),
                    Is.EqualTo(originalPayload.AllowedImporters ?? Array.Empty<string>()),
                    $"violation[{i}].Payload.AllowedImporters");
            }

            Assert.That(reconstructed.Cycles, Is.EqualTo(original.Cycles));

            // Finding #6: the five previously-omitted result-bearing fields, explicitly.
            Assert.That(reconstructed.CycleFindings.Count, Is.EqualTo(1));
            Assert.That(reconstructed.CycleFindings.Single().Path, Is.EqualTo("MyApp.A -> MyApp.B -> MyApp.A"));
            Assert.That(reconstructed.ClassificationRoles.Count, Is.EqualTo(1));
            ArchitectureClassificationRoleFact role = reconstructed.ClassificationRoles.Single();
            Assert.That(role.Subject, Is.EqualTo("MyApp.Domain.Order"));
            Assert.That(role.Role, Is.EqualTo("Domain"));
            Assert.That(role.Metadata["team"], Is.EqualTo("payments"));
            Assert.That(role.Metadata["stable"], Is.EqualTo(true));
            Assert.That(role.Metadata["weight"], Is.EqualTo(1.5m));
            Assert.That(reconstructed.ClassificationPathDeferred, Is.Not.Null);
            Assert.That(reconstructed.ClassificationPathDeferred!.DeclaredEntryCount, Is.EqualTo(2));
            Assert.That(reconstructed.CoverageSummaries.Count, Is.EqualTo(1));
            Assert.That(reconstructed.CoverageSummaries.Single().Counts.Covered, Is.EqualTo(1));
            Assert.That(reconstructed.SubtractiveMatcherParticipation.Count, Is.EqualTo(1));
            Assert.That(reconstructed.SubtractiveMatcherParticipation.Single().Field, Is.EqualTo("exclude_types_matching"));

            // Byte-identical, using the product's own definition of identity: re-mapping the
            // reconstructed outcome back to AnalysisCacheOutcomeV1 and comparing its canonical JSON
            // (the same bytes AnalysisCacheContentDigest hashes) against the originally cached
            // outcome's canonical JSON must be exactly equal.
            string originalCanonicalJson = System.Text.Json.JsonSerializer.Serialize(cacheOutcome, AnalysisCacheJson.Options);
            string reconstructedCanonicalJson = System.Text.Json.JsonSerializer.Serialize(
                AnalysisCacheOutcomeMapper.ToCacheOutcome(reconstructed), AnalysisCacheJson.Options);
            Assert.That(reconstructedCanonicalJson, Is.EqualTo(originalCanonicalJson));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
