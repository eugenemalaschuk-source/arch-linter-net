## Why

The portable import-path grammar currently accepts Windows device basenames when
they have an extension, allowing a policy graph to load on Unix but fail on
Windows. The #281 test matrix also assigns source-rich duplicate-ID diagnostics
to #281 even though that provenance behavior belongs to #282.

## What Changes

- Reject every normal import-path segment whose case-insensitive basename before
  its first dot is a Windows reserved device name, including the superscript
  `COM` and `LPT` variants.
- Add cross-platform fixture cases for reserved basenames with extensions.
- Limit #281 duplicate-ID coverage to stable rejection; retain the requirement
  to identify both declaration sources exclusively in #282.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `policy-import-composition`: Harden portable authored import-path validation
  and clarify duplicate-ID diagnostic ownership.

## Impact

Updates the normative OpenSpec and internal format/architecture handoff for
#281 and #282, plus fixture-ready documentation. No runtime code, public API,
or production schema changes are included.
