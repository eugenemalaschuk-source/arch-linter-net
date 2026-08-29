## Context

See proposal.md. The applicability evaluator is the shared collection-integrity
boundary; its current representative selection and missing-record handling
permit non-deterministic or incomplete evidence. The CLI separately maps a
transport-only completion result to a process exit code.

## Goals / Non-Goals

**Goals:**

- Make missing and duplicate collection evidence deterministic and fail closed.
- Preserve the complete provenance of each malformed duplicate source.
- Ensure a completion status can never improve an ordinary failed result.

**Non-Goals:**

- Add a family-specific YAML schema or evaluator.
- Change the reviewed public API or normalized finding projection.

## Decisions

- Order duplicate expected entries by stable value attributes and use the first
  only as a deterministic display representative; emit one integrity reason for
  every expected provenance. This preserves the existing one-row-per-identity
  projection without allowing enumeration order to select it.
- Emit one duplicate-record integrity reason per ordered record provenance.
  Retaining no representative record avoids treating malformed cardinality as a
  usable state while giving hosts exact source evidence.
- Treat every missing produced record as integrity failure. Optionality governs
  the required ratio, not permission to omit collection evidence; an explicit
  `not_applicable` record expresses intentional input absence.
- Keep completion `unassessable` as the only special CLI exit override. A
  completion `pass` must agree with the already-derived ordinary result.

## Risks / Trade-offs

- [More reasons for duplicate data] → Stable ordering keeps Human/JSON/SARIF
  output reproducible and provides the evidence needed to repair producers.
- [Existing test expectation changes] → Focused regressions encode the #505
  collection contract and protect the new fail-closed behavior.
