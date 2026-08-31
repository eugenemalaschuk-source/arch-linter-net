using System.Text.Json;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Execution;
using ArchLinterNet.Core.Model;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using NUnit.Framework;
using ArchitectureContractGroups = ArchLinterNet.Core.Contracts.Families.ArchitectureContractGroups;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class ArchitectureWaiverLifecycleEvaluatorTests
{
    [Test]
    public void Evaluate_ExpiredUnmatchedWaiver_RemainsExpired()
    {
        ArchitectureIgnoredViolation waiver = CreateStructuredWaiver(expires: "2026-08-01");
        ArchitectureContractDocument document = CreateDocument(waiver);
        var unmatched = new ArchitectureUnmatchedIgnoredViolation(
            "boundary", "boundary", 0, waiver.SourceType, waiver.ForbiddenReference, waiver.Reason)
        {
            ContractGroup = "strict"
        };

        ArchitectureWaiverLifecycleRecord record = ArchitectureWaiverLifecycleEvaluator.Evaluate(
            document, "strict", [unmatched], new DateOnly(2026, 8, 2)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo("expired"));
            Assert.That(record.MatchesGovernedFinding, Is.False);
            Assert.That(record.EvaluationDate, Is.EqualTo(new DateOnly(2026, 8, 2)));
        });
    }

    [Test]
    public void Evaluate_LegacyMatchedWaiver_IsMetadataIncomplete()
    {
        ArchitectureIgnoredViolation legacy = new()
        {
            SourceType = "App.Legacy",
            ForbiddenReference = "Infrastructure.Db",
            Reason = "Legacy extraction"
        };

        ArchitectureWaiverLifecycleRecord record = ArchitectureWaiverLifecycleEvaluator.Evaluate(
            CreateDocument(legacy), "strict", Array.Empty<ArchitectureUnmatchedIgnoredViolation>(), new DateOnly(2026, 8, 2)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo("metadata_incomplete"));
            Assert.That(record.Id, Does.StartWith("legacy-"));
            Assert.That(record.MatchesGovernedFinding, Is.True);
        });
    }

    [Test]
    public void Evaluate_SelectedContracts_ExcludesUnselectedExpiredWaivers()
    {
        ArchitectureIgnoredViolation first = CreateStructuredWaiver(expires: "2026-10-01");
        ArchitectureIgnoredViolation second = CreateStructuredWaiver(expires: "2026-08-01");
        second.WaiverId = "ARCH-IGN-002";
        ArchitectureContractDocument document = CreateDocument(first);
        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Id = "second-boundary",
            Name = "second-boundary",
            Source = "app",
            Forbidden = new List<string> { "infrastructure" },
            IgnoredViolations = new List<ArchitectureIgnoredViolation> { second }
        });

        IReadOnlyList<ArchitectureWaiverLifecycleRecord> records = ArchitectureWaiverLifecycleEvaluator.Evaluate(
            document, "strict", [], new DateOnly(2026, 8, 2), ["boundary"]);

        Assert.That(records.Select(record => record.Id), Is.EquivalentTo(new[] { "ARCH-IGN-001" }));
    }

    [Test]
    public void Evaluate_SourceSetAliases_AggregatesMatchingStateForTheAuthoredWaiver()
    {
        ArchitectureIgnoredViolation waiver = CreateStructuredWaiver(expires: "2026-10-01");
        ArchitectureContractDocument document = CreateDocument(waiver);
        document.Contracts.Strict.Add(new ArchitectureDependencyContract
        {
            Id = "boundary-domain",
            Name = "boundary-domain",
            Source = "domain",
            Forbidden = new List<string> { "infrastructure" },
            IgnoredViolations = new List<ArchitectureIgnoredViolation> { waiver },
        });
        var unmatched = new ArchitectureUnmatchedIgnoredViolation(
            "boundary", "boundary", 0, waiver.SourceType, waiver.ForbiddenReference, waiver.Reason)
        {
            ContractGroup = "strict",
        };

        ArchitectureWaiverLifecycleRecord record = ArchitectureWaiverLifecycleEvaluator.Evaluate(
            document, "strict", [unmatched], new DateOnly(2026, 8, 2)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo("active"));
            Assert.That(record.MatchesGovernedFinding, Is.True);
        });
    }

    [TestCase("2026-10-01", false, false, "active")]
    [TestCase("2026-10-01", true, false, "stale")]
    [TestCase("2026-08-01", false, false, "expired")]
    [TestCase("2026-10-01", false, true, "invalid")]
    public void Evaluate_SelectedAuthoredSourceSetId_RetainsLifecycleDebt(
        string expires,
        bool isUnmatched,
        bool isInvalid,
        string expectedState)
    {
        ArchitectureIgnoredViolation waiver = CreateStructuredWaiver(expires);
        if (isInvalid)
        {
            waiver.WaiverValidationError = "target.fingerprint must be canonical";
        }
        ArchitectureContractDocument document = new()
        {
            Version = 2,
            Name = "Source-set waiver",
            Contracts = new ArchitectureContractGroups
            {
                StrictExternal =
                [
                    new ArchitectureExternalDependencyContract
                    {
                        Id = "boundary/app",
                        Name = "boundary",
                        Source = "app",
                        ExpansionOrigin = new ArchitectureSourceExpansionOrigin(
                            "boundary", "boundary", "app", "applications", "app"),
                        IgnoredViolations = [waiver],
                    },
                ],
            },
        };
        string contractGroup = ArchitectureContractCatalog.Build(document).Descriptors.Single().Group;
        ArchitectureUnmatchedIgnoredViolation[] unmatched = isUnmatched
            ?
            [
                new ArchitectureUnmatchedIgnoredViolation(
                    "boundary", "boundary/app", 0, waiver.SourceType, waiver.ForbiddenReference, waiver.Reason)
                {
                    ContractGroup = contractGroup,
                },
            ]
            : [];

        ArchitectureWaiverLifecycleRecord record = ArchitectureWaiverLifecycleEvaluator.Evaluate(
            document, "strict", unmatched, new DateOnly(2026, 8, 2), ["BOUNDARY"]).Single();

        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo(expectedState));
            Assert.That(record.Id, Is.EqualTo("ARCH-IGN-001"));
        });
    }

    [Test]
    public void Evaluate_InvalidWaiver_TakesPrecedence()
    {
        ArchitectureIgnoredViolation waiver = CreateStructuredWaiver(expires: "2026-08-01");
        waiver.WaiverValidationError = "target.fingerprint must be canonical";

        ArchitectureWaiverLifecycleRecord record = ArchitectureWaiverLifecycleEvaluator.Evaluate(
            CreateDocument(waiver), "strict", [], new DateOnly(2026, 8, 2)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(record.State, Is.EqualTo("invalid"));
        });
    }

    [Test]
    public void Formatters_RenderCanonicalLifecycleFieldsForHumanAndJsonConsumers()
    {
        ArchitectureWaiverLifecycleRecord waiver = new(
            "ARCH-IGN-001", "expired", "boundary", "boundary", "strict", "App.Legacy",
            "Infrastructure.Db", "sha256:" + new string('a', 64), "Legacy extraction", "architecture-team",
            "ARCH-231", new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2), false);
        var formatter = new ArchitectureDiagnosticFormatter();

        string human = formatter.FormatWaiversForHumans([waiver]);
        string json = ArchitectureDiagnosticFormatter.AddWaiversToCiArtifacts("{\"passed\":false}", [waiver]);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement serialized = document.RootElement.GetProperty("waivers").EnumerateArray().Single();

        Assert.Multiple(() =>
        {
            Assert.That(human, Does.Contain("[expired] ARCH-IGN-001"));
            Assert.That(human, Does.Contain("target: sha256:"));
            Assert.That(human, Does.Contain("reason: Legacy extraction"));
            Assert.That(human, Does.Contain("expires: 2026-08-01"));
            Assert.That(serialized.GetProperty("state").GetString(), Is.EqualTo("expired"));
            Assert.That(serialized.GetProperty("evaluation_date").GetString(), Is.EqualTo("2026-08-02"));
            Assert.That(serialized.GetProperty("matches_governed_finding").GetBoolean(), Is.False);
        });
    }

    private static ArchitectureContractDocument CreateDocument(ArchitectureIgnoredViolation waiver) => new()
    {
        Version = 2,
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
                    IgnoredViolations = new List<ArchitectureIgnoredViolation> { waiver }
                }
            }
        }
    };

    private static ArchitectureIgnoredViolation CreateStructuredWaiver(string expires) => new()
    {
        WaiverId = "ARCH-IGN-001",
        SourceType = "App.Legacy",
        ForbiddenReference = "Infrastructure.Db",
        Target = new ArchitectureWaiverTarget { Fingerprint = "sha256:" + new string('a', 64) },
        Reason = "Legacy extraction",
        Owner = "architecture-team",
        Issue = "ARCH-231",
        Introduced = "2026-07-01",
        Expires = expires
    };
}
