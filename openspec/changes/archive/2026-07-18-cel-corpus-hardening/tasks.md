## Tasks

- [x] Mine the pinned `cel-expr/cel-spec` conformance corpus (`tests/simple/testdata/parse.textproto`)
      for parser/tokenizer scenario families not already covered by the existing test suite.
- [x] Classify found scenarios (A: supported, B: valid-but-unsupported, C: invalid, D: implementation-
      specific, E: future-profile research) and record provenance.
- [x] Fix the triple-quoted-string mis-tokenization defect (category B: clean rejection required).
- [x] Add provenance-tagged regression tests for the fix and for confirmed-correct existing behavior
      (octal escapes, keyword casing, string-prefix/identifier disambiguation).
- [x] Update `cel-profile-v1` spec to state the required rejection shape.
- [x] Record the scenario manifest in `docs/internal/`.
- [x] Run `rtk make fmt` and `rtk make acceptance`.
