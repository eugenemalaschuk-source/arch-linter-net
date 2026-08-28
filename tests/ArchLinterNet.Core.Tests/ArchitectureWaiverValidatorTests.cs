using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Model;
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
    public void Load_StrictProfileRejectsLegacyIgnore()
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

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => loader.Load(path))!;

        Assert.That(exception.Message, Does.Contain("Strict waiver profile"));
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
    public void Load_StructuredWaiverWithInvalidExpiry_FailsClosed()
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

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => loader.Load(path))!;

        Assert.That(exception.Message, Does.Contain("requires id, target.fingerprint"));
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
