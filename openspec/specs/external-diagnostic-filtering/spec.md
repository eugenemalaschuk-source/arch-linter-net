# external-diagnostic-filtering Specification

## Purpose
Define the trusted, deterministic projection of external SARIF diagnostics into policy-selected
findings. This capability keeps producer evidence local and bounded while preserving the validated
provenance, source facts, severity mapping, and stable identity required by downstream consumers.
## Requirements
### Requirement: Policy declares a bounded diagnostic filter with explicit matching expectations
An external-evidence requirement SHALL optionally declare one schema-backed diagnostic filter. The
parent requirement's logical evidence ID and exact expected tool/run identity SHALL remain the
authoritative evidence and producer selector. The filter SHALL allow only exact rule IDs, exact
driver-rule tags, exact project identities, normalized repository-relative path prefixes, and a
non-empty source-severity to governance-mode map. A severity map value SHALL be exactly `strict`
or `audit`; a source severity key SHALL be exactly `error`, `warning`, `note`, `none`, or
`unspecified`. Unknown, blank, duplicate, unsafe, or unsupported filter values SHALL fail policy
validation with declaration provenance. Each rule-ID, rule-tag, project, and path-prefix selector
list SHALL contain at most 128 values; the severity map SHALL contain at most the five supported
source-severity keys.

The trust reader SHALL capture an immutable authorization snapshot for every valid result. That
snapshot SHALL include the parent logical ID, tool/version/run identity, required context-binding
flags, validated assessment context, and a detached diagnostic-filter copy. The selector SHALL
consume only that captured snapshot; it SHALL NOT accept a second mutable requirement or apply a
different policy's filter to already trusted evidence. An authorization-group identity SHALL be
structural or use unambiguous length-prefixed values; a policy-supplied control or separator
character SHALL NOT cause distinct authorization snapshots to join the same group.

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

#### Scenario: A later mutable policy cannot re-authorize trusted evidence
- **WHEN** a result was trusted for one tool/run/filter authorization and a caller later changes
  a requirement with the same logical ID
- **THEN** selection uses only the immutable authorization captured by the reader and cannot apply
  the changed requirement to that evidence

#### Scenario: An unknown producer remains a trust failure
- **WHEN** a SARIF artifact has no run matching the parent requirement's expected tool and run
  identity
- **THEN** the #520 trust boundary reports its missing expected run outcome and diagnostic
  selection does not reinterpret it as an unmatched rule filter

### Requirement: Trusted source diagnostics retain typed source facts and evidence provenance
When an external-evidence requirement declares a diagnostic filter, the system SHALL expose an
immutable typed source-diagnostic collection only after the bounded SARIF reader has accepted the
selected run as valid. Each source record SHALL retain, where present, the original message, rule
ID, original source severity, primary source location and region, project identity, driver-rule
tags, and source-provided fingerprint pairs. Each selected diagnostic SHALL retain the complete
validated evidence provenance: logical evidence key, producer tool/version, SARIF run identity,
repository/revision/scope context, repository-relative artifact path, and deterministic artifact
content hash.

The reader SHALL parse these facts from the same bounded bytes that it trusts and SHALL expose no
selectable source diagnostics from a missing, malformed, unsuccessful, ambiguous, stale, or
wrong-context artifact. The selector SHALL consume only trusted reader results and SHALL NOT
perform a second currentness or producer-service check.

The reader SHALL resolve a result rule through `result.ruleId` and `result.rule.id`, or through
`result.ruleIndex` and `result.rule.index` into `tool.driver.rules`, and SHALL reject conflicting
ID/index combinations. A repeated descriptor ID is valid only when an index makes the descriptor
unambiguous; an ID-only reference to repeated descriptors SHALL fail closed instead of combining
their tags. The reader SHALL likewise resolve an artifact location index through `run.artifacts`.
An absent result message or an unsupported unresolved message reference SHALL fail closed rather
than become an empty source fact.

When present, `startLine`, `startColumn`, `endLine`, and `endColumn` SHALL be positive integers;
`charOffset` and `charLength` SHALL be non-negative integers. An end line SHALL not precede a
start line, and an end column on the same line SHALL not precede the start column. A region that
violates these bounds or ordering constraints SHALL fail closed rather than become a trusted
location fact.

For a requirement without a diagnostic filter, the reader SHALL retain the preceding trust-only
SARIF behavior: it SHALL not project or validate result members needed only for source diagnostic
selection, and it SHALL expose no source diagnostics or selection authorization.

