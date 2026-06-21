# Timings

Use `--timings` to capture phase-level validation timings while investigating performance or comparing branches.

```bash
arch-linter-net --strict --timings
```

The timing report is written to stderr. This keeps stdout available for human diagnostics or JSON output.

## JSON plus timings

```bash
arch-linter-net --strict --json --timings \
  > architecture-violations.json \
  2> architecture-timings.txt
```

## Sample output

```text
Validation timings:
  total                                      452 ms

  load_and_setup                              51 ms
    yaml_loading                              12 ms
    root_resolution                            3 ms
    condition_set_resolution                   2 ms
    assembly_resolution                       34 ms

  configuration_check                          8 ms

  contract_checks                            389 ms
    dependency                 count=1         9 ms
    layer                      count=1        12 ms
    allow_only                 count=0         0 ms
    cycle                      count=1        20 ms
    method_body                count=2       345 ms

  post_processing                              2 ms
```

## Interpretation

Timing values are machine-dependent and should not be used as golden test data. The useful parts are:

- relative phase cost;
- contract family count;
- before/after comparison on the same machine;
- evidence for performance follow-up work.

`--timings` is a measurement feature, not a performance optimization by itself.
