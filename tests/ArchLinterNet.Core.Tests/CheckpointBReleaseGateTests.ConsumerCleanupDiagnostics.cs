using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    // F6 — an invalid composed policy produces one actionable diagnostic that names the real
    // defect and points at the fragment that declares it, without reporting alternatives whose
    // constant discriminator selects another variant.
    private static CheckpointScenarioResult AssertActionableSchemaDiagnostics(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture consumer = AdoptionAcceptanceFixture.Create(ModularConsumerFixtureId);
        string fragment = Path.Combine(consumer.Root, "fragments", "module-contracts.yml");
        File.WriteAllText(fragment, File.ReadAllText(fragment).Replace(
            "      scope: assembly",
            $"      scope: assembly{Environment.NewLine}      roots: [Synthetic.Modules]",
            StringComparison.Ordinal));

        CommandResult check = candidate.RunTool(consumer.Root,
            "policy", "check", "--policy", consumer.PolicyPath, "--format", "json");

        using JsonDocument document = JsonDocument.Parse(check.StandardOutput);
        JsonElement failure = document.RootElement.GetProperty("failure");
        string message = failure.GetProperty("message").GetString() ?? string.Empty;
        JsonElement location = failure.GetProperty("policy_location");
        string sourcePath = location.GetProperty("source_path").GetString() ?? string.Empty;
        string yamlPath = location.GetProperty("yaml_path").GetString() ?? string.Empty;

        Assert.Multiple(() =>
        {
            Assert.That(check.ExitCode, Is.EqualTo(2), check.CombinedOutput);
            Assert.That(document.RootElement.GetProperty("status").GetString(), Is.EqualTo("invalid-policy"));
            Assert.That(message, Does.Contain("'roots' is not valid for assembly coverage"),
                "The actual defect must always be reported.");
        });

        bool actionable = !message.Contains("/scope: Expected", StringComparison.Ordinal)
            && sourcePath.EndsWith("module-contracts.yml", StringComparison.Ordinal)
            && yamlPath.StartsWith("contracts.strict_coverage", StringComparison.Ordinal);

        return Verdict("actionable-schema-diagnostics", actionable,
            $"Composed-policy schema diagnostics must suppress inapplicable constant-discriminator "
            + $"alternatives and locate the authored defect.{Environment.NewLine}"
            + $"message: {message}{Environment.NewLine}location: {sourcePath}:{yamlPath}");
    }

    // F9 — namespace allowance fields use the documented constrained glob grammar: one glob
    // covers every module composition boundary, a call from outside it is still a violation, and
    // an unsupported wildcard fails policy loading instead of silently matching nothing.
    private static CheckpointScenarioResult AssertNamespaceAllowancePattern(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture consumer = AdoptionAcceptanceFixture.Create(ModularConsumerFixtureId);
        File.WriteAllText(Path.Combine(consumer.Root, "src", "Synthetic.Modules.M07", "Leak.cs"), """
            using Synthetic.Shared.Abstractions;

            namespace Synthetic.Modules.M07;

            public static class Leak
            {
                public static IModule? Resolve(IServiceProvider provider) =>
                    provider.GetService(typeof(IModule)) as IModule;
            }
            """);
        consumer.Build();

        CommandResult leaked = candidate.RunTool(consumer.Root,
            "--policy", consumer.PolicyPath, "--strict", "--format", "json", "--ensure-built");

        string fragment = Path.Combine(consumer.Root, "fragments", "module-contracts.yml");
        File.WriteAllText(fragment, File.ReadAllText(fragment).Replace(
            "- Synthetic.Modules.*.Composition",
            "- Synthetic.Modules.**.Composition",
            StringComparison.Ordinal));
        CommandResult unsupported = candidate.RunTool(consumer.Root,
            "policy", "check", "--policy", consumer.PolicyPath, "--format", "json");

        using JsonDocument report = JsonDocument.Parse(leaked.StandardOutput);
        JsonElement violations = report.RootElement.GetProperty("violations");
        using JsonDocument rejected = JsonDocument.Parse(unsupported.StandardOutput);

        Assert.Multiple(() =>
        {
            Assert.That(leaked.ExitCode, Is.EqualTo(1), leaked.CombinedOutput);
            Assert.That(violations.GetArrayLength(), Is.EqualTo(1),
                "Only the call outside every allowed composition namespace may be reported.");
            Assert.That(violations[0].GetProperty("contract_id").GetString(),
                Is.EqualTo("service-resolution-confined-to-composition"));
            Assert.That(unsupported.ExitCode, Is.EqualTo(2), unsupported.CombinedOutput);
            Assert.That(rejected.RootElement.GetProperty("failure").GetProperty("message").GetString(),
                Does.Contain("Recursive wildcard '**' is not supported"));
        });
        return Passed("namespace-allowance-pattern");
    }

    // F10 — every command family that offers JSON emits one parseable JSON document on its owned
    // configuration-error termination path, so automated consumers need no fallback text parsing.
    private static CheckpointScenarioResult AssertJsonConfigurationErrorFormat(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture consumer)
    {
        string absent = Path.Combine(consumer.Root, "absent.arch.yml");
        CommandResult validate = candidate.RunTool(consumer.Root,
            "--policy", absent, "--strict", "--format", "json");
        CommandResult baseline = candidate.RunTool(consumer.Root,
            "baseline", "verify", "--config", absent, "--baseline", "absent-baseline.yml", "--format", "json");
        CommandResult publicApi = candidate.RunTool(consumer.Root,
            "public-api", "diff", "--policy", absent, "--contract", "absent", "--snapshot", "absent.txt",
            "--format", "json");
        CommandResult policyCheck = candidate.RunTool(consumer.Root,
            "policy", "check", "--policy", absent, "--format", "json");

        Assert.Multiple(() =>
        {
            foreach ((string command, CommandResult result) in new[]
                     {
                         ("validate", validate), ("baseline verify", baseline),
                         ("public-api diff", publicApi), ("policy check", policyCheck),
                     })
            {
                Assert.That(result.ExitCode, Is.EqualTo(2), $"{command}: {result.CombinedOutput}");
                Assert.That(() => JsonDocument.Parse(result.StandardOutput).Dispose(), Throws.Nothing,
                    $"{command} must emit one parseable JSON document on stdout: {result.CombinedOutput}");
            }
        });
        return Passed("json-configuration-error-format");
    }
}
