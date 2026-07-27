## 1. Core and SARIF comparison projection

- [x] 1.1 Add a deterministic Core SARIF formatter for typed baseline comparison and migration entries.
- [x] 1.2 Enable `sarif` format selection for baseline diff, verify, and migrate commands without changing existing human/JSON output.
- [x] 1.3 Add focused Core and CLI tests for comparison statuses and canonical identity SARIF properties.

## 2. Testing API

- [x] 2.1 Add public Testing API models and builder operations for baseline diff, verify, and dry-run migration outcomes.
- [x] 2.2 Reuse Core baseline application services and map their entries to typed Testing results.
- [x] 2.3 Add NUnit coverage for new, matched, stale, and ambiguous comparison statuses.

## 3. Documentation and completion

- [x] 3.1 Update migration-baseline and AI authoring documentation for SARIF and Testing comparison surfaces.
- [x] 3.2 Run formatting, focused tests, and full acceptance; synchronize specs and archive the change.
