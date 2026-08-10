using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private const int GovernedModuleAssemblies = 20;

    // #465 — one authored directional assembly contract governs every module assembly through a
    // source set, and each resolved source keeps its own finding/baseline/explain identity.
    private static CheckpointScenarioResult AssertSourceSetAssemblyAuthoring(JsonElement expansion)
    {
        JsonElement moduleSet = Set(expansion, "module_assemblies");
        JsonElement allowOnly = Expansion(expansion, "modules-reference-abstractions-only");
        JsonElement dependency = Expansion(expansion, "modules-never-reference-the-host");
        JsonElement optional = Expansion(expansion, "future-modules-never-reference-the-host");

        string[] derivedIds = allowOnly.GetProperty("instances").EnumerateArray()
            .Select(instance => instance.GetProperty("contract_id").GetString() ?? string.Empty)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(moduleSet.GetProperty("resolved_sources").GetArrayLength(),
                Is.EqualTo(GovernedModuleAssemblies));
            Assert.That(allowOnly.GetProperty("kind").GetString(), Is.EqualTo("fan_out"));
            Assert.That(allowOnly.GetProperty("instances").GetArrayLength(),
                Is.EqualTo(GovernedModuleAssemblies));
            Assert.That(dependency.GetProperty("instances").GetArrayLength(),
                Is.EqualTo(GovernedModuleAssemblies));
            Assert.That(derivedIds, Is.Unique,
                "Every resolved source must keep its own deterministic finding/baseline identity.");
            Assert.That(derivedIds, Does.Contain("modules-reference-abstractions-only/synthetic-modules-m01"));
            Assert.That(optional.GetProperty("optional_empty").GetBoolean(), Is.True,
                "An explicitly optional empty set must be reported, not silently dropped.");
            Assert.That(optional.GetProperty("optional_reason").GetString(), Is.Not.Empty);
        });
        return Passed("source-set-assembly-authoring");
    }

    // #465 — project-metadata contracts reuse the solution-discovered project inventory after
    // include/exclude filtering instead of repeating the .csproj list per contract.
    private static CheckpointScenarioResult AssertDiscoveredProjectSetAuthoring(JsonElement expansion)
    {
        JsonElement projectSet = Set(expansion, "production_projects");
        string[] resolved = projectSet.GetProperty("resolved_sources").EnumerateArray()
            .Select(entry => entry.GetString() ?? string.Empty)
            .ToArray();
        JsonElement nullableRule = Expansion(expansion, "production-projects-are-nullable");
        JsonElement tierRule = Expansion(expansion, "production-projects-declare-a-tier");

        Assert.Multiple(() =>
        {
            Assert.That(projectSet.GetProperty("kind").GetString(), Is.EqualTo("project"));
            Assert.That(resolved, Has.Length.EqualTo(GovernedModuleAssemblies + 2),
                "Every production project, and only those, must resolve from solution discovery.");
            Assert.That(resolved, Has.None.Contains("tests/"),
                "analysis.project_exclude must filter the discovered inventory before resolution.");
            Assert.That(nullableRule.GetProperty("kind").GetString(), Is.EqualTo("inline_union"),
                "Project sets union into one contract instead of fanning out.");
            Assert.That(nullableRule.GetProperty("selector_field").GetString(), Is.EqualTo("project_sets"));
            Assert.That(nullableRule.GetProperty("instances").GetArrayLength(), Is.EqualTo(resolved.Length));
            Assert.That(tierRule.GetProperty("instances").GetArrayLength(), Is.EqualTo(resolved.Length));
        });
        return Passed("discovered-project-set-authoring");
    }

    // #465 — adding one matching target assembly changes the expansion inventory and enrols the
    // new module in every module invariant without adding another contract block.
    private static CheckpointScenarioResult AssertSourceSetEnrolment(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture consumer = AdoptionAcceptanceFixture.Create(ModularConsumerFixtureId);
        const string NewModule = "Synthetic.Modules.M21";
        const string TemplateModule = "Synthetic.Modules.M20";
        string contractsBefore = File.ReadAllText(Path.Combine(consumer.Root, "fragments", "module-contracts.yml"));

        CopyModule(consumer.Root, TemplateModule, NewModule);
        string solution = Path.Combine(consumer.Root, "Synthetic.Modular.slnx");
        File.WriteAllText(solution, File.ReadAllText(solution).Replace(
            $"<Project Path=\"src/{TemplateModule}/{TemplateModule}.csproj\" />",
            $"<Project Path=\"src/{TemplateModule}/{TemplateModule}.csproj\" />{Environment.NewLine}"
            + $"    <Project Path=\"src/{NewModule}/{NewModule}.csproj\" />",
            StringComparison.Ordinal));
        File.WriteAllText(consumer.PolicyPath, File.ReadAllText(consumer.PolicyPath).Replace(
            $"    - {TemplateModule}", $"    - {TemplateModule}{Environment.NewLine}    - {NewModule}",
            StringComparison.Ordinal));
        consumer.Build();

        CommandResult strict = candidate.RunTool(consumer.Root,
            "--policy", consumer.PolicyPath, "--strict", "--format", "json", "--ensure-built");
        using JsonDocument report = JsonDocument.Parse(strict.StandardOutput);
        JsonElement expansion = report.RootElement.GetProperty("source_set_expansion");
        string[] enrolled = Expansion(expansion, "modules-reference-abstractions-only")
            .GetProperty("instances").EnumerateArray()
            .Select(instance => instance.GetProperty("source").GetString() ?? string.Empty)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(strict.ExitCode, Is.EqualTo(0), strict.CombinedOutput);
            Assert.That(enrolled, Has.Length.EqualTo(GovernedModuleAssemblies + 1));
            Assert.That(enrolled, Does.Contain(NewModule));
            Assert.That(File.ReadAllText(Path.Combine(consumer.Root, "fragments", "module-contracts.yml")),
                Is.EqualTo(contractsBefore),
                "Enrolling a module must not require another authored contract block.");
        });
        return Passed("source-set-enrolment");
    }

    // #465 — a selector that resolves to nothing fails closed unless the set is explicitly
    // optional, so a stale inventory cannot silently disable a governed invariant.
    private static CheckpointScenarioResult AssertStaleSourceSelectorFailsClosed(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture consumer = AdoptionAcceptanceFixture.Create(ModularConsumerFixtureId);
        string sets = Path.Combine(consumer.Root, "fragments", "source-sets.yml");
        File.WriteAllText(sets, File.ReadAllText(sets).Replace(
            "globs: [Synthetic.Modules.*]", "globs: [Synthetic.Renamed.Modules.*]", StringComparison.Ordinal));

        CommandResult check = candidate.RunTool(consumer.Root,
            "policy", "check", "--policy", consumer.PolicyPath, "--format", "json");
        using JsonDocument document = JsonDocument.Parse(check.StandardOutput);
        JsonElement failure = document.RootElement.GetProperty("failure");

        Assert.Multiple(() =>
        {
            Assert.That(check.ExitCode, Is.EqualTo(2), check.CombinedOutput);
            Assert.That(failure.GetProperty("message").GetString(),
                Does.Contain("matches nothing in 'analysis.target_assemblies'"));
            Assert.That(failure.GetProperty("policy_location").GetProperty("source_path").GetString(),
                Is.EqualTo("fragments/source-sets.yml"),
                "The fail-closed diagnostic must name the fragment that authored the stale selector.");
        });
        return Passed("stale-source-selector-fail-closed");
    }

    private static ConsumerPolicyShape DescribeConsumerPolicyShape(
        AdoptionAcceptanceFixture consumer, JsonElement expansion)
    {
        string[] policyDocuments =
        [
            consumer.PolicyPath,
            .. Directory.GetFiles(Path.Combine(consumer.Root, "fragments"), "*.yml").OrderBy(
                static path => path, StringComparer.Ordinal),
        ];
        string composed = string.Concat(policyDocuments.Select(File.ReadAllText));

        int directionalContracts = expansion.GetProperty("contracts").EnumerateArray()
            .Count(contract => (contract.GetProperty("group").GetString() ?? string.Empty)
                .StartsWith("strict_assembly_", StringComparison.Ordinal));
        int directionalInstances = expansion.GetProperty("contracts").EnumerateArray()
            .Where(contract => (contract.GetProperty("group").GetString() ?? string.Empty)
                .StartsWith("strict_assembly_", StringComparison.Ordinal))
            .Sum(contract => contract.GetProperty("instances").GetArrayLength());

        return new ConsumerPolicyShape(
            PolicyDocuments: policyDocuments.Length,
            ImportedFragments: policyDocuments.Length - 1,
            GovernedModuleAssemblies: Set(expansion, "module_assemblies")
                .GetProperty("resolved_sources").GetArrayLength(),
            AuthoredDirectionalAssemblyContracts: directionalContracts,
            ExpandedDirectionalAssemblyInstances: directionalInstances,
            GovernedProjects: Set(expansion, "production_projects")
                .GetProperty("resolved_sources").GetArrayLength(),
            AuthoredProjectMetadataContracts: CountOccurrences(composed, "project_sets:"),
            DeclaredProjectInventories: CountOccurrences(composed, "  projects:"),
            InlinePublicApiSignatures: CountOccurrences(composed, "declared_api:"));
    }

    // The final synthetic consumer policy is itself release evidence: it must not need any of the
    // workaround shapes 0.6.1 exists to remove.
    private static CheckpointScenarioResult AssertConsumerPolicyShape(ConsumerPolicyShape shape)
    {
        Assert.Multiple(() =>
        {
            Assert.That(shape.ImportedFragments, Is.GreaterThan(0),
                "A composed policy must not be forced back into a monolith.");
            Assert.That(shape.AuthoredDirectionalAssemblyContracts, Is.LessThan(shape.GovernedModuleAssemblies),
                "Directional assembly invariants must not be copied once per module.");
            Assert.That(shape.ExpandedDirectionalAssemblyInstances,
                Is.GreaterThanOrEqualTo(shape.GovernedModuleAssemblies));
            Assert.That(shape.AuthoredProjectMetadataContracts, Is.GreaterThan(1),
                "More than one project-metadata contract must reuse the discovered project set.");
            Assert.That(shape.DeclaredProjectInventories, Is.Zero,
                "Solution-discovered project sets must be authoritative; no contract may repeat the inventory.");
            Assert.That(shape.InlinePublicApiSignatures, Is.Zero,
                "A reviewed public API belongs in a snapshot file, not inline in YAML.");
        });
        return Passed("consumer-policy-shape");
    }

    private static void CopyModule(string root, string template, string module)
    {
        string source = Path.Combine(root, "src", template);
        string destination = Path.Combine(root, "src", module);
        foreach (string path in Directory.EnumerateFiles(source, "*.*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, path).Replace(template, module, StringComparison.Ordinal);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, File.ReadAllText(path).Replace(template, module, StringComparison.Ordinal));
        }
    }

    private static JsonElement Set(JsonElement expansion, string name)
    {
        return expansion.GetProperty("sets").EnumerateArray()
            .SingleOrDefault(set => set.GetProperty("name").GetString() == name) is { ValueKind: JsonValueKind.Object } set
            ? set
            : throw new AssertionException($"The expansion inventory has no source set '{name}'.");
    }

    private static JsonElement Expansion(JsonElement expansion, string authoredContractId)
    {
        return expansion.GetProperty("contracts").EnumerateArray()
            .SingleOrDefault(contract =>
                contract.GetProperty("authored_contract_id").GetString() == authoredContractId)
            is { ValueKind: JsonValueKind.Object } contract
            ? contract
            : throw new AssertionException($"The expansion inventory has no contract '{authoredContractId}'.");
    }

    private sealed record ConsumerPolicyShape(
        int PolicyDocuments,
        int ImportedFragments,
        int GovernedModuleAssemblies,
        int AuthoredDirectionalAssemblyContracts,
        int ExpandedDirectionalAssemblyInstances,
        int GovernedProjects,
        int AuthoredProjectMetadataContracts,
        int DeclaredProjectInventories,
        int InlinePublicApiSignatures);

    private static int CountOccurrences(string text, string token)
    {
        int count = 0;
        int index = text.IndexOf(token, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(token, index + token.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
