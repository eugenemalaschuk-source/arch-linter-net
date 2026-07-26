# Design — reusable source sets and deterministic contract expansion

## Decision 1 — One document-level `source_sets` map, not one mechanism per family

`source_sets` is a top-level map keyed by set name, mirroring `layers`, `packages`, and
`framework_references`. Every family reuses the same declarations, so a new family is one table
row in the expander rather than a new expansion mechanism.

Each set declares a `kind`, which fixes both the identity domain and the glob-resolution universe:

| `kind`     | member identity      | glob universe               |
| ---------- | -------------------- | --------------------------- |
| `assembly` | target assembly name | `analysis.target_assemblies`|
| `layer`    | declared layer key   | `layers` keys               |
| `project`  | project path         | `analysis.projects`         |

The universe is always a list the policy already declares. A set therefore cannot pull in arbitrary
repository outputs, and expansion stays a pure function of the composed policy document — no build,
no discovery, no filesystem.

## Decision 2 — Expansion runs at load time, after provenance binding, before validation

`ArchitecturePolicyDocumentLoader.Load` binds provenance against the authored YAML node paths, then
expands, then runs the validator pipeline. Two consequences:

- Every existing validator, the contract catalog, the executor, baselines, coverage, `explain`, and
  the reporters see ordinary single-source contracts and need no expansion awareness.
- Expanded instances are new objects, so the expander re-binds each one to its authored contract's
  location through `ArchitecturePolicyProvenanceIndex.BindExpandedContract`, the same aliasing seam
  `BindCatalogContract` already uses for expanded layer templates. Imported fragments keep both the
  authored location and the effective composed location.

## Decision 3 — Instance identity is `<authored-id>/<normalized-source>`

`LayerTemplateExpander` already derives `<template-id>/<container>` ids for expanded instances, and
duplicate-id validation, baseline identity, and contract-id selection all key on the contract id.
Reusing that convention keeps per-source diagnostics and baseline entries distinct without changing
any identity model.

The authored id is not lost: each instance carries an `ExpansionOrigin` naming the authored contract
id, the set that produced it (if any), and the exact selector (literal member or glob) that matched.
Contract selection and rule-input coverage `contract_ids` accept the authored id and resolve it to
every instance, so a policy author never has to name a derived id.

## Decision 4 — Fan-out for singular sources, inline union for list-shaped fields

A family whose source is a single value (`source:`) fans out into one contract instance per resolved
source, which is what makes per-source findings and per-source baseline identities possible. A family
whose field is already a list (`projects:`, `allowed_only_in_assemblies:`) takes a companion
`*_sets:` key whose members are unioned into that list; fanning out there would invent findings the
family does not have.

## Decision 5 — Fail closed, and make the one exception explicit

Zero resolved sources, an unknown set name, a glob whose universe is undeclared, a member outside
the declared target set, and an expansion above the bounded instance limit are all load-time errors
with the authored location attached. The single exception is a set that declares `optional: true`
with a non-empty `reason`, following the same exact-identity-plus-mandatory-reason rule as optional
rule inputs: contracts referencing it expand to zero instances and are recorded in the expansion
inventory as optional-empty instead of vanishing silently.

## Decision 6 — Determinism and bounds

Resolved sources are deduplicated ordinally and sorted ordinally, so overlapping sets and repeated
members produce one instance and one diagnostic. Expansion is capped at 500 instances per authored
contract; exceeding it is an actionable error rather than an unbounded run.

## Decision 7 — The inventory is data, not display text

`ArchitectureSourceExpansionInventory` is a typed model on the document. The coverage inventory
exposes it, `explain` projects it to human and JSON output, and the CI JSON and SARIF payloads carry
it as structured fields, so a machine consumer proves the resolved expansion without parsing display
text.
