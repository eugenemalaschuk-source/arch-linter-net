## Why

Architecture diagnostics currently identify structural violations and preserve
their evidence, but do not state when a safe, deterministic class of repair is
known. Reviewers and AI coding agents therefore have to infer remediation from
display text, which risks policy weakening, broad ignores, and invented
architectural intent.

## What Changes

- Add optional, structured remediation hints to normalized findings without
  changing canonical finding identity or producing executable fixes.
- Define a finite machine-readable hint vocabulary and generate specialized
  hints only from established policy and Core evidence; retain a safe
  review/no-specialized-hint fallback when that evidence is absent.
- Project equivalent hint semantics through human, JSON, SARIF, and Testing
  output using the existing diagnostic-detail projection registry.
- Document the supported categories, safe-fix ordering, evidence limits, and
  the fact that hints never authorize broad policy weakening or automatic edits.

## Capabilities

### New Capabilities

- `architecture-remediation-hints`: Deterministic, evidence-backed guidance for
  safe architectural remediation of normalized findings.

### Modified Capabilities

- `diagnostics-model`: Normalized findings carry optional remediation metadata
  while retaining existing identity and typed detail semantics.
- `diagnostic-detail-projection-registry`: Structured hint projection remains
  additive, deterministic, and completeness-protected.
- `violation-reporting`: Human and JSON output expose normalized remediation
  guidance without treating it as an automatic fix.
- `sarif-diagnostics-output`: SARIF preserves remediation guidance as result
  properties/help-adjacent evidence rather than SARIF fixes.
- `test-adapter`: Testing consumers can inspect the same normalized remediation
  metadata without parsing formatted output.

## Impact

Affected areas are the Core normalized finding and reporting models, the
diagnostic detail projection registry, SARIF formatter, Testing result surface,
NUnit projection/generation tests, and public output documentation. The change
adds a public structured metadata type but introduces no new dependencies,
contract evaluator, policy syntax, or automatic code/policy mutation.
