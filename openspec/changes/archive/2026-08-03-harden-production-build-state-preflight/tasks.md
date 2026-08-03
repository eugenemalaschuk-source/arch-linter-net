## 1. Production context propagation

- [x] 1.1 Add Platform and RuntimeIdentifier to public validation and snapshot request flow, preflight construction, and output resolution.
- [x] 1.2 Publish and verify the same context in receipts, schemas, and public API approvals.

## 2. Bounded trusted input collection

- [x] 2.1 Reject every candidate with a symbolic-link or junction ancestor before filesystem reads.
- [x] 2.2 Stop recursive source traversal immediately when count or aggregate-byte budget is exhausted.

## 3. Receipt agreement and validation

- [x] 3.1 Compare receipt cache outcome and normalized reasons with the current manifest without rejecting agreed ineligibility.
- [x] 3.2 Add end-to-end and regression coverage, run formatting, OpenSpec validation, and relevant test/lint suites.
