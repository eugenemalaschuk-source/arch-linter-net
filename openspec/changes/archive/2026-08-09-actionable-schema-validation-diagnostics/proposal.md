## Why

Effective-policy schema validation currently exposes every rejected `anyOf`, `oneOf`, and conditional branch, even when a branch's discriminator does not apply. This buries the actual policy error and can point the summary at an unrelated location, forcing adopters to use external validators to diagnose their policy.

## What Changes

- Select actionable effective-schema validation failures instead of flattening all failed alternatives.
- Suppress variant branches that are inapplicable according to their discriminator or conditional guard.
- Prefer the deepest relevant failure for the primary provenance location while preserving deterministic human and structured diagnostics.
- Add regression coverage for nested alternatives, conditionals, scalar-map type errors, and valid variants.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-import-composition`: effective-policy schema failures identify actionable, applicable validation defects with deterministic diagnostics and provenance.

## Impact

Affected code is the Core effective-schema validation projection and its NUnit regressions. The JSON Schema engine and policy validity semantics remain unchanged; CLI and Testing consumers receive the same typed failure category with improved message and location selection.
