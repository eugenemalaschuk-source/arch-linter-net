using ArchLinterNet.Core.Model;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

// Issue #685: policy inventory is repository-scoped evidence, so a shared strict/audit snapshot
// must retain every mode's manual waiver even though each mode's public Waivers collection remains
// local to the mode that was requested.
[TestFixture]
public sealed class ArchitecturePolicyInventoryRepositoryScopeTests
{
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(), $"arch-linter-policy-inventory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public void SharedSnapshot_StrictAndAuditKeepRepositoryWaiverInventoryWhileWaiversStayModeLocal()
    {
        string policyPath = Path.Combine(_tempDirectory, "dependencies.arch.yml");
        File.WriteAllText(policyPath, """
            version: 1
            name: Repository waiver inventory

            layers:
              core:
                namespace: ArchLinterNet.Core

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict:
                - id: strict-boundary
                  name: strict boundary
                  source: core
                  forbidden: [core]
              audit:
                - id: audit-boundary
                  name: audit boundary
                  source: core
                  forbidden: [core]
            """);

        ArchitectureValidationResult strictWithoutWaiver;
        ArchitectureValidationResult auditWithoutWaiver;
        using (ArchitectureValidationSnapshotSession baseline = new ArchitectureValidationBuilder(policyPath)
            .WithWaiverEvaluationDate(new DateOnly(2026, 8, 31))
            .CreateSnapshot())
        {
            strictWithoutWaiver = baseline.ValidateStrict();
            auditWithoutWaiver = baseline.ValidateAudit();
        }

        ArchitectureViolation strictViolation = strictWithoutWaiver.Violations
            .First(violation => violation.Identity is not null);
        ArchitectureViolation auditViolation = auditWithoutWaiver.Violations
            .First(violation => violation.Identity is not null);
        Assert.That(strictViolation.Identity, Is.Not.Null);
        Assert.That(auditViolation.Identity, Is.Not.Null);

        File.WriteAllText(policyPath, $"""
            version: 2
            name: Repository waiver inventory

            layers:
              core:
                namespace: ArchLinterNet.Core

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict:
                - id: strict-boundary
                  name: strict boundary
                  source: core
                  forbidden: [core]
                  ignored_violations:
                    - id: ARCH-IGN-STRICT
                      source_type: '{strictViolation.SourceType}'
                      forbidden_reference: '{strictViolation.ForbiddenReferences.First()}'
                      target:
                        fingerprint: {ArchitectureWaiverTargetFingerprint.Create(strictViolation.Identity!)}
                      reason: Strict migration
                      owner: architecture-team
                      issue: ARCH-685
                      introduced: 2026-08-01
                      expires: 2026-10-01
              audit:
                - id: audit-boundary
                  name: audit boundary
                  source: core
                  forbidden: [core]
                  ignored_violations:
                    - id: ARCH-IGN-AUDIT
                      source_type: '{auditViolation.SourceType}'
                      forbidden_reference: '{auditViolation.ForbiddenReferences.First()}'
                      target:
                        fingerprint: {ArchitectureWaiverTargetFingerprint.Create(auditViolation.Identity!)}
                      reason: Audit migration
                      owner: architecture-team
                      issue: ARCH-685
                      introduced: 2026-08-01
                      expires: 2026-10-01
            """);

        using ArchitectureValidationSnapshotSession session = new ArchitectureValidationBuilder(policyPath)
            .WithWaiverEvaluationDate(new DateOnly(2026, 8, 31))
            .CreateSnapshot();

        ArchitectureValidationResult strict = session.ValidateStrict();

        Assert.Multiple(() =>
        {
            Assert.That(strict.Waivers.Select(waiver => waiver.Id), Is.EqualTo(["ARCH-IGN-STRICT"]));
            Assert.That(strict.PolicyInventory, Is.Not.Null);
            Assert.That(strict.PolicyInventory!.IgnoreDebt.Total, Is.EqualTo(2));
            Assert.That(strict.PolicyInventory.Waivers.Select(waiver => waiver.Id),
                Is.EquivalentTo(["ARCH-IGN-AUDIT", "ARCH-IGN-STRICT"]));
            Assert.That(session.Counters.ModesEvaluated, Is.EqualTo(2));
        });

        ArchitectureValidationResult audit = session.ValidateAudit();

        Assert.Multiple(() =>
        {
            Assert.That(audit.Waivers.Select(waiver => waiver.Id), Is.EqualTo(["ARCH-IGN-AUDIT"]));
            Assert.That(strict.Waivers.Single().State, Is.EqualTo("active"));
            Assert.That(audit.Waivers.Single().State, Is.EqualTo("active"));
            Assert.That(audit.PolicyInventory, Is.EqualTo(strict.PolicyInventory));
            Assert.That(audit.PolicyInventory!.IgnoreDebt.Total, Is.EqualTo(2));
            Assert.That(audit.PolicyInventory.Waivers.Select(waiver => waiver.Id),
                Is.EquivalentTo(["ARCH-IGN-AUDIT", "ARCH-IGN-STRICT"]));
            Assert.That(session.Counters.ModesEvaluated, Is.EqualTo(2));
        });
    }
}
