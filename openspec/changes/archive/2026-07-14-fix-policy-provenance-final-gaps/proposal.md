## Why

Review found three remaining provenance contract gaps: lexical ordering inside
a source, incomplete exception location data, and untyped exhaustive template
expansion failures.

## What Changes

- Preserve composed encounter order for all related locations.
- Complete policy-exception JSON location fields.
- Convert exhaustive template expansion failures to typed provenance diagnostics.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `policy-import-composition`: ordered and typed provenance must survive all validation and expansion failures.
- `violation-reporting`: exception outputs must match normal location fields.

## Impact

Provenance indexing, template expansion, CLI exception formatting, and tests.
