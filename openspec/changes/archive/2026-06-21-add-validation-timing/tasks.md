## 1. CLI Flag and Infrastructure

- [x] 1.1 Add `--timings` boolean flag to argument parsing in `Program.cs`
- [x] 1.2 Create `TimingCollector` helper class (or local functions) to manage `Stopwatch` instances, phase names, and timing accumulation
- [x] 1.3 Create timing output formatter that writes aligned columnar table to stderr

## 2. Pipeline Instrumentation

- [x] 2.1 Instrument `load_and_setup` phase with sub-phases: `yaml_loading`, `root_resolution`, `condition_set_resolution`, `assembly_resolution`
- [x] 2.2 Instrument `configuration_check` phase
- [x] 2.3 Instrument `contract_checks` parent phase wrapping all contract-type loops
- [x] 2.4 Instrument each contract family: `dependency`, `layer`, `allow_only`, `cycle`, `method_body`, `asmdef`, `independence`, `protected`, `external`, `acyclic_sibling` — with `count=N` tracking
- [x] 2.5 Instrument `post_processing` phase (unmatched ignored violations)
- [x] 2.6 Capture and report total wall-clock duration

## 3. Edge Cases

- [x] 3.1 Verify `--json --timings` — valid JSON on stdout, timing on stderr
- [x] 3.2 Verify `--audit --timings` — audit contract selection, same phase structure
- [x] 3.3 Verify `--contract <id> --timings` — selected contracts timed, unselected show count=0
- [x] 3.4 Verify error path (bad args, missing file) exits with code 2 and no timing output
- [x] 3.5 Verify zero contracts of a type appear as `count=0` and `0 ms` for deterministic shape

## 4. Tests

- [x] 4.1 Add test: `--strict --timings` prints timing header and phase names to stderr
- [x] 4.2 Add test: `--strict --timings` exit code matches `--strict` (invariant)
- [x] 4.3 Add test: `--strict --json --timings` stdout is valid JSON
- [x] 4.4 Add test: `--strict` stdout is identical with and without `--timings`
- [x] 4.5 Add test: zero-contract phase shows `count=0` in stderr output

## 5. Documentation

- [x] 5.1 Add "Timing baseline" section to `docs/cli/index.md`
- [x] 5.2 Include example invocations: `--strict --timings`, `--audit --timings`, `--json --timings`
- [x] 5.3 Document that this is a baseline measurement tool for performance planning (#19), not performance optimization itself

## 6. Final Verification

- [x] 6.1 Run `rtk make acceptance` and confirm all tests pass
- [x] 6.2 Run `arch-linter-net --policy architecture/dependencies.arch.yml --strict --timings` and capture baseline timing for reference in #19
- [x] 6.3 Run `arch-linter-net --policy architecture/dependencies.arch.yml --strict` without `--timings` and confirm output unchanged
