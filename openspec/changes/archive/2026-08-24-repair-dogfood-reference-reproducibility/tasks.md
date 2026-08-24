## 1. Retain deterministic evidence

- [x] 1.1 Add the released-tool Git-only JSON artifact and revise the evidence
  record with its path, byte size, digest, and separate advisory enrichment
  observation.
- [x] 1.2 Add a streaming checksum verifier and make `lint-docs`/CI reject an
  artifact whose bytes do not match the digest in the evidence record.

## 2. Repair the public workflow

- [x] 2.1 Install ArchLinterNet.Cli 0.7.0 in a caller-owned isolated tool
  directory and invoke that executable in every public-guide command.
- [x] 2.2 Separate the Git-only canonical run from optional enrichment and
  document the required clean target-worktree preparation for the advisory run.

## 3. Govern the evidence-linked follow-up

- [x] 3.1 Assign #639 to #630's milestone and standard AI/tooling labels while
  preserving its existing governed task body.

## 4. Validate and finalize

- [x] 4.1 Smoke-test the guide's isolated v0.7.0 tool entrypoint in the clean
  v0.7.0 worktree and verify the artifact digest twice.
- [x] 4.2 Run the targeted tooling/docs/OpenSpec validation, archive the change,
  and confirm the resulting PR diff is scoped to the review repairs.
