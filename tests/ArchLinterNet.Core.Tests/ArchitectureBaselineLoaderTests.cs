using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.IO;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureBaselineLoaderTests
{
    private readonly ArchitectureBaselineLoadingService _service = new(ArchitectureFileSystem.Real);

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Test]
    public void LoadFromPath_MissingFile_ThrowsFileNotFound()
    {
        string missingPath = Path.Combine(_tempDir, "nonexistent.yml");
        Assert.Throws<FileNotFoundException>(() =>
            _service.LoadFromPath(missingPath));
    }

    [Test]
    public void LoadFromPath_InvalidVersion_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 999
baseline:
  strict: []
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("version"));
    }

    [Test]
    public void LoadFromPath_EmptyId_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 1
baseline:
  strict:
    - id: ''
      ignored_violations:
        - source_type: Some.Type
          forbidden_reference: Bad.Type
          reason: test
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("id"));
    }

    [Test]
    public void LoadFromPath_EmptySourceType_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 1
baseline:
  strict:
    - id: my-rule
      ignored_violations:
        - source_type: ''
          forbidden_reference: Bad.Type
          reason: test
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("source_type"));
    }

    [Test]
    public void LoadFromPath_EmptyForbiddenReference_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 1
baseline:
  strict:
    - id: my-rule
      ignored_violations:
        - source_type: Some.Type
          forbidden_reference: ''
          reason: test
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("forbidden_reference"));
    }

    [Test]
    public void LoadFromPath_Version2LegacyShapedEntry_Throws()
    {
        // A 'version: 2' document whose entries don't actually carry structured identity must be
        // rejected — otherwise it silently loads with defaulted/empty identity fields and behaves
        // differently in `validate` (glob fallback never triggers, since IdentityVersion would be
        // null) than in `diff`/`verify` (which would build a garbage all-empty identity). Fail
        // closed instead.
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 2
baseline:
  strict:
    - id: my-rule
      ignored_violations:
        - source_type: Some.Type
          forbidden_reference: Bad.Type
          reason: legacy-shaped-mislabeled-as-v2
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("identity_version"));
    }

    [Test]
    public void LoadFromPath_Version2MissingContractFamily_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 2
baseline:
  strict:
    - id: my-rule
      ignored_violations:
        - source_type: Some.Type
          forbidden_reference: Bad.Type
          reason: test
          identity_version: 2
          kind: dependency
          occurrence: 0
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("contract_family"));
    }

    [Test]
    public void LoadFromPath_Version2MissingOccurrence_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 2
baseline:
  strict:
    - id: my-rule
      ignored_violations:
        - source_type: Some.Type
          forbidden_reference: Bad.Type
          reason: test
          identity_version: 2
          contract_family: strict
          kind: dependency
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("occurrence"));
    }

    [Test]
    public void LoadFromPath_Version1EntryWithIdentityVersion_Throws()
    {
        // A 'version: 1' document must not silently accept structured-identity fields — those are
        // only meaningful (and were only ever written) in a 'version: 2' document.
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 1
baseline:
  strict:
    - id: my-rule
      ignored_violations:
        - source_type: Some.Type
          forbidden_reference: Bad.Type
          reason: test
          identity_version: 2
");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("identity_version"));
    }

    [Test]
    public void LoadFromPath_WellFormedVersion2Document_LoadsSuccessfully()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 2
baseline:
  strict:
    - id: my-rule
      ignored_violations:
        - source_type: Host.A.Program
          forbidden_reference: System.Object
          reason: known debt
          identity_version: 2
          contract_family: strict
          kind: dependency
          source_assembly: Host.A
          target_assembly: mscorlib
          target_member: System.Object
          occurrence: 0
");
        ArchitectureBaselineDocument document = _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml"));

        Assert.That(document.Version, Is.EqualTo(2));
        Assert.That(document.Baseline.Strict[0].IgnoredViolations[0].SourceAssembly, Is.EqualTo("Host.A"));
    }

    [Test]
    public void LoadFromPath_WellFormedVersion3Document_LoadsMetricBaselinesAndStructuredFindings()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 3
baseline:
  strict:
    - id: my-rule
      ignored_violations:
        - source_type: Host.A.Program
          forbidden_reference: System.Object
          reason: known debt
          identity_version: 2
          contract_family: strict
          kind: dependency
          source_assembly: Host.A
          target_assembly: mscorlib
          target_member: System.Object
          occurrence: 0
metric_baselines:
  - metric_identity_version: 1
    metric_id: app-outgoing
    metric_kind: outgoing_component_count
    native_subject: application
    effective_scope: application
    value: 3
");

        ArchitectureBaselineDocument document = _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml"));

        Assert.Multiple(() =>
        {
            Assert.That(document.Version, Is.EqualTo(3));
            Assert.That(document.Baseline.Strict[0].IgnoredViolations, Has.Count.EqualTo(1));
            Assert.That(document.MetricBaselines, Has.Count.EqualTo(1));
            Assert.That(document.MetricBaselines[0].Identity, Is.EqualTo(new ArchitectureMetricBaselineIdentity(
                1, "app-outgoing", "outgoing_component_count", "application", null, "application")));
            Assert.That(document.MetricBaselines[0].Value, Is.EqualTo(3));
        });
    }

    [Test]
    public void LoadFromPath_Version3DuplicateMetricIds_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 3
