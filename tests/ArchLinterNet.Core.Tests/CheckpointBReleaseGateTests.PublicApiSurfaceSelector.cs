using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    private static readonly string[] _value = { "added", "removed", "changed" };
    private const string ApiSurfaceSelectorFixtureId = "api-surface-selector";

    // Appended once for the whole matrix: three sibling strict_public_api_surface contracts over
    // the same assembly (no selector, has_attribute selector, namespace selector) plus one
    // role-based governance contract proving ValueObject stays a real, enforced role after
    // selection. escaping-selected-api is intentionally NOT here — issue #526 item 10's fail-closed
    // proof appends and reverts it around a single call so this permanent set always stays green.
    private const string PermanentSurfaceSelectorContracts = """

contracts:
  strict_context_allow_only:
    - id: value-objects-depend-only-on-bcl
      name: value-objects-depend-only-on-bcl
      source: { role: ValueObject }
      allowed:
        - role: ApiContract
      reason: Existing role-based governance for ValueObject types must remain unaffected by API-surface selection. No type in this fixture is classified ApiContract, so a ValueObject may reference BCL types only.
  strict_public_api_surface:
    - id: assembly-wide-api
      name: assembly-wide-api
      assemblies: [Synthetic.ApiSurfaceSelector]
      api_snapshot: public-api/assembly-wide-api.txt
      api_comparison: exact
      reason: An existing policy with no selector must retain assembly-wide #94 behavior.
    - id: marker-selected-api
      name: marker-selected-api
      assemblies: [Synthetic.ApiSurfaceSelector]
      surface_selector:
        has_attribute: Synthetic.ApiSurfaceSelector.Architecture.PublicApiContractAttribute
      api_snapshot: public-api/marker-selected-api.txt
      api_comparison: exact
      reason: Only the intentionally marked compatibility surface is reviewed.
    - id: namespace-selected-api
      name: namespace-selected-api
      assemblies: [Synthetic.ApiSurfaceSelector]
      surface_selector:
        namespace: Synthetic.ApiSurfaceSelector.Api
      api_snapshot: public-api/namespace-selected-api.txt
      api_comparison: exact
      reason: A second bounded selector source proves selection is not annotation-specific.
""";

    private const string EscapingSelectedApiContract = """

    - id: escaping-selected-api
      name: escaping-selected-api
      assemblies: [Synthetic.ApiSurfaceSelector]
      surface_selector:
        has_attribute: Synthetic.ApiSurfaceSelector.Architecture.EscapeDemoApiContractAttribute
      api_snapshot: public-api/escaping-selected-api.txt
      api_comparison: exact
      reason: Demonstrates the fail-closed first-party escape check; never part of the permanent contract set.
""";

    // Issue #526: prove, from freshly packed v0.6.4 artifacts, that surface_selector (#525/#529)
    // lets a modular consumer replace a whole-assembly reviewed API snapshot with a materially
    // smaller intentional one, selected two bounded ways, without touching CLR visibility or
    // existing semantic roles, and with exact governance, review-visibility, and fail-closed
    // escape checks all still enforced on the selected surface.
    private static List<CheckpointScenarioResult> AssertPublicApiSurfaceSelectorMatrix(CandidatePackageFeed candidate)
    {
        using AdoptionAcceptanceFixture fixture = AdoptionAcceptanceFixture.Create(ApiSurfaceSelectorFixtureId);
        Directory.CreateDirectory(Path.Combine(fixture.Root, "public-api"));
        File.AppendAllText(fixture.PolicyPath, PermanentSurfaceSelectorContracts);
        fixture.Build(configuration: "Release", targetFramework: "net10.0");

        return
        [
            AssertSurfaceSelectorSnapshotReduction(candidate, fixture),
            AssertSurfaceSelectorRolePreservation(candidate, fixture),
            AssertSurfaceSelectorExactDeltaLifecycle(candidate, fixture),
            AssertSurfaceSelectorMembershipReviewVisibility(candidate, fixture),
            AssertSurfaceSelectorEscapeFailsClosed(candidate, fixture),
            AssertSurfaceSelectorStrictRunIsGreen(candidate, fixture),
            candidate.AssertPublicApiSurfaceSelectorTestingParity(fixture),
        ];
    }

    // Items 1, 3, 4, 11, 12, 14 — capture all three sibling contracts from one build, prove the
    // selected snapshots omit every incidental type the assembly-wide sibling still governs, prove
    // a consumer can validate the selected contracts alone (the "replace the old snapshot" path),
    // prove BCL-only members never demanded evidence, and prove the marker never had to be mapped
    // into semantic classification.
    private static CheckpointScenarioResult AssertSurfaceSelectorSnapshotReduction(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture fixture)
    {
        CommandResult assemblyWide = candidate.RunTool(fixture.Root,
            "public-api", "capture", "--policy", fixture.PolicyPath, "--contract", "assembly-wide-api",
            "--output", "public-api/assembly-wide-api.txt", "--ensure-built", "--format", "json");
        CommandResult markerSelected = candidate.RunTool(fixture.Root,
            "public-api", "capture", "--policy", fixture.PolicyPath, "--contract", "marker-selected-api",
            "--output", "public-api/marker-selected-api.txt", "--ensure-built", "--format", "json");
        CommandResult namespaceSelected = candidate.RunTool(fixture.Root,
            "public-api", "capture", "--policy", fixture.PolicyPath, "--contract", "namespace-selected-api",
            "--output", "public-api/namespace-selected-api.txt", "--ensure-built", "--format", "json");

        string assemblyWideSnapshot = File.ReadAllText(Path.Combine(fixture.Root, "public-api", "assembly-wide-api.txt"));
        string markerSnapshot = File.ReadAllText(Path.Combine(fixture.Root, "public-api", "marker-selected-api.txt"));
        string namespaceSnapshot = File.ReadAllText(Path.Combine(fixture.Root, "public-api", "namespace-selected-api.txt"));

        string[] incidentalTypeNames =
        [
            "InternalPricingEngine", "InternalTaxCalculator", "InternalLedgerWriter",
            "ModuleOptions", "RetrySettings", "CacheOptions",
        ];

        // The "replace the old snapshot" consumer-exit path: the selected contracts validate on
        // their own, with no dependency on the assembly-wide sibling contract or its snapshot.
        CommandResult selectedOnly = candidate.RunTool(fixture.Root,
            "--policy", fixture.PolicyPath, "--strict", "--format", "json", "--ensure-built",
            "--contract", "marker-selected-api", "--contract", "namespace-selected-api");

        Assert.Multiple(() =>
        {
            Assert.That(assemblyWide.ExitCode, Is.EqualTo(0), assemblyWide.CombinedOutput);
            Assert.That(markerSelected.ExitCode, Is.EqualTo(0), markerSelected.CombinedOutput);
            Assert.That(namespaceSelected.ExitCode, Is.EqualTo(0), namespaceSelected.CombinedOutput);

            foreach (string incidental in incidentalTypeNames)
            {
                Assert.That(assemblyWideSnapshot, Does.Contain(incidental),
                    $"An unselected #94 contract must still govern every incidental type ({incidental}).");
                Assert.That(markerSnapshot, Does.Not.Contain(incidental),
                    $"The marker-selected snapshot must exclude incidental type {incidental}.");
                Assert.That(namespaceSnapshot, Does.Not.Contain(incidental),
                    $"The namespace-selected snapshot must exclude incidental type {incidental}.");
            }

            Assert.That(assemblyWideSnapshot, Does.Contain("Synthetic.ApiSurfaceSelector.Domain.Money"));
            Assert.That(markerSnapshot, Does.Contain("Synthetic.ApiSurfaceSelector.Domain.Money"),
                "The has_attribute selector must select Money.");
            Assert.That(markerSnapshot, Does.Not.Contain("Synthetic.ApiSurfaceSelector.Api.ApiFacade"),
                "The has_attribute selector must not select the namespace-only ApiFacade.");
            Assert.That(namespaceSnapshot, Does.Contain("Synthetic.ApiSurfaceSelector.Api.ApiFacade"),
                "The namespace selector must select ApiFacade.");
            Assert.That(namespaceSnapshot, Does.Not.Contain("Synthetic.ApiSurfaceSelector.Domain.Money"),
                "The namespace selector must not select the marker-only Money.");

            int assemblyWideLineCount = CountSnapshotEntries(assemblyWideSnapshot);
            int markerLineCount = CountSnapshotEntries(markerSnapshot);
            int namespaceLineCount = CountSnapshotEntries(namespaceSnapshot);
            Assert.That(assemblyWideLineCount, Is.GreaterThan(markerLineCount * 2),
                $"The selected snapshot must be materially smaller: assembly-wide={assemblyWideLineCount}, " +
                $"marker-selected={markerLineCount}.");
            Assert.That(assemblyWideLineCount, Is.GreaterThan(namespaceLineCount * 2),
                $"The selected snapshot must be materially smaller: assembly-wide={assemblyWideLineCount}, " +
                $"namespace-selected={namespaceLineCount}.");

            Assert.That(selectedOnly.ExitCode, Is.EqualTo(0), selectedOnly.CombinedOutput);

            // Item 14: the orthogonal marker must never require mapping into semantic classification.
            string policyText = File.ReadAllText(fixture.PolicyPath);
            int classificationStart = policyText.IndexOf("classification:", StringComparison.Ordinal);
            int contractsStart = policyText.IndexOf("contracts:", StringComparison.Ordinal);
            Assert.That(classificationStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(contractsStart, Is.GreaterThan(classificationStart));
            string classificationBlock = policyText[classificationStart..contractsStart];
            Assert.That(classificationBlock, Does.Not.Contain("PublicApiContractAttribute"),
                "The orthogonal API marker must never be mapped into semantic classification.");
            Assert.That(classificationBlock, Does.Contain("ValueObjectRoleAttribute"));
        });
        return Passed("public-api-surface-selector-snapshot-reduction");
    }

    // Split on '\n' alone, not Environment.NewLine: the CLI writes snapshots with LF-only line
    // endings regardless of platform, so splitting on "\r\n" here would treat the whole file as a
    // single line on Windows.
    private static int CountSnapshotEntries(string snapshot) =>
        snapshot.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

    // Item 6 — a selected ValueObject-role type must remain governed by an ordinary role-based
    // contract, unchanged by selection. Money passes cleanly (BCL-only members); a temporary
    // first-party dependency proves the rule is genuinely engaged, not vacuously passing.
    private static CheckpointScenarioResult AssertSurfaceSelectorRolePreservation(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture fixture)
    {
        CommandResult clean = candidate.RunTool(fixture.Root,
            "--policy", fixture.PolicyPath, "--strict", "--format", "json", "--ensure-built",
            "--contract", "value-objects-depend-only-on-bcl");

        string moneyPath = Path.Combine(fixture.Root, "Domain", "Money.cs");
        string original = File.ReadAllText(moneyPath);
        CommandResult engaged;
        try
        {
            File.WriteAllText(moneyPath, original.Replace(
                "public string Format() => $\"{Amount} {Currency}\";",
                "public string Format() => $\"{Amount} {Currency}\";" + Environment.NewLine + Environment.NewLine
                + "    public InternalPricingEngine Engine() => new();"));
            fixture.Build(configuration: "Release", targetFramework: "net10.0");
            engaged = candidate.RunTool(fixture.Root,
                "--policy", fixture.PolicyPath, "--strict", "--format", "json", "--ensure-built",
                "--contract", "value-objects-depend-only-on-bcl");
        }
        finally
        {
            File.WriteAllText(moneyPath, original);
        }

        Assert.Multiple(() =>
        {
            Assert.That(clean.ExitCode, Is.EqualTo(0), clean.CombinedOutput);
            using JsonDocument cleanDocument = JsonDocument.Parse(clean.StandardOutput);
            Assert.That(cleanDocument.RootElement.GetProperty("violations").GetArrayLength(), Is.Zero);

            Assert.That(engaged.ExitCode, Is.EqualTo(1), engaged.CombinedOutput);
            Assert.That(engaged.StandardOutput, Does.Contain("Money"));
            Assert.That(engaged.StandardOutput, Does.Contain("InternalPricingEngine"),
                "The role-based rule must genuinely evaluate Money's resolved ValueObject role.");
        });
        return Passed("public-api-surface-selector-role-preservation");
    }

    // Items 7, 8 — the exact snapshot comparison must observe an added, a removed, and a changed
    // selected signature identically to the unselected #94 lifecycle, and `update` must restore a
    // clean comparison.
    private static CheckpointScenarioResult AssertSurfaceSelectorExactDeltaLifecycle(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture fixture)
    {
        string receiptPath = Path.Combine(fixture.Root, "Domain", "Receipt.cs");
        File.WriteAllText(receiptPath, """
            using Synthetic.ApiSurfaceSelector.Architecture;

            namespace Synthetic.ApiSurfaceSelector.Domain;

            [PublicApiContract]
            public sealed class Receipt
            {
                public string Added() => "added";

                public long Changed(int value) => value;
            }
            """);
        fixture.Build(configuration: "Release", targetFramework: "net10.0");

        CommandResult evolved = candidate.RunTool(fixture.Root,
            "public-api", "diff", "--policy", fixture.PolicyPath, "--contract", "marker-selected-api",
            "--snapshot", "public-api/marker-selected-api.txt", "--ensure-built", "--format", "json");

        using JsonDocument document = JsonDocument.Parse(evolved.StandardOutput);
        Dictionary<string, string> deltas = document.RootElement.GetProperty("violations").EnumerateArray()
            .Where(violation => violation.GetProperty("source").GetString() ==
                "Synthetic.ApiSurfaceSelector.Domain.Receipt")
            .ToDictionary(
                violation => violation.GetProperty("api_delta_kind").GetString() ?? string.Empty,
                violation => violation.GetProperty("undeclared_api_signature").GetString() ?? string.Empty,
                StringComparer.Ordinal);

        CommandResult resynchronize = candidate.RunTool(fixture.Root,
            "public-api", "update", "--policy", fixture.PolicyPath, "--contract", "marker-selected-api",
            "--snapshot", "public-api/marker-selected-api.txt", "--ensure-built", "--format", "json");
        CommandResult reviewed = candidate.RunTool(fixture.Root,
            "public-api", "diff", "--policy", fixture.PolicyPath, "--contract", "marker-selected-api",
            "--snapshot", "public-api/marker-selected-api.txt", "--ensure-built", "--format", "json");

        // Receipt is permanently evolved for the rest of the matrix (the escape/green-run scenarios
        // that follow need a stable fixture). assembly-wide-api also governs Receipt, so its
        // snapshot must be resynchronized too, exactly as a real consumer would resynchronize every
        // reviewed snapshot after an ordinary API evolution.
        CommandResult resynchronizeAssemblyWide = candidate.RunTool(fixture.Root,
            "public-api", "update", "--policy", fixture.PolicyPath, "--contract", "assembly-wide-api",
            "--snapshot", "public-api/assembly-wide-api.txt", "--ensure-built", "--format", "json");

        Assert.Multiple(() =>
        {
            Assert.That(evolved.ExitCode, Is.EqualTo(1), evolved.CombinedOutput);
            Assert.That(deltas.Keys, Is.EquivalentTo(_value),
                $"Every delta class must be observed on a selected type.{Environment.NewLine}{evolved.CombinedOutput}");
            Assert.That(deltas["added"],
                Is.EqualTo("method Synthetic.ApiSurfaceSelector.Domain.Receipt.Added(): System.String"));
            Assert.That(deltas["removed"],
                Is.EqualTo("method Synthetic.ApiSurfaceSelector.Domain.Receipt.Removed(): System.String"));
            Assert.That(deltas["changed"],
                Is.EqualTo("method Synthetic.ApiSurfaceSelector.Domain.Receipt.Changed(System.Int32): System.Int64"));
            Assert.That(resynchronize.ExitCode, Is.EqualTo(0), resynchronize.CombinedOutput);
            Assert.That(reviewed.ExitCode, Is.EqualTo(0), reviewed.CombinedOutput);
            Assert.That(JsonDocument.Parse(reviewed.StandardOutput).RootElement
                .GetProperty("violations").GetArrayLength(), Is.Zero,
                "`update` must bring the selected reviewed snapshot back in sync.");
            Assert.That(resynchronizeAssemblyWide.ExitCode, Is.EqualTo(0), resynchronizeAssemblyWide.CombinedOutput);
        });
        return Passed("public-api-surface-selector-exact-delta-lifecycle");
    }

    // Item 9 — adding or removing selector-matching evidence on a type must be a review-visible
    // snapshot delta in both directions, never silent.
    private static CheckpointScenarioResult AssertSurfaceSelectorMembershipReviewVisibility(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture fixture)
    {
        string policyPath = Path.Combine(fixture.Root, "Domain", "InternalDiscountPolicy.cs");
        string unselected = File.ReadAllText(policyPath);
        string selected = unselected.Replace(
            "public sealed class InternalDiscountPolicy",
            "[Synthetic.ApiSurfaceSelector.Architecture.PublicApiContract]" + Environment.NewLine
            + "public sealed class InternalDiscountPolicy");

        File.WriteAllText(policyPath, selected);
        fixture.Build(configuration: "Release", targetFramework: "net10.0");
        CommandResult entering = candidate.RunTool(fixture.Root,
            "public-api", "diff", "--policy", fixture.PolicyPath, "--contract", "marker-selected-api",
            "--snapshot", "public-api/marker-selected-api.txt", "--ensure-built", "--format", "json");
        CommandResult syncEntry = candidate.RunTool(fixture.Root,
            "public-api", "update", "--policy", fixture.PolicyPath, "--contract", "marker-selected-api",
            "--snapshot", "public-api/marker-selected-api.txt", "--ensure-built", "--format", "json");
        string snapshotWithEntry =
            File.ReadAllText(Path.Combine(fixture.Root, "public-api", "marker-selected-api.txt"));

        File.WriteAllText(policyPath, unselected);
        fixture.Build(configuration: "Release", targetFramework: "net10.0");
        CommandResult leaving = candidate.RunTool(fixture.Root,
            "public-api", "diff", "--policy", fixture.PolicyPath, "--contract", "marker-selected-api",
            "--snapshot", "public-api/marker-selected-api.txt", "--ensure-built", "--format", "json");
        CommandResult syncExit = candidate.RunTool(fixture.Root,
            "public-api", "update", "--policy", fixture.PolicyPath, "--contract", "marker-selected-api",
            "--snapshot", "public-api/marker-selected-api.txt", "--ensure-built", "--format", "json");
        string snapshotWithoutEntry =
            File.ReadAllText(Path.Combine(fixture.Root, "public-api", "marker-selected-api.txt"));

        Assert.Multiple(() =>
        {
            Assert.That(entering.ExitCode, Is.EqualTo(1), entering.CombinedOutput);
            Assert.That(entering.StandardOutput, Does.Contain("InternalDiscountPolicy"));
            Assert.That(syncEntry.ExitCode, Is.EqualTo(0), syncEntry.CombinedOutput);
            Assert.That(snapshotWithEntry, Does.Contain("Synthetic.ApiSurfaceSelector.Domain.InternalDiscountPolicy"));

            Assert.That(leaving.ExitCode, Is.EqualTo(1), leaving.CombinedOutput);
            Assert.That(leaving.StandardOutput, Does.Contain("InternalDiscountPolicy"));
            Assert.That(syncExit.ExitCode, Is.EqualTo(0), syncExit.CombinedOutput);
            Assert.That(snapshotWithoutEntry,
                Does.Not.Contain("Synthetic.ApiSurfaceSelector.Domain.InternalDiscountPolicy"));
        });
        return Passed("public-api-surface-selector-membership-review-visibility");
    }

    // Item 10 — a selected member referencing an unselected first-party exported type must fail
    // closed. escaping-selected-api is appended and reverted around this single call.
    private static CheckpointScenarioResult AssertSurfaceSelectorEscapeFailsClosed(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture fixture)
    {
        string originalPolicy = File.ReadAllText(fixture.PolicyPath);
        const string Anchor = "  strict_public_api_surface:";
        int anchorIndex = originalPolicy.IndexOf(Anchor, StringComparison.Ordinal);
        Assert.That(anchorIndex, Is.GreaterThanOrEqualTo(0), "The fixture policy must declare strict_public_api_surface.");
        string mutatedPolicy = originalPolicy.Insert(anchorIndex + Anchor.Length, "\n" + EscapingSelectedApiContract);

        CommandResult capture;
        try
        {
            File.WriteAllText(fixture.PolicyPath, mutatedPolicy);
            capture = candidate.RunTool(fixture.Root,
                "public-api", "capture", "--policy", fixture.PolicyPath, "--contract", "escaping-selected-api",
                "--output", "public-api/escaping-selected-api.txt", "--ensure-built", "--format", "json");
        }
        finally
        {
            File.WriteAllText(fixture.PolicyPath, originalPolicy);
        }

        Assert.Multiple(() =>
        {
            Assert.That(capture.ExitCode, Is.EqualTo(2), capture.CombinedOutput);
            Assert.That(capture.StandardOutput, Does.Contain("unselected"));
            Assert.That(capture.StandardOutput, Does.Contain("InternalPricingEngine"));
            Assert.That(File.Exists(Path.Combine(fixture.Root, "public-api", "escaping-selected-api.txt")), Is.False,
                "A fail-closed escape must never write a snapshot.");
        });
        return Passed("public-api-surface-selector-escape-fails-closed");
    }

    // Item 5 — the full policy, with every permanent selector contract and the role-based
    // governance contract in place, must validate green.
    private static CheckpointScenarioResult AssertSurfaceSelectorStrictRunIsGreen(
        CandidatePackageFeed candidate, AdoptionAcceptanceFixture fixture)
    {
        CommandResult strict = candidate.RunTool(fixture.Root,
            "--policy", fixture.PolicyPath, "--strict", "--format", "json", "--ensure-built");
        Assert.Multiple(() =>
        {
            Assert.That(strict.ExitCode, Is.EqualTo(0), strict.CombinedOutput);
            using JsonDocument document = JsonDocument.Parse(strict.StandardOutput);
            Assert.That(document.RootElement.GetProperty("violations").GetArrayLength(), Is.Zero,
                strict.CombinedOutput);
        });
        return Passed("public-api-surface-selector-strict-run-is-green");
    }
}
