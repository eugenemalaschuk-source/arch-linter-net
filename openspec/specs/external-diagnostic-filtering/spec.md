# external-diagnostic-filtering Specification

## Purpose
TBD - created by archiving change add-external-diagnostic-filtering. Update Purpose after archive.
## Requirements
### Requirement: Policy declares a bounded diagnostic filter with explicit matching expectations
An external-evidence requirement SHALL optionally declare one schema-backed diagnostic filter. The
parent requirement's logical evidence ID and exact expected tool/run identity SHALL remain the
authoritative evidence and producer selector. The filter SHALL allow only exact rule IDs, exact
driver-rule tags, exact project identities, normalized repository-relative path prefixes, and a
non-empty source-severity to governance-mode map. A severity map value SHALL be exactly `strict`
or `audit`; a source severity key SHALL be exactly `error`, `warning`, `note`, `none`, or
`unspecified`. Unknown, blank, duplicate, unsafe, or unsupported filter values SHALL fail policy
validation with declaration provenance.

Non-empty categories SHALL combine conjunctively and multiple values in one category SHALL combine
disjunctively. `require_matches: true` SHALL require every configured rule ID, tag, project, path
prefix, and source-severity value to match at least one source result satisfying all other
configured categories. A filter mismatch SHALL be explicit, ordered selection evidence; it SHALL
NOT be silently treated as a valid zero-result selection.

#### Scenario: A strict rule and audit warning are declared
- **WHEN** a logical SARIF evidence requirement declares rule ID `SEC100`, path prefix `src/`,
  and source severities `error: strict` and `warning: audit`
- **THEN** only trusted results that satisfy those criteria are eligible and each selected result
  preserves its original source severity while receiving the mapped governance mode

#### Scenario: A stale required rule is not ignored
- **WHEN** `require_matches` is true and a configured rule ID occurs in no source result that
  satisfies the other configured criteria
- **THEN** selection exposes a deterministic unmatched-rule filter record rather than silently
  returning a valid empty result

#### Scenario: An unknown producer remains a trust failure
- **WHEN** a SARIF artifact has no run matching the parent requirement's expected tool and run
  identity
- **THEN** the #520 trust boundary reports its missing expected run outcome and diagnostic
  selection does not reinterpret it as an unmatched rule filter

### Requirement: Trusted source diagnostics retain typed source facts and evidence provenance
The system SHALL expose an immutable typed source-diagnostic collection only after the bounded
SARIF reader has accepted the selected run as valid. Each source record SHALL retain, where
present, the original message, rule ID, original source severity, primary source location and
region, project identity, driver-rule tags, and source-provided fingerprint pairs. Each selected
diagnostic SHALL retain the complete validated evidence provenance: logical evidence key, producer
tool/version, SARIF run identity, repository/revision/scope context, repository-relative artifact
path, and deterministic artifact content hash.

The reader SHALL parse these facts from the same bounded bytes that it trusts and SHALL expose no
selectable source diagnostics from a missing, malformed, unsuccessful, ambiguous, stale, or
wrong-context artifact. The selector SHALL consume only trusted reader results and SHALL NOT
perform a second currentness or producer-service check.

#### Scenario: A trusted result retains source and trust provenance
- **WHEN** a validated current-context SARIF result has a rule, message, location, tool version,
  source fingerprint, and artifact hash
- **THEN** its selected representation retains those facts together with the validated logical
  evidence, repository, revision, and scope provenance

#### Scenario: A wrong-revision artifact is never selected
- **WHEN** the bounded reader rejects an artifact because its revision differs from the current
  assessment context
- **THEN** no ordinary selected diagnostic is exposed from that artifact

### Requirement: Selection has deterministic severity, identity, ordering, and deduplication
The system SHALL map each eligible source severity to the filter's configured `strict` or `audit`
mode without overwriting or replacing the original source severity. It SHALL preserve all
well-formed source-provided fingerprints and prefer a deterministic source fingerprint when one
is available. When no preferred source fingerprint is available, it SHALL produce a deterministic
fallback fingerprint from stable normalized evidence and source-location facts, never from message
text or runtime enumeration order.

The canonical selected-result identity SHALL include the logical evidence key, validated
repository/revision/scope context, expected producer identity, rule/project/location facts, and
preferred-or-fallback fingerprint. It SHALL exclude transient artifact hash and run identity from
the persistent selected-result identity while preserving them as ordered provenance. Equivalent
results SHALL deduplicate into one selected result with all authorizing provenance entries;
different logical evidence keys, revisions, scopes, or source locations SHALL remain distinct.
Selected results, mismatch records, source-fingerprint pairs, and provenance entries SHALL use
deterministic ordinal ordering.

#### Scenario: Equivalent repeated runs deduplicate without display text
- **WHEN** two trusted evidence inputs describe the same rule and normalized location in the same
  logical evidence and validated context but have different artifact hashes or result ordering
- **THEN** selection returns one canonical result with both ordered provenance entries and an
  identity that does not depend on either result message or input order

#### Scenario: Same rule at distinct locations remains distinct
- **WHEN** two eligible source results have the same rule and fingerprint but distinct normalized
  primary source locations
- **THEN** selection returns two canonical results

#### Scenario: Different evidence contexts do not collapse
- **WHEN** visually similar eligible results belong to different logical evidence IDs, revisions,
  or scopes
- **THEN** selection returns distinct canonical results even when their source fingerprints match

#### Scenario: Missing source fingerprint uses deterministic fallback
- **WHEN** an eligible result has no preferred source-provided fingerprint but has the stable rule
  and normalized location facts needed for fallback identity
- **THEN** selection exposes a deterministic fallback fingerprint and preserves the absence of a
  source fingerprint

### Requirement: External diagnostic selection remains vendor-neutral and non-executing
The system SHALL operate only on bounded local SARIF bytes already supplied to the trust reader.
It SHALL NOT execute or configure an analyzer, query a producer/SaaS API, use producer job status,
or infer selection/currentness from artifact names, timestamps, workflow names, or display text.

#### Scenario: Selection does not consult a producer service
- **WHEN** a caller selects trusted external diagnostics
- **THEN** the operation uses only the supplied trusted reader results and policy filter without
  network or producer-status access
