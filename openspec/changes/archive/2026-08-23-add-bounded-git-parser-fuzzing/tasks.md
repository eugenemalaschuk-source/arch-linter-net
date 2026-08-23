## 1. Core parser seams and contract tests

- [x] 1.1 Extract internal byte-array adapters for the selected loose-object, v2 pack-index, pack-entry, and REF-delta parser seams without changing shipping behavior.
- [x] 1.2 Add focused NUnit coverage for canonical and fail-closed results, including both digest lengths for every digest-sensitive seam.
- [x] 1.3 Record the reviewed non-shipping fuzz friend assembly and tool-project exclusion in architecture policy and its supporting documentation.

## 2. Executable harness and corpus

- [x] 2.1 Add the .NET 10 console harness with SharpFuzz integration, a pre-dispatch 1 MiB input cap, and deterministic replay/materialization modes.
- [x] 2.2 Add public-safe textual synthetic seed sources for all selected seams and deterministic materialization to binary campaign inputs.
- [x] 2.3 Add deterministic regression tests that replay every seed and an oversized input under both relevant digest modes.
- [x] 2.4 Add synthetic `OBJ_OFS_DELTA` reconstruction, corpus coverage, and both-digest regression tests.

## 3. Campaign operation and documentation

- [x] 3.1 Add the pinned scheduled/manual AFL++ workflow with fixed time, memory, CPU, no-network, and finite-duration containment.
- [x] 3.2 Document local toolchain verification, replay, minimization, corpus ownership, artifact retention, and confirmed-finding promotion.
- [x] 3.3 Smoke the fixed-limit workflow path with synthetic inputs and inspect the resulting containment behavior.
- [x] 3.4 Make `--replay` launch a child under the 100 ms/512 MiB envelope and cover the launcher contract with an acceptance regression.
- [x] 3.5 Set `AFL_HANG_TMOUT=100`, run the AFL++ container as the host UID/GID, and report/remove findings without uploading raw inputs.
- [x] 3.6 Cover the hexadecimal managed-heap limit and the macOS `ulimit -v` replay launcher in the acceptance contract.

## 4. Verification and lifecycle completion

- [x] 4.1 Run focused Core and harness tests, formatting, architecture/lint checks, and strict OpenSpec validation.
- [x] 4.2 Synchronize implementation and specifications, archive the OpenSpec change, and validate all specifications.
- [x] 4.3 Commit, push the issue branch, and open one pull request that closes #623 with the exact local validation evidence.
