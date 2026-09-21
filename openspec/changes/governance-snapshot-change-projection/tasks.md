## 1. Core composite seam

- [x] 1.1 Let Health evaluate from a caller-owned immutable snapshot.
- [x] 1.2 Reuse the snapshot's candidate receipt for baseline verification.
- [x] 1.3 Add retained-session graph projections and Core change composition.

## 2. CLI workflow

- [x] 2.1 Add `health --change-snapshot <path>` without changing ordinary Health
  output or exit categories.
- [x] 2.2 Reject output paths that collide with policy or baseline inputs.
- [x] 2.3 Keep standalone `change snapshot` behavior unchanged.

## 3. Correctness evidence

- [x] 3.1 Prove shared and independent snapshots serialize identically.
- [x] 3.2 Add CLI integration coverage for shared Health/change publication.
- [ ] 3.3 Run focused and full Core/CLI suites, formatting, and OpenSpec
  validation.

## 4. Consumer handoff

- [ ] 4.1 Publish the generic seam and package/release identity for adopters.
- [ ] 4.2 Integrate firstice-server with capability-gated use of the new option;
  retain the 0.8.2 fallback until a compatible package is available.
