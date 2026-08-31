using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.IO;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureBaselineRoundTripTests
{
    private static readonly ArchitectureBaselineGenerator _generator = new();
    private static readonly ArchitectureBaselineLoadingService _loadingService = new();

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
    public void RoundTrip_SerializedThenDeserialized_MaintainsStructure()
    {
        var doc = new ArchitectureBaselineDocument
        {
            Version = 1,
            Baseline = new ArchitectureBaselineContractGroups
            {
                Strict = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "dep-rule",
                        IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "Old.Library.Service",
                                ForbiddenReference = "Deprecated.Class",
                                Reason = "migration in progress"
                            }
                        }
                    }
                },
                StrictCycles = new List<ArchitectureBaselineContractEntry>
                {
                    new()
                    {
                        Id = "no-cycles",
                        IgnoredViolations = new List<ArchitectureBaselineIgnoredViolation>
                        {
                            new()
                            {
                                SourceType = "A.B.C",
                                ForbiddenReference = "D.E.F",
                                Reason = "legacy circular"
                            }
                        }
                    }
                }
            }
        };

        string yaml = _generator.Serialize(doc);
        string baselinePath = Path.Combine(_tempDir, "baseline.yml");
        File.WriteAllText(baselinePath, yaml);

        ArchitectureBaselineDocument loaded = _loadingService.LoadFromPath(baselinePath);

        Assert.That(loaded.Version, Is.EqualTo(1));
        Assert.That(loaded.Baseline.Strict, Has.Count.EqualTo(1));
        Assert.That(loaded.Baseline.Strict[0].Id, Is.EqualTo("dep-rule"));
        Assert.That(loaded.Baseline.Strict[0].IgnoredViolations, Has.Count.EqualTo(1));
        Assert.That(loaded.Baseline.Strict[0].IgnoredViolations[0].SourceType, Is.EqualTo("Old.Library.Service"));
        Assert.That(loaded.Baseline.Strict[0].IgnoredViolations[0].ForbiddenReference, Is.EqualTo("Deprecated.Class"));
        Assert.That(loaded.Baseline.Strict[0].IgnoredViolations[0].Reason, Is.EqualTo("migration in progress"));
        Assert.That(loaded.Baseline.StrictCycles, Has.Count.EqualTo(1));
        Assert.That(loaded.Baseline.Audit, Is.Empty);
        Assert.That(loaded.Baseline.AuditCycles, Is.Empty);
    }

    [Test]
    public void RoundTrip_Version3MetricBaselines_MaintainsCanonicalIdentityAndValue()
    {
        var doc = new ArchitectureBaselineDocument
        {
            Version = 3,
            Baseline = new ArchitectureBaselineContractGroups(),
            MetricBaselines = new List<ArchitectureMetricBaselineEntry>
            {
                new()
                {
                    MetricIdentityVersion = ArchitectureMetricBaselineIdentity.CurrentVersion,
                    MetricId = "app-footprint",
                    MetricKind = "component_footprint_count",
                    NativeSubject = "application",
                    Unit = "project",
                    EffectiveScope = "application",
                    Value = 2,
                },
            },
        };

        string yaml = _generator.Serialize(doc);
        string baselinePath = Path.Combine(_tempDir, "baseline-v3.yml");
        File.WriteAllText(baselinePath, yaml);

        ArchitectureBaselineDocument loaded = _loadingService.LoadFromPath(baselinePath);

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Version, Is.EqualTo(3));
            Assert.That(loaded.Baseline.Strict, Is.Empty);
            Assert.That(loaded.MetricBaselines, Has.Count.EqualTo(1));
            Assert.That(loaded.MetricBaselines[0].Value, Is.EqualTo(2));
            Assert.That(loaded.MetricBaselines[0].Identity, Is.EqualTo(new ArchitectureMetricBaselineIdentity(
                ArchitectureMetricBaselineIdentity.CurrentVersion,
                "app-footprint",
                "component_footprint_count",
                "application",
                "project",
                "application")));
        });
    }
}
