## Why

Review of #827 found that the shipped disclosure validator accepted a wider
language than the shipped schema, rejected its own unavailable fixture, and
did not make strict-profile failures actionable. These gaps weaken the closed
public-representation guarantee and break portability of the packed consumer
test matrix.

## What Changes

- Make unavailable output and the bounded canonical count grammar part of the
  executable disclosure allow-list.
- Preserve a stable diagnostic for strict-profile rejection while retaining the
  fail-closed public payload and exit code.
- Make packed-tool verification portable across Windows, Linux, and macOS.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `architecture-policy-badge-cli`: tighten strict disclosure profile validation,
  diagnostics, and packaged-consumer portability requirements.
- `architecture-health-badge-relay-contract`: require profile validators to
  accept every shipped canonical representation and reject out-of-schema
  count forms.

## Impact

Updates internal CLI validation and its NUnit package-consumer regression test.
No public .NET API, dependencies, relay deployment, or publication authority
changes are introduced.
