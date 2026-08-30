## Context

Issue #520 already provides a bounded, repository-local `SarifEvidenceReader`. It selects one
successful SARIF 2.1.0 run and proves its logical evidence key, producer identity, repository,
revision, and scope context. Its result currently carries only the count of source results, so a
later consumer cannot select diagnostics without reopening an artifact or losing the established
trust binding.

Issue #521 is the next boundary. It must be usable by #522's normalized-finding work without
turning the reader into an analyzer runner, an output adapter, or a vendor-service client.

## Goals / Non-Goals

**Goals:**

- Let an `external_evidence` requirement declare a bounded diagnostic filter in the same policy
  object that already owns its logical key and expected tool/run identity.
- Expose typed source-result facts only after the reader has accepted the artifact as trusted.
- Make filter results, strict/audit mapping, fingerprints, provenance, ordering, and duplicate
  suppression deterministic and independent of input enumeration or display text.
- Preserve the complete #520 evidence provenance with every selected result and keep source
  tool/rule/message/location/severity facts inspectable.

**Non-Goals:**

- CLI wiring, applicability projection, baseline/output integration, or normalized findings
  (#522).
- Analyzer execution/configuration, remote artifacts or producer APIs, arbitrary regex/scripts,
  or a second trust/currentness validator.
- Rewriting a source severity or treating vendor job status as selection input.

## Decisions

### The filter extends its logical external-evidence requirement

`ArchitectureExternalEvidenceRequirement` will gain one optional typed `diagnostic_filter`
object. The parent requirement remains the authoritative logical evidence key and exact expected
tool/run identity; the child filter contains:

- `rule_ids`, `rule_tags`, `projects`, and repository-relative `path_prefixes`;
- a non-empty `severity` map from source levels (`error`, `warning`, `note`, `none`, or
  `unspecified`) to ArchLinterNet `strict` or `audit`; and
- `require_matches`, which makes every configured selector value an explicit expected match.

Non-empty filter categories combine conjunctively, while values within a category combine
disjunctively. A severity-map entry is both an allowed source severity and the selected finding's
governance mode. `require_matches` checks every configured value against results satisfying the
other categories, returning deterministic filter-mismatch evidence instead of silently accepting
a stale allow-list. Empty categories impose no predicate. Path prefixes are normalized,
repository-relative `/` paths and use exact-or-descendant matching; they are intentionally not
glob or regular-expression programs.

This keeps logical-key and producer identity selection at the existing trust boundary rather than
duplicating them in a parallel diagnostics policy block. A missing expected tool remains #520's
`missing_expected_run` trust outcome; the selector only receives trusted results.

### Reader projection happens only after trust validation

`SarifEvidenceReadResult` will expose an immutable collection of typed source diagnostics only
when the selected run is valid. The reader will parse the bounded result objects once from the
same bytes it hashes and validates, then attach the selected run's result facts to the valid read
result. Trust failures expose no selectable diagnostics.

The typed source record preserves the original message, rule ID, original source severity,
primary repository-relative location and region where present, optional result `properties.project`,
driver-rule tags, and string-valued SARIF `fingerprints` / `partialFingerprints`. A malformed
consumed result field is an actionable unsupported-source-shape outcome; absent optional fields
remain absent. Driver-rule tags are joined to result rule IDs by exact ordinal identity. The
selection layer never derives a project from a path or a currentness claim from result fields.

### One selector consumes trusted inputs and retains a provenance set

A dedicated Core selector will consume typed pairs of an external-evidence requirement and its
valid reader result. It rejects an untrusted or logically mismatched input as a programming
boundary violation; #520 trust results continue to be handled by applicability work rather than
reclassified as ordinary diagnostics.

For every matching result, the selector carries the #520 `SarifEvidenceProvenance` unchanged,
including logical ID, tool/version, run ID, repository/revision/scope, artifact path, and content
hash. Equivalent results are grouped by a canonical semantic identity and retain an ordered set
of their evidence provenances, so repeated runs do not discard their authorizing artifacts.

### Fingerprints and canonical identity do not use display text

The selector retains all well-formed source fingerprint pairs. When the trusted source supplies
at least one `fingerprints` pair, the ordinally first pair is the preferred fingerprint and is
marked `source`; otherwise it creates a lowercase SHA-256 fallback marked `deterministic` from
the logical evidence key, resolved repository/revision/scope, expected tool identity, rule ID,
project, and normalized primary location/region. Source messages and runtime/SARIF enumeration
ordinals are never fingerprint or identity inputs.

Canonical grouping always includes the logical evidence key and resolved context plus the source
rule/project/location and preferred-or-fallback fingerprint. It deliberately excludes transient
artifact hash and run ID from the persistent selected-result identity while preserving them as
ordered provenance, so equivalent current-context repeated runs deduplicate. Different logical
keys, revisions, or scopes remain distinct. Selected results and their provenance sets are ordered
by canonical semantic values using ordinal comparison.

## Risks / Trade-offs

- [External SARIF omits a project, rule tag, or source fingerprint] → the field remains absent;
  only a filter requiring it fails to match, and deterministic fallback identity remains available
  when rule and location facts suffice.
- [A configured selector becomes stale] → `require_matches` emits ordered mismatch evidence;
  an intentional bounded subset can leave it false.
- [A source fingerprint is reused for two locations] → primary normalized location/region is also
  part of canonical identity, preventing distinct locations from collapsing.
- [New Core models change public API] → add focused approved-API evidence and update the reviewed
  snapshot only through the explicit public-API lifecycle.

## Migration Plan

The feature is opt-in. Policies without `diagnostic_filter` retain #520 reader behavior. A policy
can add a filter under an existing external-evidence requirement, supply explicit severity mapping,
and then have a later consuming feature invoke the selector. Removing the filter restores the
prior trust-only boundary; no persisted state or remote service is migrated.
