## Why

PR #314 review identified three ambiguities that could make policy import behavior inconsistent or silently discard currently visible classification state. The format contract must distinguish source-role validation from effective-policy validation, define a portable import-path grammar, and preserve raw classification sections before #281 implements the loader.

## What Changes

- Define two-stage validation: root/fragment source-role schema validation followed by full composed effective-policy schema validation before fallback IDs and semantic validators.
- Define a portable, platform-neutral grammar for relative import paths and explicitly reject Windows-specific, Unix-absolute, UNC/device, separator, and interpolation forms.
- Define composition behavior for raw-only `classification.path`, `classification.overrides`, and `classification.exclusions`, preserving the existing visible deferred notice for `path`.
- Replace the generated placeholder Purpose in the canonical policy-import-composition spec with a meaningful capability purpose.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-import-composition`: Clarify validation phases, portable path syntax, and classification raw-section composition.

## Impact

This review-fix change updates the canonical OpenSpec, archived design artifacts, format draft, architecture handoff, and fixture guidance. It remains design-only: #281 and #282 will implement the clarified contracts without adding a new public entry point or changing current runtime behavior.
