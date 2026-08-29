## Why

`ignored_violations` currently suppress known violations with only matcher and
optional rationale text. That allows accepted architecture debt to be anonymous,
non-expiring, and invisible once it no longer matches. The v0.8 governance
model needs deterministic, accountable waivers without weakening legitimate
architecture scope semantics or duplicating the existing policy-weakening and
new-debt gate authorities.

## What Changes

- Add a schema-backed architecture-waiver model for structured explicit
  `ignored_violations`, including stable ID, exact target identity, accountable
  remediation metadata, date-only expiry, and composed-policy provenance.
- Preserve legacy ignore entries as compatibility debt rather than silently
  breaking existing policies or treating absent metadata as healthy.
- Evaluate active, stale, expired, metadata-incomplete, and invalid waiver
  states deterministically from a supplied analysis date; expose the same
  canonical records through Core validation, CLI output, JSON, and Testing.
- Make invalid and expired structured waivers fail closed in the default strict
  policy profile, make stale waivers observable policy hygiene debt, and retain
  an explicit migration profile for legacy-policy compatibility.
- Enrich policy-context and policy-weakening evidence so adding or broadening
  structured waivers remains visible to the existing #119/#121 guardrail path.
- Document the distinction between architecture waivers, baseline finding debt,
  and legitimate policy scope exclusions, with migration guidance.

## Capabilities

### New Capabilities

- `architecture-waiver-lifecycle`: Canonical structured waiver metadata,
  deterministic lifecycle evaluation, compatibility, provenance, and policy
  hygiene semantics.

### Modified Capabilities

- `ignore-matching`: Structured waivers add exact target-identity matching while
  preserving legacy matcher behavior.
- `unmatched-ignore-alerting`: Stale ignores become canonical waiver lifecycle
  evidence rather than a standalone, minimally-described alert.
- `policy-context-export`: Effective policy context exports structured waiver
  identity, metadata, lifecycle, and provenance for downstream consumers.
- `policy-weakening-guardrails`: Structured waiver additions and broadenings
  retain deterministic weakening evidence through the existing authority.
- `test-adapter`: Typed validation results expose canonical waiver lifecycle
  records and policy-hygiene outcomes without parsing formatted output.

## Impact

Core policy schema/loading, contract execution and validation outcomes;
policy-context and policy-weakening projections; CLI human/JSON policy-check
output; the NUnit Testing adapter; Core/CLI/Testing public API snapshots;
architecture policy fixtures; public migration and governance documentation.
No external ticket/owner service, wall-clock dependency, policy auto-rewrite,
baseline lifecycle replacement, or reimplementation of #119/#121 is added.
