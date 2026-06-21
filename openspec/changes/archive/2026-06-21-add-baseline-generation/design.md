## Context

ArchLinterNet already supports `ignored_violations` per contract, unmatched ignore alerting (stale detection), and glob-based ignore matching. There is no persistence layer — validation evaluates fresh against the code every time. Adding adoption in existing repositories requires freezing current violations without hand-authoring every ignore entry.

Decision context: the design was explored interactively in GitHub issue #44, resulting in the decisions documented here.

## Goals / Non-Goals

**Goals:**
- Add `baseline generate` CLI subcommand that snapshots current violations into a separate baseline file
- Add `--baseline` flag to `validate` for consuming a baseline file during validation
- Reuse existing `ArchitectureIgnoreMatcher`, `ArchitectureIgnoreUsageTracker`, and unmatched-ignore-alerting without modification
- Generate deterministic output: identical input → identical baseline, no timestamp
- Support all contract types that have `ignored_violations` (strict, audit, layers, cycles, allow-only, independence, protected, external, method-body, acyclic-sibling)
- Cover cycle and acyclic-sibling contracts by collecting baseline candidates before graph aggregation
- Provide a separate JSON schema for the baseline file format

**Non-Goals:**
- Auto-editing the main policy file (separate command only)
- YAML `$include` mechanism for baseline files (explicit `--baseline` flag only)
- Glob generalization: only exact `(source_type, forbidden_reference)` pairs
- Baseline management UI or dashboard
- Broad wildcard ignores that hide new violations
- Replacing manual curated ignores (baseline is additive, not a replacement)

## Decisions

### Decision: Separate `ArchitectureBaselineDocument` model

The baseline file has a distinct structure: `baseline.<group>[].{id, ignored_violations}` — no layers, no analysis config, no contract definitions. A dedicated model avoids polluting `ArchitectureContractDocument` with optional fields and enables separate schema validation.

```yaml
version: 1
baseline:
  strict:
    - id: "cli-must-not-depend-on-testing"
      ignored_violations:
        - source_type: "MyApp.Cli.Command"
          forbidden_reference: "MyApp.Testing.TestHarness"
          reason: "generated baseline"
```

**Alternatives considered:**
- Reusing `ArchitectureContractDocument` with optional fields: rejected — would require making required fields optional in the policy schema, risking incomplete policy acceptance
- Using `contracts:` instead of `baseline:` as root key: rejected — this is a patch document, not a policy document, and would require schema gymnastics

### Decision: Baseline candidate collection via runner-side accumulator

A `List<ArchitectureBaselineCandidate>` in the runner collects `(contractId, contractGroup, sourceType, forbiddenReference)` pairs at each `IsIgnored` call site where the call returns `false` (i.e., the pair IS a violation). This mirrors the existing `_unmatchedIgnoredViolations` collector pattern.

For cycle and acyclic-sibling contracts, candidates are collected before `graph[source].Add(target)` — the same point where `IsIgnored` is already called. This preserves type-level edge pairs that would otherwise be lost after graph aggregation.

**Alternatives considered:**
- Post-processing violations and iterating `.ForbiddenReferences`: rejected — cycle contracts don't expose edge pairs in the final result
- Dedicated flag on `ArchitectureIgnoreMatcher.IsIgnored` to record pairs: rejected — would couple static utility to change-specific concern; collector lives in the runner

### Decision: Merge at load time, not at scan time

When `--baseline` is provided, the baseline file is loaded into an `ArchitectureBaselineDocument`, then entries are merged into the `ArchitectureContractDocument.IgnoredViolations` lists before the runner is constructed. The runner sees a unified set of ignores and operates unchanged.

**Merge algorithm:**
1. For each baseline entry in `baseline.<group>`:
   a. Find matching contract in `document.Contracts.<Group>` by `Id` (case-insensitive, same as `--contract` filter)
   b. Append baseline `ignored_violations` entries to `contract.IgnoredViolations`
   c. Deduplicate by `(source_type, forbidden_reference)` preserving first occurrence
2. If any baseline `id` has no matching contract, report error and exit

### Decision: Dedicated baseline JSON schema

A new file `baseline.schema.json` (adjacent to the existing `dependencies.arch.schema.json`) defines the baseline format. It is NOT referenced from the policy schema.

The baseline schema defines:
- Root: `version` (int) + `baseline` (object)
- `baseline` properties: contract group names (same set as policy)
- Each group entry: `id` (required) + `ignored_violations` array (reusing the existing `ignoredViolation` definition from the policy schema)

### Decision: Default reason is deterministic

`reason: "generated baseline"` by default. No timestamp, no hash. Optional `--reason` flag overrides.

The reason field is informational only — not used in ignore matching or deduplication. This keeps output stable across runs and avoids creating a false sense of identity binding.

## Risks / Trade-offs

- **[Risk]** Large baselines: a project with many violations could generate hundreds of entries. Each entry is an exact `(source_type, forbidden_reference)` pair. → [Mitigation] The file is machine-generated; AI and humans only review diffs. If verbosity becomes a problem, glob-aware generation can be added later (tracked in #46).
- **[Risk]** Contract ID changes break baseline: if someone renames a contract in the policy, the auto-generated fallback ID changes (derived from `name`), and baseline entries for that contract silently stop matching. → [Mitigation] This is correct behavior: stale baseline entries are caught by the existing unmatched-ignore-alerting system. The user regenerates the baseline after renaming.
- **[Risk]** Cycle baseline loses precision: a cycle between layers A→B→C→A involves multiple type-level edges. If only some edges are fixed, only the remaining edges trigger violations. → [Mitigation] The candidate collector captures each edge individually before graph aggregation, so the baseline has type-level granularity, just like dependency contracts.
- **[Risk]** Schema drift between baseline and policy contract groups: adding a new contract type to the policy schema (e.g., a future `strict_foo`) would need a corresponding baseline group. → [Mitigation] Acceptable for now; the generator simply doesn't emit entries for unknown groups, and the loader reports unknown IDs as errors.
