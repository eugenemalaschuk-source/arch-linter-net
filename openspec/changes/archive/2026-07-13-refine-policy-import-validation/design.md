## Context

The archived #280 design introduced root/fragment schemas, composition, and
provenance, but its wording left a gap between a deliberately lightweight root
source shape and the current full policy schema. It also described safe path
resolution without defining a portable authored-path language, and treated all
classification lists as model-bound even though `path`, `overrides`, and
`exclusions` are intentionally raw-YAML-only today.

The current loader detects a non-empty root `classification.path` directly from
raw YAML and exposes `ClassificationPathDeferred`. `overrides` and `exclusions`
are schema-valid deferred no-ops. Import composition must retain those distinct
current behaviors.

## Goals / Non-Goals

**Goals:**

- Make validation order and the effective policy contract unambiguous.
- Make one authored import string have the same syntactic meaning on Windows,
  Linux, and macOS before filesystem resolution.
- Preserve raw classification sections and the visible path-deferred diagnostic
  across composed sources.
- Keep the #281 implementation local to the existing loader and filesystem
  seams, with no adapter or contract-family redesign.

**Non-Goals:**

- Implementing imports, changing the current production schema, or enabling
  deferred classification features.
- Broadening path support to absolute, platform-native, remote, glob, or
  interpolation syntax.
- Making `overrides` or `exclusions` behavior-owning before their existing
  semantic feature work ships.

## Decisions

### Validate source role first, then validate the effective policy

The root source schema is a role/shape schema: it requires `version` and
`name`, permits `imports` and all root sections, and records explicitly
declared fields. A fragment source schema permits only fragment sections. These
checks run per source before composition.

After composition, the effective document is validated against the full
effective-policy schema: `version`, `name`, `layers`, `analysis`, and
`contracts` are required, matching the current production schema. This occurs
before fallback ID assignment and the existing ordered semantic validator
pipeline. A root can therefore defer `layers`, `analysis`, or `contracts` to
fragments, but a complete graph cannot omit them.

Using only the root source schema would accept a non-executable effective
policy; requiring all full sections on the root source would defeat focused
fragments. The two phases solve both constraints without root precedence.

### Define a portable relative import grammar

An import is a Unicode-NFC scalar containing one or more `/`-separated
segments. A segment is `.` or `..`, or a portable file-name segment. Portable
segments contain no control character or `<`, `>`, `:`, `"`, `/`, `\\`, `|`,
`?`, or `*`; do not end in dot or space; and are not a Windows reserved device
name (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`, ignoring case).
Empty segments, non-NFC strings, backslashes, a leading slash, drive/URI
colons, and interpolation tokens (`${`, `$(`, `%...%`, or a leading `~`) are
invalid. The grammar deliberately accepts `.` and `..`; canonical physical
resolution and the repository-boundary check decide whether their target is
allowed.

This narrow grammar rejects Unix absolute paths, Windows drive paths, UNC and
device forms, and separator-dependent paths before host-specific APIs can
interpret them. It is more restrictive than native filesystems by design so a
checked-in policy is portable.

### Compose raw classification fields explicitly

The composer handles model-bound classification fields as before:
`attributes`, `assembly_attributes`, `inheritance`, and `namespace` append in
composed order; `precedence` is singleton-conflict checked.

`classification.path`, `classification.overrides`, and
`classification.exclusions` are retained in raw classification composition
metadata with source descriptors and composed order. `path` entries are counted
across all sources; a non-zero total sets `ClassificationPathDeferred` to that
total, preserving the present visible deferred-support diagnostic. `overrides`
and `exclusions` remain schema-valid, provenance-preserving deferred no-ops;
they do not silently become behavior because they were never model-bound.

Dropping raw nodes after per-file parsing was rejected because it would turn an
imported `classification.path` from a visible notice into a silent no-op.

## Risks / Trade-offs

- [Portable grammar excludes some native filenames] → A checked-in policy needs
  cross-platform meaning; diagnostics explain the rejected character or form.
- [Two schema phases add implementation work] → The phase boundary prevents
  incomplete policies reaching fallback IDs or family validators.
- [Raw metadata extends composition state] → It is confined to the loader and
  provenance index, avoiding changes to public contract DTOs.

## Migration Plan

1. Archive this clarification into the existing capability and set its Purpose.
2. Update the draft, handoff, examples, and test matrix in this PR.
3. #281 implements both schema phases, grammar checks, and raw classification
   composition; #282 uses retained source descriptors in diagnostics.

## Open Questions

None. Future support for deferred classification semantics requires its own
proposal and can consume the preserved raw metadata.
