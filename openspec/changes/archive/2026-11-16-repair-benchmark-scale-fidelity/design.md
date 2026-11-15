# Design

## Context

The benchmark evidence composes the existing `analysis-profile/v1` payload; that profile must remain unchanged. Metric results are currently absent from the benchmark sample model, while fixture inputs and History generators already expose the seams needed to correct the scale shapes.

## Goals / Non-Goals

**Goals:** Make each reviewed dimension execute the intended work, expose deterministic output checks for computed metrics and SARIF, and validate History graph/rename structure from generated records.

**Non-Goals:** Change production metric semantics, alter public APIs, introduce a second benchmark framework, or claim performance improvement from the repaired harness.

## Decisions

- Add a small optional metric-work result to each benchmark sample and its v1 evidence schema. Record count, completion, deterministic identity, and returned metric values. Call `Measure()` inside the measured interval only for metric-rule workloads; fail closed if the count or evaluation status is wrong. Keep `analysis-profile/v1` untouched.
- Keep `contracts_per_workload` as the ordinary policy contract count. The inventory total remains ordinary contracts plus method-body contracts plus graph contracts.
- Give report workloads a constant 32 findings, cycling through all declared rule IDs. This keeps serialization's result volume controlled while the rule table grows.
- Model the merged DAG as a linear mainline plus distinct side-branch commits. Each merge commit has its preceding mainline commit and its side-branch commit as parents; each side branch descends from an earlier mainline fork. Generate queries for side-branch commits against the tip. Model the rename chain with one explicit continuous path transition per declared event.
- Hash canonical source and configuration inputs, excluding generated `bin`/`obj` build outputs while retaining explicitly staged assemblies and receipts as analysis inputs. Staged builds map their temporary source root to a stable virtual path so the copied PE and receipt bytes are reproducible. The evidence identity is a workload-input identity, not an identity for incidental compiler outputs.

Alternatives considered: placing metric output in `analysis-profile/v1` would break the profile contract; letting findings scale with rule count would confound report timings; and hashing built DLLs would make source-input identity sensitive to compiler/path metadata.

## Risks / Trade-offs

- The metric evidence field extends `benchmark-evidence/v1`; readers that reject unknown fields may need a schema update. → The schema remains versioned and additive; validate all retained samples against it and preserve the raw analysis profile separately.
- More realistic History inputs may increase deterministic input size. → Keep event and query cardinalities bounded by the existing catalog and assert generated counts before any production benchmark consumes them.
- Generated `bin`/`obj` outputs are excluded, while staged analysis assemblies remain in the identity. → Verify both real-MSBuild and staged workloads across independent roots so the filter preserves the actual consumer input.
