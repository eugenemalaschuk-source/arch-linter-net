## Why

The bounded SARIF reader proves that one repository-local artifact is trustworthy, but it
intentionally does not expose a governed, deterministic subset of its results. Issue #521 adds
that selection boundary so later normalization can consume only policy-authorized diagnostics
without losing the validated evidence context that made them eligible.

## What Changes

- Add a vendor-neutral, schema-backed external-diagnostic filter declared with each logical SARIF
  evidence requirement.
- Expose trusted SARIF results through a typed selection model that retains source tool, rule,
  message, location, source fingerprint, and #520 evidence provenance.
- Define deterministic severity mapping, canonical selection ordering, and duplicate suppression
  that distinguish locations and trusted evidence contexts.
- Reject unknown or stale configured rule/tag selectors when policy declares them as required
  matches; do not silently ignore them.
- Document the external-evidence filter format, provenance, fingerprint, and non-goal boundary.

## Capabilities

### New Capabilities

- `external-diagnostic-filtering`: Select, classify, fingerprint, order, and deduplicate trusted
  SARIF diagnostics using vendor-neutral policy filters while retaining source and trust
  provenance.

### Modified Capabilities

- `external-sarif-evidence`: Make the bounded trusted-reader result available to the filtering
  boundary without weakening its local-input, successful-run, or context-binding trust contract.

## Impact

- Core external-evidence policy/schema models, validation, SARIF reader, and new typed selection
  models in `ArchLinterNet.Core`.
- Focused NUnit coverage for policy schema validation and deterministic selection behavior.
- External-evidence documentation and the reviewed Core public-API snapshot.
