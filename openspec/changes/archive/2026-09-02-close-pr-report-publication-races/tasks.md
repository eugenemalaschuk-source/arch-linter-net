## 1. Producer integrity

- [x] 1.1 Supply an ephemeral canonical empty Health baseline when the current worktree has no reviewed baseline, while retaining independent snapshot baseline handling.
- [x] 1.2 Reject Health output unless it is parseable `architecture-health/v1` before report rendering or upload.

## 2. Publisher integrity

- [x] 2.1 Distinguish an absent latest-attempt producer from a failed producer and preserve one verified same-head sticky report for partial reruns.
- [x] 2.2 Strictly decode report bytes once during validation and publish that validated UTF-8 string only.
- [x] 2.3 Add pre-write and post-write current-head checks that reject or repair detected stale report publication.

## 3. Regression evidence

- [x] 3.1 Extend executable publisher fixtures for malformed UTF-8, partial rerun preservation, and pre/post-write stale-head races.
- [x] 3.2 Run focused workflow and publisher tests, then run relevant formatting and OpenSpec validation.
