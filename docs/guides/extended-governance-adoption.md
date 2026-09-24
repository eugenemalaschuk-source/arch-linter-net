# Adopt extended architecture governance

Use this guide to add selected governance capabilities to an existing strict
check. For a new repository, start with [Getting Started](../getting-started/index.md).
For commands that produce a review report, use the [single-tool workflow](single-tool-workflow.md).
This page owns migration choices, not another copy of that command sequence.

## 1. Upgrade the tool before changing policy

Follow [the package upgrade procedure](upgrading.md#upgrade-an-existing-policy).
First prove the existing policy still behaves as expected, then review new
findings. Do not combine a tool upgrade with automatic debt acceptance.

## 2. Keep v1 compatibility until waiver migration is ready

Existing policy v1 keeps compatibility defaults for manual ignores. New policies
should use v2. Inspect still-needed exceptions, add their stable ID, exact
fingerprint, owner, reason, tracking reference and dates, and remove obsolete
ones before enabling strict lifecycle behavior.

A reviewed temporary `analysis.waiver_lifecycle_profile: compatibility` override
is available for v2 migration. It does not remove the debt. See
[structured waivers](../policy-format/structured-waivers.md).

## 3. Keep baseline finding debt separate

A finding baseline accepts specific known findings; a waiver is a policy
exception. Both can affect Health but are not interchangeable. `gate` and
`health` require an explicit baseline; when no finding debt is accepted, the
[report recipe](single-tool-workflow.md) creates an explicit empty artifact.
Relative metric budgets need their own reviewed scalar entries even then.

## 4. Add topology in partial mode first

[Capture observations](topology-review-workflow.md), review component mappings
and allowed edges, then declare the map. Use exhaustive mode only when the
bounded scope is fully reviewed. Capture output is not an approved policy.

## 5. Add visible contract-surface governance deliberately

[Exposure contracts](../contracts/contract-surface-exposure.md) catch forbidden
types in recursively visible signatures. Reuse reviewed API membership where
available. [Version isolation](../contracts/versioned-contract-surface-isolation.md)
adds local surface groupings; neither mechanism changes a type's semantic role.
Audit first when leakage is expected, and configure the audit CI step's failure
policy explicitly. Audit mode alone does not guarantee exit 0.

## 6. Measure before introducing budgets

Declare and inspect [metrics](../policy-format/architecture-metrics.md) before
choosing absolute bounds or baseline-relative ratchets. Missing required scope
is not a small value. [Repository metrics](../reference/repository-metrics.md)
are informational and must not be confused with enforcing metric budgets.

## 7. Bind external SARIF only when freshness evidence is explicit

Keep the analyzer's producer step. Use [SARIF integration](sarif-integration.md)
when its actual repository, revision and scope are known. Choose trust-only
checking or diagnostic import deliberately; no `diagnostic_filter` means no
imported diagnostics. Never manufacture producer context from the consuming
job's current SHA.

## 8. Add policy weakening and architecture change evidence

Use the [exact base/current workflow](single-tool-workflow.md). Two snapshot
filenames do not identify two revisions; each must come from its own state.
Changing package, mode or build selectors halfway through invalidates comparison.

## 9. Adopt Architecture Health

Health evaluates the configured evidence and reports Gate separately from
Health. A passing gate may retain accepted debt; missing required evidence is
unassessable. Preserve nonzero exits while keeping valid JSON for review. See
[the Health reference](../reference/architecture-health.md).

## 10. Replace repository-owned reporting logic

Render `report pr` and `badge architecture-health` from canonical artifacts,
not hand-counted findings. On supporting CLI versions, Health also writes the
current snapshot with `--change-snapshot`. Keep only the transport checks in
publication code. [CI integration](ci-integration.md) explains that boundary.

## 11. Keep PR authority and main responsibilities separate

Do not repeat full analysis after merge just to refresh a badge. Promote verified
PR evidence, or select another explicit lifecycle such as nightly analysis and
label its revision honestly. The upstream repository's Sonar/Codecov/package
jobs are an [example implementation](../reference/repository-ci.md), not adoption
prerequisites.

## 12. Understand `main.N` correctly

Development builds are not stable releases or release candidates. Use a preview
only for deliberate early adoption. See [versioning](../reference/versioning-and-releases.md)
for package meaning and [release provenance](release-provenance-verification.md)
for verification.

## 13. Documentation publication behavior

The upstream site is published by the release workflow, not every merge to
`main`. Consult the installed CLI for available options; a page on the development
branch may be newer than a stable package. This does not authorize deploying
Pages or declaring unreleased behavior stable.

## Migration completion checklist

The migration is complete when the selected policy, debt and scope choices have
been reviewed, required evidence is available for the real consumer, and the
normal CI gate and reports work with the pinned package. Verify both a clean
case and a deliberate violation. A documentation build alone does not prove
that an unexecuted consumer or hosted integration passed.
