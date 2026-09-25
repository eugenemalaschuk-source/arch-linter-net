## Context

See `proposal.md` for motivation. `health --profile` already writes deterministic counters through the shared profile publisher, but the Health service creates and evaluates its snapshot without a `ValidationTiming` instance. The `validate --profile` path already passes one timing object through snapshot creation and mode evaluation.

Health also has a caller-owned snapshot path for `--change-snapshot`. Any timing support must preserve that single-snapshot behavior and must not create a second analysis lifetime. The profile model and JSON schema already support phases and resource measurements; no new output fields or public API are needed.

## Goals / Non-Goals

**Goals:**

- Reuse `ValidationTiming` for Health snapshot setup, strict/audit evaluation, debt-gate comparison, and Health projection when `--profile` is requested.
- Include elapsed/processor phase data and process memory/allocation measurements in the existing profile document.
- Preserve the one-snapshot invariant for composite Health/change output.
- Retain the opt-in profile as a separate artifact from the repository's existing CI Health run so normalized runs can use its inner-boundary measurements.

**Non-Goals:**

- Change analyzer algorithms, policy semantics, result identity/order, or exit categories.
- Change `analysis-profile/v1` fields, schema identity, or the default timing behavior of commands without profile collection.
- Add per-symbol telemetry or publish adopter-specific topology or logs.
- Optimize Health or change the CI orchestration owned by adopter repositories.

## Decisions

1. **Pass timing through Health orchestration using existing types.** Add internal timing-aware overloads for Core Health evaluation and the CLI runtime. Keep existing overloads as null-timing delegates so direct API callers and current fakes retain their behavior. Alternatives such as adding a new profiling API or a Health-specific profile schema would duplicate `ValidationTiming` and require a new compatibility boundary.

2. **Use one timing instance and one snapshot per profiled Health invocation.** The ordinary Health path passes the timing object into snapshot preparation and Health evaluation. The `--change-snapshot` path passes the same timing into its existing caller-owned snapshot and all projections. The timing records the enclosing `total` interval and bounded Health phases for mode evaluation, external-evidence binding, debt-gate comparison, and summary projection; existing Core phases continue to supply detailed preparation and contract timings. This preserves clear phase attribution without changing Core counters.

3. **Capture host measurements only for the existing opt-in profile.** The Health handler records the allocation baseline after argument and destination validation, then supplies allocation and peak-working-set measurements to the shared publisher. Omitted `--profile` leaves timing collection off. The publisher's existing counter-only callers remain unchanged.

4. **Keep phase names low-cardinality and document them.** Add the Health phase names to the analysis-profile dictionary and tests. Do not emit paths, policy identifiers, symbols, or private adopter data in the profile evidence.

5. **Capture the profile in the existing repository Health CI workflow.** Pass a distinct profile path to the already-required Health projection and upload it as a separate artifact when present. This makes future normalized dogfood runs inspectable without changing the Health gate, report JSON, or other workflow projections.

## Risks / Trade-offs

- **Timing adds measurement overhead to profiled runs.** The overhead is opt-in and uses the existing timing implementation; unprofiled calls continue to pass `null`.
- **Profile timing may make a failure path more visible.** The Health outcome, formatter, and exit mapping remain the authorities; timing data is observational and cannot affect them.
- **A Health profile is not by itself consumer-CI attribution.** Container, scheduler, and runner time still require caller-side timestamps, so evidence must retain the inner/outer boundary and avoid claiming a product regression from aggregate spans.
