## Why

Imported policies can fail on Intel macOS because path identity relies on a
Darwin `stat` structure whose layout is not portable across macOS
architectures. The affected import loader is a common dependency of CLI,
Testing API, and CI consumers, so it must keep its existing security and
provenance guarantees while becoming platform-neutral.

## What Changes

- Replace the Darwin ABI-dependent policy-file identity handling with a
  managed, canonical-path identity on macOS.
- Preserve exact-case checks, physical link resolution, repository-boundary
  enforcement, regular-file validation, and deterministic duplicate/cycle
  detection.
- Mark selected-root resolution failures as root-policy diagnostics; retain the
  declaring fragment, import edge, and ordered chain for import failures.
- Add regression coverage for root-plus-fragment loading and root/fragment
  diagnostic roles.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-import-composition`: portable source identity and typed root/import
  resolution diagnostics.

## Impact

The change is limited to Core policy-import path resolution and its NUnit
coverage. CLI, Testing API, JSON, and SARIF continue to consume the same typed
policy diagnostic model without new public entry points or dependencies.
