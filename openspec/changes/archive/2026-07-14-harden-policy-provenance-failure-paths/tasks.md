## 1. Typed root parsing

- [x] 1.1 Create the selected root descriptor before import detection and enrich
      malformed, multi-document, and non-mapping root parse failures.
- [x] 1.2 Add Core and CLI regression tests for typed root diagnostics in JSON
      and SARIF output.

## 2. Collision-safe provenance and raw validation

- [x] 2.1 Introduce escaped JSON Pointer provenance keys and migrate map
      construction, ownership binding, schema lookup, and runtime lookups.
- [x] 2.2 Set the active provenance subject for raw layer, contextual,
      port-boundary, and semantic-coverage validation before enrichment.
- [x] 2.3 Add imported raw-validation and legal-key collision regression tests.

## 3. Verification

- [x] 3.1 Run targeted Core and CLI regression tests.
- [x] 3.2 Run `make fmt` and `make acceptance` and resolve every failure.