#### Scenario: A trusted result retains source and trust provenance
- **WHEN** a validated current-context SARIF result has a rule, message, location, tool version,
  source fingerprint, and artifact hash
- **THEN** its selected representation retains those facts together with the validated logical
  evidence, repository, revision, and scope provenance

#### Scenario: A wrong-revision artifact is never selected
- **WHEN** the bounded reader rejects an artifact because its revision differs from the current
  assessment context
- **THEN** no ordinary selected diagnostic is exposed from that artifact

#### Scenario: Repeated descriptor IDs retain their indexed tags
- **WHEN** two tool-driver rule descriptors share an ID but have distinct tags and results identify
  the descriptors by their respective rule indexes
- **THEN** each source diagnostic retains only the tags of its indexed descriptor

#### Scenario: Trust-only evidence retains #520 result-shape compatibility
- **WHEN** an otherwise valid SARIF requirement has no diagnostic filter and its result contains a
  member that is irrelevant to trust validation but unsupported by source projection
- **THEN** the reader remains valid and exposes no source diagnostics

#### Scenario: Invalid region bounds are not trusted
- **WHEN** a projected result contains a zero or negative line/column, a negative character
  offset/length, or an ending position before its start
- **THEN** the reader rejects the artifact as an unsupported shape and exposes no source diagnostics

### Requirement: Selection has deterministic severity, identity, ordering, and deduplication
The system SHALL map each eligible source severity to the filter's configured `strict` or `audit`
mode without overwriting or replacing the original source severity. It SHALL preserve all
well-formed source-provided fingerprints and prefer a deterministic source fingerprint when one
is available. When no preferred source fingerprint is available, it SHALL produce a deterministic
fallback fingerprint from stable normalized evidence and source-location facts, never from message
text or runtime enumeration order.

The canonical selected-result identity SHALL include the logical evidence key, validated
repository/revision/scope context, expected producer identity, rule/project/location facts, and
preferred-or-fallback fingerprint, original source severity, and mapped governance mode. It SHALL
exclude transient artifact hash and run identity from
the persistent selected-result identity while preserving them as ordered provenance. Equivalent
results with the same source severity and governance mode SHALL deduplicate into one selected
result with all authorizing provenance entries; results with different source severities or modes
SHALL remain distinct. Different logical evidence keys, revisions, scopes, or source locations
SHALL also remain distinct.
Selected results, mismatch records, source-fingerprint pairs, and provenance entries SHALL use
deterministic ordinal ordering.

#### Scenario: Equivalent repeated runs deduplicate without display text
- **WHEN** two trusted evidence inputs describe the same rule and normalized location in the same
  logical evidence and validated context but have different artifact hashes or result ordering
- **THEN** selection returns one canonical result with both ordered provenance entries and an
  identity that does not depend on either result message or input order

#### Scenario: Complementary trusted artifacts jointly satisfy required values
- **WHEN** two artifacts authorized by the same immutable requirement each contain one of two
  required rule IDs
- **THEN** the selector evaluates required matches across their combined trusted source facts and
  reports no mismatch in either input order

#### Scenario: Separator characters cannot merge authorization groups
- **WHEN** two authorization snapshots differ only through a filter value containing a control or
  separator character
- **THEN** the selector evaluates their required matches independently and does not combine their
  trusted source facts

#### Scenario: Different source severities remain distinct
- **WHEN** two otherwise equivalent source results have the same fingerprint and location but map
  from different source severities to different governance modes
- **THEN** selection returns one stable result for each severity/mode rather than discarding either
  governance semantic during deduplication

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
For each immutable authorization group, it SHALL evaluate source diagnostics in one pass using
ordinal selector membership indexes; it SHALL not rescan the complete diagnostic collection for
each required selector value.

#### Scenario: Selection does not consult a producer service
- **WHEN** a caller selects trusted external diagnostics
- **THEN** the operation uses only the supplied trusted reader results and policy filter without
  network or producer-status access

### Requirement: Selected diagnostics have one non-revalidating normalization consumer
The trusted external-diagnostic selection result SHALL be consumable by a normalized-finding
projector that preserves its immutable source and evidence provenance. The consumer SHALL treat
#520 trust status and #521 selection identity as authoritative and SHALL NOT accept a replacement
mutable filter, reopen SARIF bytes, or turn a rejected artifact into a selected finding.

#### Scenario: Normalization cannot reauthorize stale evidence
- **WHEN** an artifact was rejected for a wrong required revision
- **THEN** normalization records the corresponding applicability evidence but cannot project its
  source results as a current governed finding
