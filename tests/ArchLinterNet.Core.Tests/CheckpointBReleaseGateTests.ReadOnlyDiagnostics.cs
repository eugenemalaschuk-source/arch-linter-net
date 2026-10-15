using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    [Test]
    [Category("ConsumerAcceptance")]
    [Category("ReceiptBackedReaders")]
    public void PackedCandidate_ReadOnlyDiagnosticsUsePreparedReceipts()
    {
        CandidatePackageFeed candidate = Candidate;
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create("read-only-diagnostics");
        fixture.Build();

        string policy = fixture.PolicyPath;
        string baseline = Path.Combine(fixture.Root, "baseline.arch.yml");
        string outputDirectory = Path.Combine(fixture.Root, "bin", "Debug", "net10.0");
        string consumerAssembly = Path.Combine(outputDirectory, "Repro.Consumer.dll");
        string dependencyAssembly = Path.Combine(outputDirectory, "Microsoft.Extensions.DependencyInjection.Abstractions.dll");
        Assert.That(File.Exists(consumerAssembly), Is.True, consumerAssembly);
        Assert.That(File.Exists(dependencyAssembly), Is.True, dependencyAssembly);
        Assert.That(AssemblyName.GetAssemblyName(dependencyAssembly).Version?.Major, Is.EqualTo(10),
            "The consumer fixture must load the DI Abstractions 10 assembly beside its own output.");

        // The only build-capable CLI call in this scenario is the producer. The following
        // independent tool processes use only the candidate package's reader path.
        CommandResult producer = candidate.RunTool(fixture.Root,
            "--policy", policy, "--mode", "strict", "--format", "json",
            "--ensure-built", "--no-restore");
        Assert.That(producer.ExitCode, Is.EqualTo(0), producer.CombinedOutput);

        string[] receipts = Directory.GetFiles(fixture.Root, "*.archlinternet-receipt.json", SearchOption.AllDirectories);
        Assert.That(receipts, Has.Length.GreaterThanOrEqualTo(1), "The selected consumer output must be receipt-backed.");
        Dictionary<string, string> before = HashSelectedOutputs(fixture.Root);
        Assert.That(before.Keys.Any(path => path.EndsWith("Repro.Consumer.dll", StringComparison.Ordinal)), Is.True);
        Assert.That(before.Keys.Any(path => path.EndsWith("Microsoft.Extensions.DependencyInjection.Abstractions.dll", StringComparison.Ordinal)), Is.True);
        Assert.That(before.Keys.Any(path => path.EndsWith("Repro.Consumer.dll.archlinternet-receipt.json", StringComparison.Ordinal)), Is.True);
        Assert.That(before.Keys, Does.Contain("obj/project.assets.json"), "The restore marker must exist before reader invocations.");
        Assert.That(before.Keys, Does.Contain("build-invocations.log"), "The build invocation marker must exist before reader invocations.");
        Assert.That(before.Keys, Does.Contain("restore-invocations.log"), "The restore invocation marker must exist before reader invocations.");
        string buildInvocationMarker = File.ReadAllText(Path.Combine(fixture.Root, "build-invocations.log"));
        string restoreInvocationMarker = File.ReadAllText(Path.Combine(fixture.Root, "restore-invocations.log"));

        Dictionary<string, string> readerBaseline = HashSelectedOutputs(fixture.Root);

        CommandResult ordinaryCapture = candidate.RunTool(fixture.Root,
            "topology", "capture", "--policy", policy, "--subject-kind", "type", "--format", "json",
            "--no-restore");
        Assert.That(ordinaryCapture.ExitCode, Is.Not.EqualTo(0),
            "Ordinary topology capture without prepared receipts must fail closed: " + ordinaryCapture.CombinedOutput);
        using (JsonDocument ordinaryDocument = JsonDocument.Parse(ordinaryCapture.StandardOutput))
        {
            Assert.That(ordinaryDocument.RootElement.GetProperty("preflight_blocked").GetBoolean(), Is.True,
                ordinaryCapture.CombinedOutput);
            Assert.That(ordinaryDocument.RootElement.GetProperty("subjects").GetArrayLength(), Is.Zero,
                "A failed ordinary read must not publish partial topology subjects.");
        }

        CommandResult preparedCapture = candidate.RunTool(fixture.Root,
            "topology", "capture", "--policy", policy, "--subject-kind", "type", "--format", "json",
            "--use-prepared-receipts", "--no-restore");
        Assert.That(preparedCapture.ExitCode, Is.EqualTo(0), preparedCapture.CombinedOutput);
        using JsonDocument preparedCaptureDocument = JsonDocument.Parse(preparedCapture.StandardOutput);
        JsonElement preparedSubjects = preparedCaptureDocument.RootElement.GetProperty("subjects");
        AssertCompleteSubjects(preparedSubjects, preparedCapture.StandardOutput);
        AssertCurrentPreflight(preparedCaptureDocument.RootElement, preparedCapture.StandardOutput);

        CommandResult preparedHealth = candidate.RunTool(fixture.Root,
            "health", "--policy", policy, "--baseline", baseline, "--mode", "all", "--format", "json",
            "--execution-context", "read-only-reader", "--use-prepared-receipts", "--no-restore");
        Assert.That(preparedHealth.ExitCode, Is.EqualTo(0), preparedHealth.CombinedOutput);
        using JsonDocument preparedHealthDocument = JsonDocument.Parse(preparedHealth.StandardOutput);
        AssertCurrentPreflight(preparedHealthDocument.RootElement, preparedHealth.StandardOutput);

        string changeSnapshot = Path.Combine(fixture.Root, "reader-change-snapshot.json");
        CommandResult preparedHealthWithChange = candidate.RunTool(fixture.Root,
            "health", "--policy", policy, "--baseline", baseline, "--mode", "all", "--format", "json",
            "--execution-context", "read-only-reader-change", "--change-snapshot", changeSnapshot,
            "--use-prepared-receipts", "--no-restore");
        Assert.That(preparedHealthWithChange.ExitCode, Is.EqualTo(0), preparedHealthWithChange.CombinedOutput);
        Assert.That(File.Exists(changeSnapshot), Is.True, changeSnapshot);
        using JsonDocument preparedHealthWithChangeDocument = JsonDocument.Parse(preparedHealthWithChange.StandardOutput);
        AssertCurrentPreflight(preparedHealthWithChangeDocument.RootElement, preparedHealthWithChange.StandardOutput);
        AssertHealthSemanticsEqual(preparedHealthDocument.RootElement, preparedHealthWithChangeDocument.RootElement);

        (string Outcome, int UnmappedSubjectCount)? strictAuditDiffParity = null;
        foreach (string mode in new[] { "strict", "audit" })
        {
            string diffPath = Path.Combine(fixture.Root, $"reader-topology-{mode}-diff.json");
            CommandResult diff = candidate.RunTool(fixture.Root,
                "topology", "diff", "--policy", policy, "--mode", mode, "--format", "json",
                "--output", diffPath, "--use-prepared-receipts", "--no-restore");
            Assert.That(diff.ExitCode, Is.EqualTo(0), diff.CombinedOutput);
            using JsonDocument diffDocument = JsonDocument.Parse(File.ReadAllText(diffPath));
            Assert.That(diffDocument.RootElement.GetProperty("outcome").GetString(), Is.EqualTo("reviewable"), diff.StandardOutput);
            int unmappedSubjectCount = diffDocument.RootElement.GetProperty("evidence")
                .GetProperty("unmapped_subject_count").GetInt32();
            Assert.That(unmappedSubjectCount, Is.Zero);
            (string Outcome, int UnmappedSubjectCount) diffParity = (
                diffDocument.RootElement.GetProperty("outcome").GetString()!, unmappedSubjectCount);
            if (strictAuditDiffParity is { } previousDiffParity)
            {
                Assert.That(diffParity, Is.EqualTo(previousDiffParity),
                    "Strict and audit topology diff must agree on the complete consumer observation.");
            }
            else
            {
                strictAuditDiffParity = diffParity;
            }

            CommandResult verify = candidate.RunTool(fixture.Root,
                "topology", "verify", "--policy", policy, "--mode", mode, "--format", "json",
                "--use-prepared-receipts", "--no-restore");
            Assert.That(verify.ExitCode, Is.EqualTo(0), verify.CombinedOutput);
            using JsonDocument verifyDocument = JsonDocument.Parse(verify.StandardOutput);
            AssertCurrentPreflight(verifyDocument.RootElement, verify.StandardOutput);
        }

        Dictionary<string, string> after = HashSelectedOutputs(fixture.Root);
        Assert.That(after, Is.EqualTo(readerBaseline), "Reader commands rewrote a selected consumer or dependency output.");

        // A stale selected input must fail before topology analysis, then deleting the receipt must
        // fail for the same reason. These probes run after the successful reader matrix and leave
        // no state that can affect another test because the fixture is disposable.
        string sourcePath = Path.Combine(fixture.Root, "Scope.cs");
        string originalSource = File.ReadAllText(sourcePath);
        File.AppendAllText(sourcePath, Environment.NewLine + "// stale receipt probe" + Environment.NewLine);
        CommandResult stale = candidate.RunTool(fixture.Root,
            "topology", "capture", "--policy", policy, "--subject-kind", "type", "--format", "json",
            "--use-prepared-receipts", "--no-restore");
        Assert.That(stale.ExitCode, Is.Not.EqualTo(0), stale.CombinedOutput);
        Assert.That(stale.CombinedOutput, Does.Contain("stale").IgnoreCase);
        File.WriteAllText(sourcePath, originalSource);

        string consumerReceipt = receipts.Single(path => path.EndsWith("Repro.Consumer.dll.archlinternet-receipt.json", StringComparison.Ordinal));
        File.Delete(consumerReceipt);
        CommandResult missing = candidate.RunTool(fixture.Root,
            "health", "--policy", policy, "--baseline", baseline, "--mode", "all", "--format", "json",
            "--execution-context", "read-only-missing", "--use-prepared-receipts", "--no-restore");
        Assert.That(missing.ExitCode, Is.Not.EqualTo(0), missing.CombinedOutput);
        Assert.That(missing.CombinedOutput, Does.Contain("receipt").IgnoreCase);
        Assert.That(File.ReadAllText(Path.Combine(fixture.Root, "build-invocations.log")), Is.EqualTo(buildInvocationMarker));
        Assert.That(File.ReadAllText(Path.Combine(fixture.Root, "restore-invocations.log")), Is.EqualTo(restoreInvocationMarker));

        candidate.WriteShardEvidence("read-only-diagnostics-prepared-receipts",
            [Passed("read-only-diagnostics-prepared-receipts")]);
    }

    private static Dictionary<string, string> HashSelectedOutputs(string root)
    {
        string[] patterns = ["*.dll", "*.pdb", "*.deps.json", "*.runtimeconfig.json", "*.archlinternet-receipt.json"];
        string[] invocationMarkers = ["build-invocations.log", "restore-invocations.log"];
        return patterns
            .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains("/bin/", StringComparison.Ordinal))
            .Concat(new[]
            {
                Path.Combine(root, "obj", "project.assets.json"),
                Path.Combine(root, "obj", "project.nuget.cache"),
            }.Concat(invocationMarkers.Select(marker => Path.Combine(root, marker)))
                .Where(File.Exists))
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                StringComparer.Ordinal);
    }

    private static void AssertCompleteSubjects(JsonElement subjects, string output)
    {
        string[] names = subjects.EnumerateArray()
            .Select(subject => subject.GetProperty("subject").GetString() ?? string.Empty)
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("Repro.Consumer.Scope"), output);
            Assert.That(names, Does.Contain("Repro.Consumer.ScopeProperty"), output);
            Assert.That(names, Has.No.Member(string.Empty), output);
        });
    }

    private static void AssertCurrentPreflight(JsonElement root, string output)
    {
        string json = root.GetRawText();
        Assert.That(json, Does.Contain("\"state\":\"current\""),
            "The reader must publish current receipt preflight evidence: " + output);
    }

    private static void AssertHealthSemanticsEqual(JsonElement expected, JsonElement actual)
    {
        Assert.That(expected.GetProperty("schema_id").GetString(), Is.EqualTo(actual.GetProperty("schema_id").GetString()));
        Assert.That(expected.GetProperty("gate").GetString(), Is.EqualTo(actual.GetProperty("gate").GetString()));
        Assert.That(expected.GetProperty("health").GetString(), Is.EqualTo(actual.GetProperty("health").GetString()));
        Assert.That(expected.GetProperty("dimensions").GetRawText(), Is.EqualTo(actual.GetProperty("dimensions").GetRawText()));
    }
}
