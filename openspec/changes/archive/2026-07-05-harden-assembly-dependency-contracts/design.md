## Context

`assembly_dependency`/`assembly_allow_only` (PR #184, issue #58) reused `Assembly.Location` as violation evidence for `assembly_dependency`, mirroring `assembly_independence`'s existing pattern. That's a filesystem path — environment-dependent and not directly useful as architecture evidence, unlike the source/forbidden assembly names already carried in `SourceType`/`ForbiddenNamespace`. Separately, the namespace-level `ArchitectureDependencyContract` already exposes `dependency_depth: direct|transitive`; the two new assembly-axis families accepted no such field, leaving the direct-only decision implicit rather than a documented, schema-visible contract.

## Goals / Non-Goals

**Goals:**
- Make `assembly_dependency` violation evidence deterministic and architecture-meaningful: `"{Source} -> {Forbidden}"`.
- Make direct-only explicit in the YAML/runtime model for both assembly-axis families via an optional `dependency_depth` field, defaulting to `direct`.
- Fail policy loading loudly and actionably if `dependency_depth: transitive` is declared on either family, rather than silently ignoring it or misbehaving.

**Non-Goals:**
- Implementing transitive assembly-reference-path resolution — remains a documented follow-up.
- Any change to `assembly_allow_only`'s evaluation semantics (declared-assembly scoping, self-exclusion, allow-list logic).
- Any change to `assembly_independence`, Unity `asmdef`, or namespace-level `dependency`/`allow_only` contracts.
- Renaming existing YAML groups (`strict_assembly_dependency`, etc.) or the `ArchitectureContractExecutor`/catalog/handler dispatch model.

## Decisions

**1. Reuse the existing `DependencyDepthMode` enum and `dependency_depth` field name for both families, rather than a new enum or a different name for allow-only.**
`DependencyDepthMode` (`Direct`/`Transitive`) and the `dependency_depth` YAML alias already exist and are used by `ArchitectureDependencyContract`. Reusing them keeps the field name and enum consistent across every family that exposes a depth concept, rather than introducing a parallel `reference_depth` name for one of the two new families and not the other. YamlDotNet deserializes `"transitive"` into `DependencyDepthMode.Transitive` without error at the deserialization stage; rejection happens at policy-load validation time (mirroring how unresolvable assembly names are already rejected), not at YAML-parse time.

**2. Reject `transitive` at policy-load time with an actionable error, not silently coerce it to `direct` or leave it unenforced.**
Silently treating `transitive` as `direct` would let a policy author believe transitive checking is active when it is not — a worse outcome than a loud failure. This mirrors the existing `ValidateAssemblyDependencyContracts`/`ValidateAssemblyAllowOnlyContracts` pattern of failing fast on authoring mistakes (unresolvable assembly names) rather than deferring to a confusing runtime no-op.

**3. Evidence format `"{Source} -> {Forbidden}"` replaces `Assembly.Location`, only for `assembly_dependency`.**
`assembly_allow_only` already reports evidence as the disallowed assembly names themselves (via `ForbiddenReferences`), which is already deterministic and architecture-meaningful — no change needed there. `assembly_independence` is unaffected (out of scope; it did not need the same immediate hardening this PR review flagged for the new `assembly_dependency` family specifically).

## Risks / Trade-offs

- [Risk] Changing `assembly_dependency` evidence format is a breaking change for any existing consumer parsing `ForbiddenReferences` as a filesystem path. → Mitigation: this family is unreleased (still in an open PR, not yet merged to `main`), so there are no external consumers to break.
- [Risk] Reusing `DependencyDepthMode.Transitive` in the enum without implementing it could tempt a future contributor to assume half-built transitive support exists. → Mitigation: the fail-fast loader rejection and updated docs make the current-vs-planned boundary explicit at both the YAML-authoring and code level.
