## 1. Ordinary-validation diff semantics

- [x] 1.1 Carry the native declared-topology applicability record through the diff report and render its state, reasons, membership, and provenance.
- [x] 1.2 Return a typed non-reviewable result for `unexpected_empty_input` while retaining projectable drift categories.
- [x] 1.3 Cover unassessable and projectable diff outcomes with CLI renderer/handler tests.

## 2. Provenance-based output publication

- [x] 2.1 Expose consumed source inputs from capture and validation provenance without a CLI repository rescan.
- [x] 2.2 Check aliases only for existing outputs and preserve cancellation through temporary-file cleanup.
- [x] 2.3 Add regression tests for Unity-like unrelated trees, aliases, and cancellation between temporary write and rename.

## 3. Lifecycle and CLI contract evidence

- [x] 3.1 Run capture, diff, and verify lifecycle fixtures with output artifacts and hash every consumed input class.
- [x] 3.2 Align topology help with option registration and add parser/help parity tests.

## 4. Validation and change completion

- [x] 4.1 Run focused tests, formatting, architecture/public API checks, and coverage as warranted by the changed code.
- [x] 4.2 Archive the OpenSpec change and validate the resulting specifications.
