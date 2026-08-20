## Context

The #236 ingestion implementation has a fixed `TaskKeyExtraction` seam and
canonical path text, but it has no policy-backed configuration. The normal
architecture policy already supports source-aware loading, import composition,
effective-schema checks, raw YAML validation, and YamlDotNet deserialization.
This change must use that lifecycle rather than introduce a history-specific
file or command-line tuning surface.

## Goals / Non-Goals

**Goals:**

- Add optional `history_analysis` configuration to the normal architecture
  policy lifecycle.
- Expose only bounded task-extractor literals, exact path glob classification,
  ignores, fixed-shape profiles, and one co-change threshold.
- Keep #235/#236's canonical extraction, path, and failure semantics fixed.
- Let `history ingest --policy <path>` consume the one effective policy when a
  caller wants non-default configuration; omission retains the default profile.

**Non-Goals:**

- Implement hotspot, graph, bottleneck, OCP, report, or enrichment stages.
- Support user-supplied regular expressions, scripts, alternate Git/ref
  parsing, metadata transcoding, path normalization, or profile repair.
- Replace or make configurable the built-in `issue` extractor.

## Decisions

### One bounded policy model

`history_analysis` is a public YAML model owned by Core and validated twice:
the schema protects composed/imported policies and a raw-node validator gives a
monolithic policy the same strict unknown-key and numeric-literal behavior.
The deserialized model supplies defaults, and a document validator checks
cross-field invariants. This reuses the normal policy authority rather than
adding a history configuration parser.

### Literal extractor mini-language

A configured extractor has an ID, namespace, and `pattern` containing a
non-empty literal `prefix` and optional literal `suffix`. It matches exactly
`prefix + [0-9]+ + suffix` only when the outer scalars are outside
`[A-Za-z0-9_#]`; the captured digit sequence must be positive. Matching scans
raw UTF-8 bytes, so the complete match has the same half-open byte provenance
as the existing issue extractor. This deliberately small language supplies one
identifier without accepting arbitrary regex behavior.

The built-in `issue` extractor remains present for every effective profile and
custom extractors cannot use its reserved ID. Canonical ordering,
deduplication, `BigInteger` normalization, and overlap rejection remain the
responsibility of `TaskKeyExtraction`.

### Exact segment glob paths

Configured classification and ignore patterns use a small `/`-separated glob:
literal segments, `*` for one segment, and `**` for zero or more segments. The
grammar rejects backslashes, dot segments, empty segments, partial wildcards,
and character classes so matching never normalizes a path. Ignore matching
happens before category selection. The six configurable categories have the
theory's fixed priority; otherwise a retained path is `unknown`.

### Exact-decimal profiles

The raw validator accepts plain nonnegative base-10 decimal literals only,
with at most nine fractional digits. A fixed complete profile is required when
that profile is supplied. Profile values and co-change alpha/beta must sum
exactly to `1.000000000`; the optional threshold is in `[0,1]`. Defaults are
the #235 profiles, while an absent threshold leaves later `Gtheta` construction
disabled. No value is rounded, rescaled, or repaired at load time.

## Risks / Trade-offs

- [Literal extractors are less expressive than regex] → They make the captured
  identifier, span, and complexity auditable and deterministic.
- [An optional policy argument adds a command path] → It remains the sole
  configuration entry point; no individual analysis knobs are added to the CLI.
- [Future scoring is not implemented here] → Models/classifier are internal
  Core seams with focused tests, ready for #238–#241 without inventing scores.

## Migration Plan

`history_analysis` is optional. Existing policies and `history ingest` calls
without `--policy` use the same built-in issue extraction and fixed defaults.
Invalid newly-authored configuration fails policy loading before ingestion. No
data migration or rollback work is needed: removing the section restores the
default effective profile.
