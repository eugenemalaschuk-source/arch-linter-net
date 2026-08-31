using System.Text.Encodings.Web;
using System.Text.Json;
using ArchLinterNet.Core.Reporting;
using ArchLinterNet.Core.Validation;
using ArchLinterNet.Testing;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

/// <summary>
/// Proves the self-policy guards adopted by #464 actually fire, rather than only asserting that
/// the current tree happens to be green. Each case takes the real repository policy, applies one
/// exact mutation that should be caught, and runs the mutated copy from the same policy boundary
/// so every repository-relative input still resolves. The anchors are asserted to occur exactly
/// once, so a future policy edit that removes a guard breaks its regression instead of silently
/// turning it into a no-op.
/// </summary>
[TestFixture]
[Category("E2E")]
// Every case prepares and verifies the real project graph (`WithEnsureBuilt`), which the policy now
// requires: declaring analysis.solution brings each run under build-state preflight, and a build
// receipt does not outlive the process that created it. On an idle machine each case lands well
// inside the assembly-wide 15 s per-test limit, but an MSBuild pass on a loaded CI runner does not
// reliably, so this fixture takes the same explicit, reviewable exemption
// CheckpointBReleaseGateTests uses rather than being intermittently red.
[CancelAfter(120_000)]
public sealed class SelfPolicyNegativeRegressionTests
{
    private static readonly JsonSerializerOptions _renderedFindingJsonOptions =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private string _repositoryRoot = string.Empty;
    private string _policy = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _repositoryRoot = SelfPolicyRepository.FindRepositoryRoot();
        SelfPolicyRepository.DeleteMutations(_repositoryRoot);
        _policy = SelfPolicyRepository.ReadPolicy(_repositoryRoot);
    }

    [TearDown]
    public void TearDown() => SelfPolicyRepository.DeleteMutations(_repositoryRoot);

    // ── Direct product assembly graph ───────────────────────────────────────
    [Test]
    public void AdapterAssemblyAllowOnly_RejectsAnUnlistedDirectFirstPartyReference()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      source_sets: [adapter_assemblies]\n      allowed: [ArchLinterNet.Core]",
            "      source_sets: [adapter_assemblies]\n      allowed: [ArchLinterNet.CEL]");

        ArchitectureValidationResult result = ValidateMutated(mutated, "adapter-assemblies-reference-only-core");

        AssertFailedMentioning(result, "ArchLinterNet.Core");
    }

    // ── Project discovery and coverage ──────────────────────────────────────
    [Test]
    public void ProjectCoverage_RejectsANewlyDiscoveredProjectThatNoLayerCovers()
    {
        // Dropping the benchmarks exclusion is the closest reproduction of "a new project appears
        // under a governed root": it enters the discovered inventory without a declared layer.
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "  project_exclude:\n    - tests/**\n    - benchmarks/**\n",
            "  project_exclude:\n    - tests/**\n");

        ArchitectureValidationResult result = ValidateMutated(mutated, "project-coverage");

        AssertFailedMentioning(result, "Benchmarks");
    }

    // ── Friend assemblies and project references ────────────────────────────
    [Test]
    public void ProjectMetadata_RejectsAnUnreviewedFriendAssembly()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      allowed_friend_assemblies:\n        - ArchLinterNet.Core.Tests\n        - ArchLinterNet.Cli\n",
            "      allowed_friend_assemblies:\n        - ArchLinterNet.Core.Tests\n");

        ArchitectureValidationResult result = ValidateMutated(mutated, "core-friend-assemblies-are-reviewed");

        AssertFailedMentioning(result, "ArchLinterNet.Cli");
    }

    [Test]
    public void ProjectMetadata_RejectsAForbiddenProjectReference()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      forbidden_project_references:\n        - tests/**/*.csproj\n        - benchmarks/**/*.csproj\n",
            "      forbidden_project_references:\n        - src/**/*.csproj\n");

        ArchitectureValidationResult result = ValidateMutated(
            mutated,
            "production-projects-must-not-reference-test-or-benchmark-projects");

        AssertFailedMentioning(result, ".csproj");
    }

    // ── Package and FrameworkReference declarations ─────────────────────────
    [Test]
    public void PackageAllowOnly_RejectsAnUndeclaredPackageReference()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      source: ArchLinterNet.CEL\n      allowed: []",
            "      source: ArchLinterNet.Core\n      allowed: []");

        ArchitectureValidationResult result = ValidateMutated(mutated, "cel-declares-no-package-dependencies");

        AssertFailedMentioning(result, "YamlDotNet");
    }

    [Test]
    public void FrameworkAllowOnly_RejectsAnUndeclaredFrameworkReference()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      allowed: [base_runtime_framework]",
            "      allowed: []");

        ArchitectureValidationResult result = ValidateMutated(
            mutated,
            "shipped-projects-declare-only-the-base-runtime-framework");

        AssertFailedMentioning(result, "Microsoft.NETCore.App");
    }

    // ── Post-#451/#452/#453 structural seams ────────────────────────────────
    [Test]
    public void TypePlacement_RejectsAFamilyCheckerOutsideTheExtractedCheckerSeam()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      must_reside_in_namespaces: [ArchLinterNet.Core.Execution.Checkers]",
            "      must_reside_in_namespaces: [ArchLinterNet.Core.Reporting]");

        ArchitectureValidationResult result = ValidateMutated(
            mutated,
            "family-checkers-stay-in-the-extracted-checker-seam");

        AssertFailedMentioning(result, "Checker");
    }

    [Test]
    public void InterfaceImplementation_RejectsADiagnosticPayloadOutsideTheModelLayer()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      interfaces: [ArchLinterNet.Core.Model.IArchitectureDiagnosticPayload]\n      allowed_only_in_layers: [core_model]",
            "      interfaces: [ArchLinterNet.Core.Model.IArchitectureDiagnosticPayload]\n      allowed_only_in_layers: [core_reporting]");

        ArchitectureValidationResult result = ValidateMutated(
            mutated,
            "diagnostic-detail-payloads-stay-in-the-model-layer");

        AssertFailedMentioning(result, "Payload");
    }

    // ── CLI command module boundaries ───────────────────────────────────────
    [Test]
    public void ModuleContainerProfile_RejectsALayoutThatReinterpretsCommandModulesAsNestedSegments()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      container: ArchLinterNet.Cli.Commands\n      profile: cli_command",
            "      container: ArchLinterNet.Cli\n      profile: cli_command");

        ArchitectureValidationResult result = ValidateMutated(mutated, "cli-command-modules-follow-the-feature-profile");

        AssertFailedMentioning(result, "<module-root:Abstractions>");
    }

    // ── Recursive folder-purity and leaf conventions ────────────────────────
    [Test]
    public void AbstractionsPurity_RejectsInterfacesWhenTheAllowedKindIsMutatedAway()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "        allowed_type_kinds: [interface, class]\n        require_abstract_classes: true",
            "        allowed_type_kinds: [record]\n        require_abstract_classes: true");

        ArchitectureValidationResult result = ValidateMutated(
            mutated,
            "abstractions-directories-contain-only-interfaces-or-abstract-classes");

        AssertFailedMentioning(result, "Abstractions");
    }

    [Test]
    public void ExceptionsPurity_RejectsExceptionTypesWhenTheAllowedRoleIsMutatedAway()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "        allowed_roles: [Exception]",
            "        allowed_roles: [Model]");

        ArchitectureValidationResult result = ValidateMutated(
            mutated,
            "exceptions-directories-contain-only-exception-classes");

        AssertFailedMentioning(result, "Exception");
    }

    // ── #742 partial-declaration debt ratchet ───────────────────────────────
    [Test]
    public void PartialDeclarationRatchet_RejectsAnAggregateExceedingItsReviewedCount()
    {
        // The reviewed exception freezes ArchitectureDiagnosticFormatter's declaration count at its
        // exact current value (19) via an exact-text ignored_violations entry. Lowering the frozen
        // count by one reproduces "the aggregate grew past what was reviewed" without touching real
        // source files: the live finding still says "found 19", the mutated entry now says "found
        // 18", the exact-text match fails, and the (unignored) violation surfaces.
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "found 19: src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.Applicability.cs",
            "found 18: src/ArchLinterNet.Core/Reporting/ArchitectureDiagnosticFormatter.Applicability.cs");

        ArchitectureValidationResult result = ValidateMutated(
            mutated, "production-partial-type-declaration-count-does-not-increase");

        AssertFailedMentioning(result, "ArchitectureDiagnosticFormatter");
    }

    [Test]
    public void ModelsLeafRule_RejectsCliDependenciesWhenTheRuleIsPointedAtTheCliSourceSet()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      id: models-have-no-first-party-dependencies\n      source: model_types\n      allowed: []",
            "      id: models-have-no-first-party-dependencies\n      source: cli\n      allowed: []");

        ArchitectureValidationResult result = ValidateMutated(mutated, "models-have-no-first-party-dependencies");

        AssertFailedMentioning(result, "ArchLinterNet.Core");
    }

    [Test]
    public void ExceptionsLeafRule_RejectsCliDependenciesWhenTheRuleIsPointedAtTheCliSourceSet()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      id: exceptions-have-no-first-party-dependencies\n      source: exception_types\n      allowed: []",
            "      id: exceptions-have-no-first-party-dependencies\n      source: cli\n      allowed: []");

        ArchitectureValidationResult result = ValidateMutated(mutated, "exceptions-have-no-first-party-dependencies");

        AssertFailedMentioning(result, "ArchLinterNet.Core");
    }

    // ── Reviewed public API lifecycle ───────────────────────────────────────
    [Test]
    public void PublicApiSurface_RejectsAnUnreviewedAdditionWithoutRewritingTheSnapshot()
    {
        string reviewedPath = Path.Combine(
            _repositoryRoot, "architecture", "api", "ArchLinterNet.Testing.public-api.txt");
        string reviewed = File.ReadAllText(reviewedPath);
        byte[] reviewedBytesBefore = File.ReadAllBytes(reviewedPath);

        // Dropping one reviewed entry makes the live surface carry an undeclared member.
        string[] lines = reviewed.ReplaceLineEndings("\n").Split('\n');
        int dropIndex = Array.FindIndex(lines, line => line.StartsWith("class ", StringComparison.Ordinal));
        Assert.That(dropIndex, Is.GreaterThanOrEqualTo(0), "Expected at least one class entry in the reviewed snapshot.");
        string drifted = string.Join('\n', lines.Where((_, index) => index != dropIndex));

        string snapshotPath = SelfPolicyRepository.WriteMutatedSnapshot(_repositoryRoot, drifted);
        byte[] driftedBytesBefore = File.ReadAllBytes(snapshotPath);

        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      api_snapshot: architecture/api/ArchLinterNet.Testing.public-api.txt",
            $"      api_snapshot: {SelfPolicyRepository.RelativePolicyPath(_repositoryRoot, snapshotPath)}");

        ArchitectureValidationResult result = ValidateMutated(mutated, "testing-public-api-is-reviewed");

        Assert.That(result.Passed, Is.False, "An undeclared exported member must fail the read-only gate.");
        Assert.That(File.ReadAllBytes(snapshotPath), Is.EqualTo(driftedBytesBefore),
            "A read-only validation run must never rewrite the snapshot it compares against.");
        Assert.That(File.ReadAllBytes(reviewedPath), Is.EqualTo(reviewedBytesBefore),
            "A read-only validation run must never rewrite a reviewed snapshot.");
    }

    [Test]
    public void PublicApiSurface_RejectsARemovedMemberUnderExactComparison()
    {
        string reviewed = File.ReadAllText(Path.Combine(
            _repositoryRoot, "architecture", "api", "ArchLinterNet.Testing.public-api.txt"));

        // A declared entry that no longer exists is only reported because api_comparison is exact.
        string drifted = reviewed.ReplaceLineEndings("\n").TrimEnd('\n')
                         + "\nclass ArchLinterNet.Testing.RetiredAdapterType [sealed]\n";

        string snapshotPath = SelfPolicyRepository.WriteMutatedSnapshot(_repositoryRoot, drifted);
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      api_snapshot: architecture/api/ArchLinterNet.Testing.public-api.txt",
            $"      api_snapshot: {SelfPolicyRepository.RelativePolicyPath(_repositoryRoot, snapshotPath)}");

        ArchitectureValidationResult result = ValidateMutated(mutated, "testing-public-api-is-reviewed");

        AssertFailedMentioning(result, "RetiredAdapterType");
    }

    // ── Fast policy-only gate (`make policy-check`) ─────────────────────────
    [Test]
    public void PolicyCheck_RejectsAStaleSourceSetMember()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "    members:\n      - ArchLinterNet.Cli\n      - ArchLinterNet.Testing\n",
            "    members:\n      - ArchLinterNet.Cli\n      - ArchLinterNet.Retired\n");

        string rejection = CheckMutatedRejection(mutated);

        Assert.That(rejection, Does.Contain("ArchLinterNet.Retired"));
    }

    [Test]
    public void PolicyCheck_RejectsARuleInputCoverageIdThatNoLongerResolves()
    {
        string mutated = SelfPolicyRepository.Replace(
            _policy,
            "      id: cel-must-not-depend-on-core\n",
            "      id: cel-must-not-depend-on-core-renamed\n");

        string rejection = CheckMutatedRejection(mutated);

        Assert.That(rejection, Does.Contain("cel-must-not-depend-on-core"));
    }

    [Test]
    public void PolicyCheck_RejectsAMalformedPolicyDocument()
    {
        string path = SelfPolicyRepository.WriteMutatedPolicy(
            _repositoryRoot,
            "version: 1\nname: Broken\nlayers:\n  core: [unterminated\n");

        bool rejected;
        try
        {
            rejected = !ArchitectureAssertions.CheckPolicy(path).IsValid;
        }
        catch (Exception)
        {
            rejected = true;
        }

        Assert.That(rejected, Is.True, "A malformed policy must fail the fast policy-only gate.");
    }

    // ── Mutation-harness determinism ────────────────────────────────────────
    // `.gitattributes` pins only `schema/*.json` to LF, so the policy is checked out CRLF on
    // Windows. Every multi-line anchor above is a C# literal with \n, so without normalization the
    // whole fixture fails on Windows before reaching a single contract.
    [Test]
    public void MutationAnchors_MatchRegardlessOfCheckoutLineEndings()
    {
        const string Anchor = "  project_exclude:\n    - tests/**\n    - benchmarks/**\n";

        const string Replacement = "  project_exclude:\n    - tests/**\n";

        string crlfPolicy = _policy.ReplaceLineEndings("\r\n");
        Assert.That(crlfPolicy, Does.Not.Contain(Anchor),
            "This case is only meaningful while the anchor is genuinely multi-line.");

        string mutated = SelfPolicyRepository.Replace(crlfPolicy, Anchor, Replacement);

        Assert.Multiple(() =>
        {
            Assert.That(mutated, Does.Not.Contain(Anchor), "The anchored block must be gone.");
            Assert.That(mutated, Does.Contain(Replacement), "The replacement must be present.");
        });
    }

    [Test]
    public void ReadPolicy_NormalizesLineEndings()
    {
        Assert.That(SelfPolicyRepository.ReadPolicy(_repositoryRoot), Does.Not.Contain("\r"));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────
    private ArchitectureValidationResult ValidateMutated(string mutatedPolicy, params string[] contractIds)
    {
        string path = SelfPolicyRepository.WriteMutatedPolicy(_repositoryRoot, mutatedPolicy);
        return ArchitectureAssertions
            .FromPolicy(path)
            .WithContracts(contractIds)
            .WithEnsureBuilt()
            .ValidateStrict();
    }

    /// <summary>
    /// Runs the fast policy-only gate and returns its rejection text. A policy-load defect surfaces
    /// either as a typed <see cref="PolicyCheckFailure"/> or as a load exception, depending on which
    /// preparation stage rejects it; both are rejections of the mutated policy.
    /// </summary>
    private string CheckMutatedRejection(string mutatedPolicy)
    {
        string path = SelfPolicyRepository.WriteMutatedPolicy(_repositoryRoot, mutatedPolicy);

        PolicyCheckOutcome outcome;
        try
        {
            outcome = ArchitectureAssertions.CheckPolicy(path);
        }
        catch (Exception exception)
        {
            return exception.Message;
        }

        Assert.That(outcome.IsValid, Is.False, "The mutated policy must fail the fast policy-only gate.");
        return outcome.Failure!.Message;
    }

    /// <summary>
    /// Asserts the mutated policy failed and that the expected evidence appears in the structured
    /// finding projection. The normalized JSON projection is used rather than
    /// <c>Details.ToString()</c>, because a record's generated ToString renders collection-valued
    /// evidence as its type name and would hide exactly the fields these regressions check.
    /// </summary>
    private static void AssertFailedMentioning(ArchitectureValidationResult result, string expectedEvidence)
    {
        Assert.That(result.Passed, Is.False, "The mutated policy must fail strict validation.");

        string rendered = string.Join(
            "\n",
            result.Findings.Select(finding => JsonSerializer.Serialize(
                ArchitectureDiagnosticFormatter.FormatNormalizedFindingForJson(finding),
                _renderedFindingJsonOptions)));
        Assert.That(rendered, Does.Contain(expectedEvidence));
    }
}
