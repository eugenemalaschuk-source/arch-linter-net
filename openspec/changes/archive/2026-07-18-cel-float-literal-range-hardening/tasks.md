## Tasks

- [x] Mine `google/cel-go` `parser/parser_test.go` and `parser/unescape_test.go` for scenario
      families not already covered.
- [x] Mine `google/cel-java` `parser/src/test/resources/parser_errors.baseline`.
- [x] Mine `projectnessie/cel-java` `ParserTest.java` (confirmed largely redundant with cel-go's
      own table — same upstream corpus, ported).
- [x] Mine four independent .NET CEL ports (`rayokota/cel.net`, `telus-labs/cel-net`,
      `plaisted/cel-compiled`, `hbjydev/celdotnet`) for C#-specific parser/lexer edge cases.
- [x] Fix the float-literal-magnitude-overflow defect (category B/C boundary: a
      syntactically-plausible literal whose value cannot be represented must fail, not silently
      round to Infinity).
- [x] Add provenance-tagged regression tests for the fix and for confirmed-correct behaviors
      found during this pass.
- [x] Update `cel-profile-v1` spec.
- [x] Update the corpus mining manifest.
- [x] Run `rtk make fmt` and `rtk make acceptance`.
