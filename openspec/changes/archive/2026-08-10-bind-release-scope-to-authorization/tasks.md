## 1. Release-scope closure

- [x] 1.1 Declare the authoritative required and excluded release-scope items in the repository.
- [x] 1.2 Resolve current issue state bound to the candidate manifest and source commit.
- [x] 1.3 Refuse PASS while any required item is open and emit the inventory in the evidence.

## 2. Consumer-cleanup oracles

- [x] 2.1 Prove added, removed, and changed public signatures through the reviewed snapshot
  lifecycle and prove `update` restores sync.
- [x] 2.2 Add the packaged Testing `WithEnsureBuilt()` back-to-back regression and widen the
  preservation oracle to every selected primary output.

## 3. Verification

- [x] 3.1 Run the packed gate, `make fmt`, `make acceptance`, and strict OpenSpec validation.
