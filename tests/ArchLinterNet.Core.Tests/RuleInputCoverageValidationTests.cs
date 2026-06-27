using ArchLinterNet.Core.Validation;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public sealed class RuleInputCoverageValidationTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arch-linter-rule-input-coverage-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private string WritePolicy(string yaml)
    {
        string path = Path.Combine(_tempDir, "dependencies.arch.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private const string FixtureRoot = "ArchLinterNet.Core.Tests.RuleInputCoverageFixtures";

    private string AssemblyName => typeof(RuleInputCoverageValidationTests).Assembly.GetName().Name!;

    private string BuildPolicy(
        string coverageGroup,
        string referencedRuleGroup,
        string? analysisCoverage = null,
        string extraExclude = "",
        string extraContracts = "")
    {
        string coverageSetting = analysisCoverage is null
            ? string.Empty
            : $"  coverage: {analysisCoverage}{Environment.NewLine}";

        return $"version: 1{Environment.NewLine}" +
               $"name: Test{Environment.NewLine}{Environment.NewLine}" +
               $"layers:{Environment.NewLine}" +
               $"  audio:{Environment.NewLine}" +
               $"    namespace: {FixtureRoot}.Audio{Environment.NewLine}" +
               $"  video:{Environment.NewLine}" +
               $"    namespace: {FixtureRoot}.Video{Environment.NewLine}" +
               $"  ghost:{Environment.NewLine}" +
               $"    namespace: {FixtureRoot}.Ghost{Environment.NewLine}{Environment.NewLine}" +
               $"analysis:{Environment.NewLine}" +
               $"  target_assemblies: [{AssemblyName}]{Environment.NewLine}" +
               coverageSetting +
               $"contracts:{Environment.NewLine}" +
               $"  {referencedRuleGroup}:{Environment.NewLine}" +
               $"    - id: video-to-ghost-rule{Environment.NewLine}" +
               $"      name: video-to-ghost-rule{Environment.NewLine}" +
               $"      source: video{Environment.NewLine}" +
               $"      forbidden: [ghost]{Environment.NewLine}" +
               $"      reason: Video must not depend on ghost.{Environment.NewLine}" +
               extraContracts +
               $"  {coverageGroup}:{Environment.NewLine}" +
               $"    - id: rule-input-coverage{Environment.NewLine}" +
               $"      name: rule-input-coverage{Environment.NewLine}" +
               $"      scope: rule_input{Environment.NewLine}" +
               $"      contract_ids: [video-to-ghost-rule]{Environment.NewLine}" +
               extraExclude +
               $"      reason: Flag if referenced rules stop matching any code.{Environment.NewLine}";
    }

    [Test]
    public void StrictRuleInputCoverage_SameModeDefaultSeverity_FailsViaCoverageOnly()
    {
        // Referenced rule is in the SAME group ('strict') as the validated mode. Before the fix,
        // the pre-existing, unconditional CheckConfiguration "empty layer namespace" check would
        // also fire on this exact gap regardless of analysis.coverage, making rule_input
        // severity/exclusion meaningless for the dominant same-mode case. Asserting Violations is
        // empty proves CheckConfiguration now defers to the coverage contract that tracks this ID.
        string policyPath = WritePolicy(BuildPolicy("strict_coverage", referencedRuleGroup: "strict"));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations, Is.Empty);
        Assert.That(outcome.CoverageFindings, Has.Count.EqualTo(1));
        Assert.That(outcome.CoverageFindings.Single().ForbiddenNamespace, Is.EqualTo("empty-input"));
    }

    [Test]
    public void AuditRuleInputCoverage_SameModeWarnSeverity_ReportsWithoutFailing()
    {
        string policyPath = WritePolicy(
            BuildPolicy("audit_coverage", referencedRuleGroup: "audit", analysisCoverage: "warn"));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "audit"
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.Violations, Is.Empty);
        Assert.That(outcome.CoverageConfig, Is.EqualTo("warn"));
        Assert.That(outcome.CoverageFindings, Has.Count.EqualTo(1));
    }

    [Test]
    public void StrictRuleInputCoverage_SameModeOffSeverity_SuppressesFindingAndPasses()
    {
        string policyPath = WritePolicy(
            BuildPolicy("strict_coverage", referencedRuleGroup: "strict", analysisCoverage: "off"));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.Violations, Is.Empty);
        Assert.That(outcome.CoverageFindings, Is.Empty);
    }

    [Test]
    public void StrictRuleInputCoverage_OffSeverityWithStaleBaselineEntry_StillPasses()
    {
        // The coverage gate is off, but a strict_coverage contract still carries an
        // ignored_violations entry that no longer matches any current finding (resolved coverage
        // debt). Stale-entry reporting must follow analysis.coverage, not just
        // analysis.unmatched_ignored_violations — otherwise turning coverage off would not fully
        // disable the coverage family.
        // Keyed on a layer name that the current "video-to-ghost-rule" finding never reports
        // (the real finding's forbidden_reference is "ghost"), so this entry never matches and is
        // genuinely stale rather than coincidentally suppressing the live finding.
        string staleIgnore =
            $"      ignored_violations:{Environment.NewLine}" +
            $"        - source_type: video-to-ghost-rule{Environment.NewLine}" +
            $"          forbidden_reference: already-resolved-layer{Environment.NewLine}" +
            $"          reason: previously accepted{Environment.NewLine}";

        string policyPath = WritePolicy(BuildPolicy(
            "strict_coverage",
            referencedRuleGroup: "strict",
            analysisCoverage: "off",
            extraExclude: staleIgnore));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict",
            EnforceUnmatchedIgnoredViolationsPolicy = true
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.CoverageFindings, Is.Empty);
        Assert.That(outcome.UnmatchedIgnoredViolations, Is.Empty);
    }

    [Test]
    public void RuleInputCoverage_SameModeIntentionallyEmptyExclusion_SuppressesFindingAndPasses()
    {
        string extraExclude =
            $"      exclude:{Environment.NewLine}" +
            $"        - contract_id: video-to-ghost-rule{Environment.NewLine}" +
            $"          reason: Ghost layer is intentionally unused for now.{Environment.NewLine}";

        string policyPath = WritePolicy(
            BuildPolicy("strict_coverage", referencedRuleGroup: "strict", extraExclude: extraExclude));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.True);
        Assert.That(outcome.Violations, Is.Empty);
        Assert.That(outcome.CoverageFindings, Is.Empty);
    }

    [Test]
    public void RuleInputCoverage_UnreferencedRuleWithEmptyLayer_StillFailsViaConfigurationCheck()
    {
        // A second strict rule that targets the same empty 'ghost' layer but is NOT listed in
        // contract_ids must still be caught by the pre-existing, unconditional CheckConfiguration
        // check — only contracts a rule_input coverage contract explicitly tracks defer to it.
        string extraContracts =
            $"    - id: audio-to-ghost-rule{Environment.NewLine}" +
            $"      name: audio-to-ghost-rule{Environment.NewLine}" +
            $"      source: audio{Environment.NewLine}" +
            $"      forbidden: [ghost]{Environment.NewLine}" +
            $"      reason: Audio must not depend on ghost.{Environment.NewLine}";

        string policyPath = WritePolicy(
            BuildPolicy("strict_coverage", referencedRuleGroup: "strict", extraContracts: extraContracts));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations.Select(v => v.ForbiddenNamespace), Has.Member("empty layer namespace"));
        Assert.That(outcome.CoverageFindings, Has.Count.EqualTo(1));
    }

    private string BuildDanglingLayerPolicy(string coverageGroup, string referencedRuleGroup)
    {
        return $"version: 1{Environment.NewLine}" +
               $"name: Test{Environment.NewLine}{Environment.NewLine}" +
               $"layers:{Environment.NewLine}" +
               $"  audio:{Environment.NewLine}" +
               $"    namespace: {FixtureRoot}.Audio{Environment.NewLine}{Environment.NewLine}" +
               $"analysis:{Environment.NewLine}" +
               $"  target_assemblies: [{AssemblyName}]{Environment.NewLine}" +
               $"contracts:{Environment.NewLine}" +
               $"  {referencedRuleGroup}:{Environment.NewLine}" +
               $"    - id: audio-to-typo-rule{Environment.NewLine}" +
               $"      name: audio-to-typo-rule{Environment.NewLine}" +
               $"      source: audio{Environment.NewLine}" +
               $"      forbidden: [does_not_exist_layer]{Environment.NewLine}" +
               $"      reason: Audio must not depend on a typo'd layer.{Environment.NewLine}" +
               $"  {coverageGroup}:{Environment.NewLine}" +
               $"    - id: rule-input-coverage{Environment.NewLine}" +
               $"      name: rule-input-coverage{Environment.NewLine}" +
               $"      scope: rule_input{Environment.NewLine}" +
               $"      contract_ids: [audio-to-typo-rule]{Environment.NewLine}" +
               $"      reason: Flag dangling layer references.{Environment.NewLine}";
    }

    [Test]
    public void StrictRuleInputCoverage_SameModeDanglingLayer_ReportsUnresolvedWithoutThrowing()
    {
        // Before the fix, CheckConfiguration and CheckContract both resolved every referenced
        // layer name unconditionally — a dangling layer name threw before any coverage contract
        // ever ran, making scope: rule_input's 'unresolved' finding unreachable through the real
        // Validate() pipeline (only reachable by calling CheckCoverageContract directly in a unit
        // test). Asserting this does not throw, and reports 'unresolved' instead, proves the
        // deferral now also covers dangling layer references, not just empty ones.
        string policyPath = WritePolicy(BuildDanglingLayerPolicy("strict_coverage", referencedRuleGroup: "strict"));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations, Is.Empty);
        Assert.That(outcome.CoverageFindings, Has.Count.EqualTo(1));
        Assert.That(outcome.CoverageFindings.Single().ForbiddenNamespace, Is.EqualTo("unresolved"));
        Assert.That(outcome.CoverageFindings.Single().ForbiddenReferences, Is.EqualTo(new[] { "does_not_exist_layer" }));
    }

    [Test]
    public void RuleInputCoverage_DanglingLayerCoveredOnlyByAuditCoverage_StillThrows()
    {
        // Mirrors the mode-mismatch regression for empty-input: an audit_coverage contract
        // tracking a strict-group rule never executes when validating in strict mode, so the
        // dangling layer reference must still surface as the original hard failure rather than
        // silently passing.
        string policyPath = WritePolicy(BuildDanglingLayerPolicy("audit_coverage", referencedRuleGroup: "strict"));

        Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }));
    }

    [Test]
    public void RuleInputCoverage_UnreferencedDanglingLayer_StillThrows()
    {
        // A dangling layer reference on a contract that no rule_input coverage contract tracks at
        // all must keep the pre-existing hard-failure behavior.
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict:
                - id: untracked-typo-rule
                  name: untracked-typo-rule
                  source: ArchLinterNet.Core
                  forbidden: [does_not_exist_layer]
                  reason: Placeholder with a dangling forbidden layer.
            """);

        Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }));
    }

    [Test]
    public void RuleInputCoverage_StrictRuleCoveredOnlyByAuditCoverage_StillFailsViaConfigurationCheck()
    {
        // The referenced rule lives in 'strict' but the rule_input coverage contract that tracks
        // it lives in 'audit_coverage'. ArchitectureContractExecutor only runs the coverage group
        // matching the current mode, so when validating in strict mode this audit_coverage
        // contract never executes and produces no finding. CheckConfiguration must NOT defer to
        // a coverage contract that won't run this request — otherwise the empty layer goes
        // unreported by both mechanisms (false green).
        string policyPath = WritePolicy(BuildPolicy("audit_coverage", referencedRuleGroup: "strict"));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict"
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations.Select(v => v.ForbiddenNamespace), Has.Member("empty layer namespace"));
        Assert.That(outcome.CoverageFindings, Is.Empty);
    }

    [Test]
    public void RuleInputCoverage_CoverageContractNotSelected_StillFailsViaConfigurationCheck()
    {
        // When --contract-id selection excludes the rule_input coverage contract itself, that
        // coverage contract's own IsContractSelected guard makes it produce no finding for this
        // request. CheckConfiguration must not defer to an unselected coverage contract either,
        // for the same false-green reason as the mode-mismatch case above.
        string policyPath = WritePolicy(BuildPolicy("strict_coverage", referencedRuleGroup: "strict"));

        ValidationOutcome outcome = ArchitectureValidationService.Validate(new ValidationRequest
        {
            PolicyPath = policyPath,
            Mode = "strict",
            ContractIds = new[] { "video-to-ghost-rule" }
        });

        Assert.That(outcome.Passed, Is.False);
        Assert.That(outcome.Violations.Select(v => v.ForbiddenNamespace), Has.Member("empty layer namespace"));
        Assert.That(outcome.CoverageFindings, Is.Empty);
    }

    [Test]
    public void RuleInputCoverage_EmptyContractIds_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_coverage:
                - name: rule-input-coverage
                  scope: rule_input
                  contract_ids: []
                  reason: Invalid rule-input coverage contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("must declare at least one entry in 'contract_ids'"));
    }

    [Test]
    public void RuleInputCoverage_WithRoots_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict:
                - id: some-rule
                  name: some-rule
                  source: ArchLinterNet.Core
                  forbidden: []
                  reason: Placeholder.
              strict_coverage:
                - name: rule-input-coverage
                  scope: rule_input
                  contract_ids: [some-rule]
                  roots:
                    - namespace: ArchLinterNet.Core
                  reason: Invalid rule-input coverage contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("cannot declare 'roots'"));
    }

    [Test]
    public void RuleInputCoverage_DanglingContractId_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_coverage:
                - name: rule-input-coverage
                  scope: rule_input
                  contract_ids: [does-not-exist]
                  reason: Invalid rule-input coverage contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("unknown contract ID 'does-not-exist'"));
    }

    [Test]
    public void RuleInputCoverage_AsmdefContractId_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_asmdef:
                - id: editor-asmdef-rule
                  name: editor-asmdef-rule
                  source_assemblies: [MyApp.Editor]
                  forbidden_editor_refs: true
                  reason: Placeholder.
              strict_coverage:
                - name: rule-input-coverage
                  scope: rule_input
                  contract_ids: [editor-asmdef-rule]
                  reason: Invalid rule-input coverage contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("unknown contract ID 'editor-asmdef-rule'"));
    }

    [Test]
    public void RuleInputCoverage_AcyclicSiblingContractId_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_acyclic_siblings:
                - id: feature-siblings-rule
                  name: feature-siblings-rule
                  ancestors: [MyApp.Features]
                  reason: Placeholder.
              strict_coverage:
                - name: rule-input-coverage
                  scope: rule_input
                  contract_ids: [feature-siblings-rule]
                  reason: Invalid rule-input coverage contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("unknown contract ID 'feature-siblings-rule'"));
    }

    [Test]
    public void RuleInputCoverage_LayerTemplateContractId_ThrowsActionableError()
    {
        string policyPath = WritePolicy("""
            version: 1
            name: Test

            analysis:
              target_assemblies: [ArchLinterNet.Core]

            contracts:
              strict_layer_templates:
                - id: feature-template
                  name: feature-template
                  containers: [MyApp.Features.Billing]
                  layers:
                    - name: Contracts
                  reason: Placeholder.
              strict_coverage:
                - name: rule-input-coverage
                  scope: rule_input
                  contract_ids: [feature-template]
                  reason: Invalid rule-input coverage contract.
            """);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("unknown contract ID 'feature-template'"));
    }

    [Test]
    public void RuleInputCoverage_ExclusionWithoutReason_ThrowsActionableError()
    {
        string extraExclude =
            $"      exclude:{Environment.NewLine}" +
            $"        - contract_id: video-to-ghost-rule{Environment.NewLine}";

        string policyPath = WritePolicy(
            BuildPolicy("strict_coverage", referencedRuleGroup: "strict", extraExclude: extraExclude));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("without a non-empty reason"));
    }

    [Test]
    public void RuleInputCoverage_ExclusionWithoutContractId_ThrowsActionableError()
    {
        string extraExclude =
            $"      exclude:{Environment.NewLine}" +
            $"        - reason: Missing contract_id matcher.{Environment.NewLine}";

        string policyPath = WritePolicy(
            BuildPolicy("strict_coverage", referencedRuleGroup: "strict", extraExclude: extraExclude));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            ArchitectureValidationService.Validate(new ValidationRequest
            {
                PolicyPath = policyPath,
                Mode = "strict"
            }))!;

        Assert.That(ex.Message, Does.Contain("without a 'contract_id' matcher"));
    }
}
