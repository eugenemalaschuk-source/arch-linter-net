# Structured waivers

Structured waivers are explicit, reviewable exceptions to architecture findings. They live in `ignored_violations`, but unlike legacy matcher-only ignores they bind to one exact normalized finding identity and carry lifecycle metadata.

They are not the same thing as migration baseline debt and they are not the same thing as deliberate scope exclusions.

## Policy versions and lifecycle defaults

The policy schema supports `version: 1` and `version: 2`:

- `version: 1` keeps compatibility waiver behavior by default so existing policies continue to work;
- `version: 2` defaults to strict waiver lifecycle governance.

A version-2 migration can temporarily opt back into compatibility behavior explicitly:

```yaml
version: 2
name: Example architecture

analysis:
  waiver_lifecycle_profile: compatibility
```

Treat that as a reviewed migration state, not as the target end state. Remove the override once all manual ignores have complete structured metadata.

## Structured waiver shape

```yaml
ignored_violations:
  - id: ARCH-IGN-042
    source_type: Example.Application.Legacy.LegacyUseCase
    forbidden_reference: Example.Infrastructure.LegacyGateway
    target:
      fingerprint: sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
    reason: Temporary migration seam while the legacy gateway is extracted.
    owner: architecture-team
    issue: ARCH-231
    introduced: 2026-08-01
    expires: 2026-10-01
```

A structured waiver carries:

- stable waiver ID;
- display matchers that make the exception readable in policy review;
- exact `target.fingerprint` for the governed finding identity;
- non-empty reason;
- owner;
- tracking issue or remediation reference;
- introduced date;
- expiry date.

The exact field/type authority is the packaged schema (`arch-linter-net schema print policy-root`).

## Exact target identity

The display matcher is not the waiver target. Two findings can have similar text while still being distinct occurrences.

ArchLinterNet uses a canonical lowercase SHA-256 fingerprint over the normalized violation identity. The Core API exposes `ArchitectureWaiverTargetFingerprint.Create` when a programmatic migration or policy-authoring tool needs to create that value from an exact `ArchitectureViolationIdentity`.

A fingerprint is `sha256:` followed by 64 lowercase hexadecimal characters. Unsupported or non-canonical values fail policy validation.

Do not reuse a fingerprint for another occurrence and do not broaden the display matchers as a substitute for creating the correct target.

## Lifecycle states

Canonical waiver evidence distinguishes these states:

| State | Meaning | Expected action |
| --- | --- | --- |
| `active` | Complete structured metadata, within its review window, and matching the governed occurrence. | Keep only while the exception remains deliberately accepted. |
| `stale` | The waiver no longer matches a live governed finding. | Remove the obsolete waiver. |
| `expired` | The review window has elapsed. | Reassess immediately; strict lifecycle does not treat it as harmless debt. |
| `metadata_incomplete` | Legacy compatibility entry without complete structured lifecycle metadata. | Migrate it to a structured waiver or remove it. |
| `invalid` | Metadata or target is malformed/unsupported. | Fix the policy; invalid evidence does not suppress a finding. |

Use `--waiver-evaluation-date yyyy-MM-dd` when a reproducible expiry-boundary assessment is required. Otherwise the CLI captures one UTC evaluation date for the invocation.

## Finding baseline debt vs waiver debt

A baseline and a waiver solve different governance problems.

A **finding baseline** records reviewed existing findings so the repository can ratchet new debt without pretending the old debt disappeared. It is compared through baseline/gate/change workflows.

A **structured waiver** is a manual policy exception to one exact finding occurrence. It appears as explicit ignore debt in policy inventory and Architecture Health.

Do not duplicate the same governance decision in both places unless the product workflow specifically requires both independent facts. Health and reports preserve the distinction rather than combining them into one generic debt count.

## Intended exclusions are not waiver debt

A topology `out_of_scope` declaration, coverage exclusion with a reason, or other schema-backed intended-scope choice says that a subject is deliberately outside a particular governed universe. That is policy scope, not an ignored violation.

Review broadening an exclusion as a policy change because it can weaken governance, but do not count every legitimate exclusion as waiver debt.

## Policy inventory and deterministic counts

Validation can project the canonical `architecture-policy-inventory/v1` object. It includes:

- `effective_rule_count` for the selected effective policy;
- strict, audit and coverage control partitions;
- explicit ignore debt counts by lifecycle state;
- canonical waiver records for drill-down.

Effective controls are counted once by authored control identity. Imports, conditions and source-set/runtime fan-out do not turn one effective authored rule into many rules merely because it evaluates multiple concrete subjects.

A missing policy inventory is missing evidence. Consumers must not interpret it as zero controls or zero waivers.

## New and broadened waivers are change evidence

A waiver is a policy relaxation. Adding one or broadening its governed scope must remain visible to policy weakening/new-debt review instead of being treated as neutral configuration churn.

Use base/current policy contexts:

```bash
arch-linter-net policy context \
  --policy architecture/arch.yml \
  --format json > current-policy.json

arch-linter-net policy weakening \
  --base-context base-policy.json \
  --current-context current-policy.json
```

Architecture Health can therefore become `degrading` when a new or broadened waiver represents regression, even when unrelated dimensions remain healthy.

Removing a no-longer-needed waiver is improvement evidence. Do not keep stale exceptions merely to preserve historical counts.

## Migrating legacy ignores

A safe migration from v0.7-era matcher-only ignores is incremental:

1. upgrade/pin the v0.8 CLI while keeping the existing policy on `version: 1`;
1. run strict/audit validation and capture the current canonical finding identities;
1. for each still-legitimate manual exception, assign a stable waiver ID and exact target fingerprint;
1. add reason, owner, issue/remediation, introduced date and expiry;
1. remove ignores whose findings have already disappeared;
1. verify policy inventory/lifecycle output;
1. move the root policy to `version: 2`, or temporarily use explicit `compatibility` only while remaining entries are migrated;
1. rerun Health and policy-weakening review before merging.

Do not respond to a v0.8 diagnostic by weakening the underlying architecture rule just to keep the build green.

## CI behavior

CI should only read and evaluate waiver state. It must not automatically create, extend, broaden or delete waivers.

Recommended CI responsibilities are:

- run the packed CLI;
- fail closed on invalid/unassessable policy evidence;
- transport canonical JSON/SARIF/Health/report artifacts;
- expose waiver lifecycle and policy-inventory evidence to reviewers.

Repository scripts must not reimplement waiver matching, expiry evaluation, debt counting, policy weakening or Health classification.
