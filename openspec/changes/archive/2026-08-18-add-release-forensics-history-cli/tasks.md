## 1. Canonical Git object access

- [x] 1.1 Add repository discovery, `extensions.objectformat` detection, and canonical lowercase full object-ID representation for SHA-1 and SHA-256.
- [x] 1.2 Add a loose-object and packfile object database with `OBJ_OFS_DELTA`/`OBJ_REF_DELTA` reconstruction that fails closed on missing or unreadable objects.
- [x] 1.3 Add deterministic authored-ref resolution with packed-refs lookup, symbolic-ref cycle detection, annotated-tag peeling, and shorthand collision failure.

## 2. Canonical commit metadata

- [x] 2.1 Parse raw commit objects into direct headers and raw message payload, failing closed on malformed or duplicated required headers.
- [x] 2.2 Implement the exact right-to-left `author`/`committer` grammar, canonical ASCII-only author identity, and arbitrary-precision committer epoch integers with a retained non-shifting timezone token.
- [x] 2.3 Retain every direct `encoding ` header as ordered lowercase-hexadecimal provenance without transcoding.

## 3. Canonical TaskKey extraction

- [x] 3.1 Add the stable extractor seam and the default `issue` extractor with exact lexical boundaries over raw message bytes.
- [x] 3.2 Add canonical TaskKey identity, mandatory ordered match provenance, deduplication, and fail-closed overlap detection.

## 4. Canonical range, paths, and identity

- [x] 4.1 Implement `Reachable(to) \ Reachable(from)` with canonical commit ordering and metadata-only merge handling.
- [x] 4.2 Implement strict UTF-8 Git path decoding, scalar-value ordering, and root-commit empty-tree deltas over a subtree-skipping tree diff.
- [x] 4.3 Implement baseline same-path identity across the whole analyzed range.

## 5. Rename lineage and file events

- [x] 5.1 Detect local exact-rename candidates and build the endpoint-overlap component graph.
- [x] 5.2 Collapse only uniquely ancestry-ordered, endpoint-linked, lifecycle-clean components and retain every candidate as ordered provenance.
- [x] 5.3 Emit canonical file events with exact-rename zero churn, `binary_or_unavailable` handling, and raw-LF/LCS text churn.

## 6. CLI surface

- [x] 6.1 Add the `history ingest` command module with authored operands, JSON and text formats, and deterministic canonical JSON bytes.
- [x] 6.2 Add the stable fail-closed diagnostic surface with non-zero exit codes and no partial result.

## 7. Verification and synchronization

- [x] 7.1 Add tests covering ref resolution, raw metadata, TaskKey vectors, reachability, path strictness, rename lineage vectors, churn vectors, fail-closed behavior, and empty range.
- [x] 7.2 Run repository formatting, focused test suites, and architecture policy validation.
- [x] 7.3 Synchronize the specification with the implementation and archive the change.
