# Policy Import Design Examples

> These files describe the format selected by issue #280. They are candidate
> fixtures for #281 and #282 and are not executable with the current loader.

## Positive examples

- [`recommended`](recommended/architecture/arch.yml) uses `arch.yml` and
  `*.arch.yml` conventions, root-inline content, and a nested fragment.
- [`alternative`](alternative/config/policy.custom.yaml) uses arbitrary root
  and fragment filenames with equivalent graph-role semantics.
- [`two-stage`](two-stage/root.yml) demonstrates a root source that contributes
  only identity/imports while fragments make the effective policy complete.
- [`classification`](classification/root.yml) demonstrates aggregate
  `classification.path` deferred support across root and fragment sources.

## Negative examples

The [`negative`](negative/README.md) directory defines file sets for forbidden
fragment fields, duplicate definitions and IDs, cycles, boundary escapes,
unsupported import expressions, portable-path grammar failures, and graph-limit
fixtures.

The paths and document names are test data only. No test may infer root or
fragment role from them.
