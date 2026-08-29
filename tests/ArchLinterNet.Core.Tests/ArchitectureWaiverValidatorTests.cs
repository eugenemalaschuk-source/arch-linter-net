using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureWaiverValidatorTests
{
    private readonly List<string> _policyPaths = [];

    [TearDown]
    public void TearDown()
    {
        foreach (string path in _policyPaths)
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Load_VersionOneLegacyIgnore_PreservesCompatibilityProfile()
    {
        ArchitectureContractDocument document = CreateDocument(version: 1, CreateLegacyIgnore());

        Assert.That(ArchitectureWaiverProfile.Resolve(document), Is.EqualTo(ArchitectureWaiverProfile.Compatibility));
    }

    [Test]
    public void Load_StrictProfileLegacyIgnore_RetainsFailClosedInvalidEvidence()
    {
        var loader = new ArchitecturePolicyDocumentLoader();
        string path = CreatePolicyFile("""
            version: 2
            name: Strict waiver policy
            analysis:
              target_assemblies: []
            contracts:
              strict:
                - id: boundary
                  name: boundary
                  source: app
                  forbidden: [infrastructure]
                  ignored_violations:
                    - source_type: App.Legacy
                      forbidden_reference: Infrastructure.Db
                      reason: Legacy extraction
            """);

        ArchitectureContractDocument document = loader.Load(path);
        ArchitectureIgnoredViolation waiver = document.Contracts.Strict.Single().IgnoredViolations.Single();
        ArchitectureWaiverLifecycleRecord record = ArchitectureWaiverLifecycleEvaluator.Evaluate(
            document, "strict", [], new DateOnly(2026, 8, 28)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(waiver.WaiverValidationError, Does.Contain("Strict waiver profile"));
            Assert.That(record.State, Is.EqualTo("invalid"));
        });
    }

    [Test]
    public void Load_CompleteStructuredWaiver_AcceptsCanonicalMetadata()
    {
        var loader = new ArchitecturePolicyDocumentLoader();
        string fingerprint = "sha256:" + new string('a', 64);
        string path = CreatePolicyFile($"""
            version: 2
            name: Strict waiver policy
            layers:
              app:
                namespace: App
            analysis:
              target_assemblies: []
            contracts:
              strict:
                - id: boundary
                  name: boundary
                  source: app
                  forbidden: [infrastructure]
                  ignored_violations:
                    - id: ARCH-IGN-001
                      source_type: App.Legacy
                      forbidden_reference: Infrastructure.Db
                      target:
                        fingerprint: {fingerprint}
                      reason: Legacy extraction
                      owner: architecture-team
                      issue: ARCH-231
                      introduced: 2026-08-12
                      expires: 2026-10-01
            """);

        ArchitectureContractDocument document = loader.Load(path);

        Assert.Multiple(() =>
        {
            Assert.That(ArchitectureWaiverProfile.Resolve(document), Is.EqualTo(ArchitectureWaiverProfile.Strict));
            Assert.That(document.Contracts.Strict.Single().IgnoredViolations.Single().WaiverId, Is.EqualTo("ARCH-IGN-001"));
        });
    }

    [Test]
    public void TargetFingerprint_ChangesForDistinctOccurrence()
    {
        ArchitectureViolationIdentity first = new(2, "strict", "dependency", "boundary", null, "App.Service", null,
            null, null, "Infrastructure.Db", 0);
        ArchitectureViolationIdentity second = first with { Occurrence = 1 };

        Assert.That(ArchitectureWaiverTargetFingerprint.Create(first), Is.Not.EqualTo(ArchitectureWaiverTargetFingerprint.Create(second)));
    }

    [Test]
    public void TargetFingerprint_RejectsUppercaseHexadecimal()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ArchitectureWaiverTargetFingerprint.IsSupported("sha256:" + new string('a', 64)), Is.True);
            Assert.That(ArchitectureWaiverTargetFingerprint.IsSupported("sha256:" + new string('A', 64)), Is.False);
        });
    }

    [Test]
    public void Load_DuplicateStructuredWaiverIds_FailsClosed()
    {
        var loader = new ArchitecturePolicyDocumentLoader();
        string fingerprint = "sha256:" + new string('a', 64);
        string path = CreatePolicyFile($"""
            version: 2
            name: Duplicate waivers
            analysis:
              target_assemblies: []
            contracts:
              strict:
                - id: first
                  name: first
                  source: app
                  forbidden: [infrastructure]
                  ignored_violations:
                    - id: ARCH-IGN-001
                      source_type: App.First
                      forbidden_reference: Infrastructure.Db
                      target:
                        fingerprint: {fingerprint}
                      reason: Temporary migration
                      owner: architecture-team
                      issue: ARCH-231
                      introduced: 2026-08-01
                      expires: 2026-10-01
                - id: second
                  name: second
                  source: app
                  forbidden: [infrastructure]
                  ignored_violations:
                    - id: ARCH-IGN-001
                      source_type: App.Second
                      forbidden_reference: Infrastructure.Db
                      target:
                        fingerprint: {fingerprint}
                      reason: Temporary migration
                      owner: architecture-team
                      issue: ARCH-231
                      introduced: 2026-08-01
                      expires: 2026-10-01
            """);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => loader.Load(path))!;

        Assert.That(exception.Message, Does.Contain("Duplicate structured waiver id 'ARCH-IGN-001'"));
    }

    [Test]
    public void Load_StructuredWaiverWithInvalidMetadata_RetainsInvalidLifecycleEvidence()
    {
        var loader = new ArchitecturePolicyDocumentLoader();
        string path = CreatePolicyFile("""
            version: 2
            name: Invalid waiver dates
            analysis:
              target_assemblies: []
            contracts:
              strict:
                - id: boundary
                  name: boundary
                  source: app
                  forbidden: [infrastructure]
                  ignored_violations:
                    - id: ARCH-IGN-001
                      source_type: App.Legacy
                      forbidden_reference: Infrastructure.Db
                      target:
                        fingerprint: sha256:not-a-fingerprint
                      reason: Temporary migration
                      owner: architecture-team
                      issue: ARCH-231
                      introduced: 2026-08-01
                      expires: 2026-07-01
            """);

        ArchitectureContractDocument document = loader.Load(path);
        ArchitectureIgnoredViolation waiver = document.Contracts.Strict.Single().IgnoredViolations.Single();
        ArchitectureWaiverLifecycleRecord record = ArchitectureWaiverLifecycleEvaluator.Evaluate(
            document, "strict", [], new DateOnly(2026, 8, 2)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(waiver.WaiverValidationError, Does.Contain("canonical lowercase"));
            Assert.That(record.State, Is.EqualTo("invalid"));
        });
    }

    [Test]
    public void Load_SourceSetExpandedStructuredWaiver_ValidatesAuthoredDeclarationOnce()
    {
        var loader = new ArchitecturePolicyDocumentLoader();
        string fingerprint = "sha256:" + new string('a', 64);
        string path = CreatePolicyFile($"""
            version: 2
            name: Source-expanded waiver
            layers:
              application:
                namespace: App
              domain:
                namespace: Domain
            source_sets:
              inner_layers:
                kind: layer
                members: [application, domain]
            external_dependencies:
              vendor:
                namespace_prefixes: [Vendor]
            analysis:
              target_assemblies: []
            contracts:
              strict_external:
                - id: no-vendor
                  name: no vendor
                  source_sets: [inner_layers]
                  forbidden: [vendor]
                  ignored_violations:
                    - id: ARCH-IGN-001
                      source_type: App.Legacy
                      forbidden_reference: Vendor.Client
                      target:
                        fingerprint: {fingerprint}
                      reason: Temporary migration
                      owner: architecture-team
                      issue: ARCH-231
                      introduced: 2026-08-01
                      expires: 2026-10-01
            """);

        ArchitectureContractDocument document = loader.Load(path);
        ArchitectureIgnoredViolation[] aliases = document.Contracts.StrictExternal
            .Select(contract => contract.IgnoredViolations.Single())
            .ToArray();
        ArchitectureExternalDependencyContract unmatchedAlias = document.Contracts.StrictExternal
            .Single(contract => contract.Source == "application");
        var unmatched = new ArchitectureUnmatchedIgnoredViolation(
            unmatchedAlias.Name,
            unmatchedAlias.Id,
            0,
            aliases[0].SourceType,
            aliases[0].ForbiddenReference,
            aliases[0].Reason)
        {
            ContractGroup = "strict_external",
        };
        ArchitectureWaiverLifecycleRecord record = ArchitectureWaiverLifecycleEvaluator.Evaluate(
            document, "strict", [unmatched], new DateOnly(2026, 8, 28)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(aliases, Has.Length.EqualTo(2));
            Assert.That(ReferenceEquals(aliases[0], aliases[1]), Is.True);
            Assert.That(aliases[0].WaiverValidationError, Is.Null);
            Assert.That(record.State, Is.EqualTo("active"));
            Assert.That(record.MatchesGovernedFinding, Is.True);
        });
    }

    private static ArchitectureContractDocument CreateDocument(int version, ArchitectureIgnoredViolation ignore) => new()
    {
        Version = version,
        Name = "Test",
        Analysis = new ArchitectureAnalysisConfiguration { TargetAssemblies = new List<string>() },
        Contracts = new ArchitectureContractGroups
        {
            Strict = new List<ArchitectureDependencyContract>
            {
                new()
                {
                    Id = "boundary",
                    Name = "boundary",
                    Source = "app",
                    Forbidden = new List<string> { "infrastructure" },
                    IgnoredViolations = new List<ArchitectureIgnoredViolation> { ignore }
                }
            }
        }
    };

    private static ArchitectureIgnoredViolation CreateLegacyIgnore() => new()
    {
        SourceType = "App.Legacy",
        ForbiddenReference = "Infrastructure.Db",
        Reason = "Legacy extraction"
    };

    private string CreatePolicyFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"arch-linter-waiver-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, content);
        _policyPaths.Add(path);
        return path;
    }
}
