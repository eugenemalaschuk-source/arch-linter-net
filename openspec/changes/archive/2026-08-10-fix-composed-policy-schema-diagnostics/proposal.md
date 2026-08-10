## Why

The packed-artifact 0.6.1 consumer-cleanup gate (#466) re-ran the F6 (#440) regression against the
installed candidate and found that the composed-policy path violates F6's own acceptance
scenarios (#471):

- A specialized diagnostic message (assembly/project coverage `roots`) was paired with a location
  computed from the generic schema failures, so the reported location described a different,
  entirely valid part of the policy.
- Failures beneath an `if` discriminator were reported as defects even though `if` never makes a
  document invalid.
- A plural `anyOf` ("declare `target_assemblies`, a `solution`, or `projects`") made every branch
  look inapplicable, so no alternative could be suppressed and the unsatisfied alternatives were
  reported as missing requirements.
- Type-discriminated alternatives were retained even when the failing value's own JSON type ruled
  them out, so one wrong scalar produced one diagnostic per rejected type.

Together these buried the real defect and dragged the primary provenance location onto valid
policy, which is exactly the adopter-facing failure #440 set out to remove.

## What Changes

- Bind the reported policy location to the reported message: a specialized diagnostic owns its own
  instance pointer.
- Treat any failure beneath an `if` discriminator as variant selection, not a defect.
- Treat an alternative the instance already satisfies as applicable.
- Treat an alternative whose declared `type` cannot accept the failing value as inapplicable.
- Add composed-policy regressions for each case and keep single-file behavior unchanged.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-import-composition`: state that the reported location describes the reported message and
  extend alternative suppression to `if` discriminators, satisfied alternatives, and incompatible
  declared types.

## Impact

Affected code is the Core effective-schema diagnostic projection and its NUnit regressions. Policy
validity semantics, the JSON Schema engine, schema artifacts, and CLI exit codes are unchanged.