baseline: {}
metric_baselines:
  - metric_identity_version: 1
    metric_id: app-outgoing
    metric_kind: outgoing_component_count
    native_subject: application
    effective_scope: application
    value: 3
  - metric_identity_version: 1
    metric_id: app-outgoing
    metric_kind: outgoing_component_count
    native_subject: application
    effective_scope: application
    value: 4
");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("Duplicate metric baseline id"));
    }

    [Test]
    public void LoadFromPath_Version3MetricBaselinesRequireVersion3()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 2
baseline: {}
metric_baselines:
  - metric_identity_version: 1
    metric_id: app-outgoing
    metric_kind: outgoing_component_count
    native_subject: application
    effective_scope: application
    value: 3
");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("only valid in a 'version: 3'"));
    }

    [TestCase("metric_identity_version: 2", "metric_identity_version")]
    [TestCase("metric_id: ''", "metric_id")]
    [TestCase("metric_kind: ''", "metric_kind")]
    [TestCase("native_subject: ''", "native_subject")]
    [TestCase("effective_scope: ''", "effective_scope")]
    [TestCase("value: -1", "value")]
    public void LoadFromPath_Version3MalformedMetricEntry_Throws(string replacement, string diagnostic)
    {
        string metric = $"""
  - metric_identity_version: 1
    metric_id: app-outgoing
    metric_kind: outgoing_component_count
    native_subject: application
    effective_scope: application
    value: 3
""";
        metric = metric.Replace(
            replacement.StartsWith("metric_identity_version", StringComparison.Ordinal)
                ? "metric_identity_version: 1"
                : replacement.StartsWith("metric_id", StringComparison.Ordinal)
                    ? "metric_id: app-outgoing"
                    : replacement.StartsWith("metric_kind", StringComparison.Ordinal)
                        ? "metric_kind: outgoing_component_count"
                        : replacement.StartsWith("native_subject", StringComparison.Ordinal)
                            ? "native_subject: application"
                            : replacement.StartsWith("effective_scope", StringComparison.Ordinal)
                                ? "effective_scope: application"
                                : "value: 3",
            replacement,
            StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), $"version: 3\nbaseline: {{}}\nmetric_baselines:\n{metric}");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain(diagnostic));
    }

    [TestCase("    metric_identity_version: 1\n", "", "metric_identity_version")]
    [TestCase("    value: 3", "", "value")]
    [TestCase("    value: 3", "    valu: 3", "valu")]
    [TestCase("    value: 3", "    value: 3\n    unknown: true", "unknown")]
    public void LoadFromPath_Version3MetricEntryWithMissingOrMisspelledRequiredField_Throws(
        string original,
        string replacement,
        string diagnostic)
    {
        string metric = """
  -
    metric_identity_version: 1
    metric_id: app-outgoing
    metric_kind: outgoing_component_count
    native_subject: application
    effective_scope: application
    value: 3
""".Replace(original, replacement, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), $"version: 3\nbaseline: {{}}\nmetric_baselines:\n{metric}");

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(exception!.Message, Does.Contain(diagnostic));
    }

    [Test]
    public void LoadFromPath_Version3FindingEntryWithoutStructuredIdentity_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "baseline.yml"), @"
version: 3
baseline:
  strict:
    - id: my-rule
      ignored_violations:
        - source_type: Some.Type
          forbidden_reference: Bad.Type
          reason: legacy-shaped-mislabeled-as-v3
metric_baselines: []
");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            _service.LoadFromPath(Path.Combine(_tempDir, "baseline.yml")));
        Assert.That(ex!.Message, Does.Contain("identity_version"));
    }

    [Test]
    public void MergeAndValidate_ProjectMetadataGroup_AppliesIgnoredViolations()
    {
        ArchitectureContractDocument policy = new()
        {
            Version = 1,
            Name = "Test",
            Contracts = new ArchitectureContractGroups
            {
                StrictProjectMetadata = new List<ArchitectureProjectMetadataContract>
                {
                    new()
                    {
                        Name = "project-metadata",
                        Id = "project-metadata",
                        Projects = new List<string> { "src/MyApp/MyApp.csproj" },
                        RequiredProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Nullable"] = "enable"
                        }
                    }
                }
            }
        };

        ArchitectureBaselineDocument baseline = new()
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                StrictProjectMetadata = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "project-metadata",
                        IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "src/MyApp/MyApp.csproj",
                                ForbiddenReference = "friend_assembly:MyApp.Tools",
                                Reason = "known debt"
                            }
                        }
                    }
                }
            }
        };

        ArchitectureBaselineLoadingService.MergeAndValidate(policy, baseline);

        Assert.That(policy.Contracts.StrictProjectMetadata[0].IgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(policy.Contracts.StrictProjectMetadata[0].IgnoredViolations[0].ForbiddenReference,
            Is.EqualTo("friend_assembly:MyApp.Tools"));
    }
}
