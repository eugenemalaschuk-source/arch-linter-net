## Context

The tool supports two scanning modalities for dependency analysis:
- **Reflection-based** (most contract types) — loads compiled assemblies and crawls type references
- **Source+IL** (method body contracts) — parses C# source with Roslyn and also decodes IL bytecode

Neither modality currently accounts for `#if` / `#elif` / `#else` preprocessor directives. The Roslyn scanner calls `CSharpSyntaxTree.ParseText()` with default options (no symbols defined), meaning all conditional branches are parsed as if `#if SYMBOL` is false. The IL scanner sees whatever was compiled.

## Goals / Non-Goals

**Goals:**
- Policy-level control over preprocessor symbols for Roslyn source analysis
- Named condition sets defined in YAML, selectable via CLI `--condition-set <name>`
- Optional default condition set for runs without explicit selection
- Deterministic, reproducible diagnostics under any symbol set
- Backward compatibility — existing policies with no condition configuration produce identical output
- Update JSON schema, docs, examples, and AI-facing guidance

**Non-Goals:**
- Full build matrix or multi-condition-set runs per invocation
- `--define` inline symbol overrides (deferred)
- Per-contract condition filtering (deferred)
- Reinterpreting compiled assemblies under different symbols (reflection/IL scanners unaffected)
- Unity project import or Unity Editor execution

## Decisions

### D1: Condition sets live under `analysis` in the YAML

**Chosen:** `analysis.condition_sets` and `analysis.default_condition_set`

**Rationale:** The `analysis` block already contains scanner/runtime inputs (target assemblies, search paths, source roots, unmatched-ignore behavior). Preprocessor symbols are another scanner input, not a contract family. A top-level `conditions` key would imply they are a first-class architectural concept.

**Rejected:** Top-level `conditions:` key, or per-contract `conditions:` field.

### D2: One condition set per invocation

**Chosen:** `--condition-set <name>` selects exactly one named set per run.

**Rationale:** Keeps output, exit codes, JSON, and ignore tracking simple. CI can run multiple invocations (e.g., `--condition-set runtime && --condition-set editor`). Future `--condition-set all` or matrix reporting is follow-up scope.

**Rejected:** Multi-condition-set invocation in a single run.

### D3: Condition sets affect Roslyn source scanning only

**Chosen:** Only `ArchitectureSourceScanner.FindMethodBodyViolations` receives symbols. Reflection-based scanners (`ArchitectureReferenceScanner`, `ArchitectureTypeScanner`) and the IL scanner (`ArchitectureIlMethodBodyScanner`) are unchanged.

**Rationale:** Reflection and IL scanners work on compiled assemblies — they see what was compiled, and the issue explicitly excludes recompilation or build-system modeling. Symbols cannot retroactively change what an assembly contains. Honest and deterministic: source analysis uses the configured symbols, assembly analysis uses the provided binaries.

### D4: Unknown condition set name → exit code 2

**Chosen:** If the resolved name (CLI arg or default) is non-empty and not found in `analysis.condition_sets`, the tool exits with code 2 and lists available sets.

**Rationale:** Matches existing invalid-argument handling (unkown contract IDs, invalid mode, missing files). Provides a clear error message with available options.

**Rejected:** Silently falling back to empty symbols (would mask configuration errors).

### D5: Resolution order — CLI arg, then default, then empty

**Chosen:**
```
1. --condition-set CLI arg (if provided)
2. analysis.default_condition_set (if non-empty)
3. empty list [] (backward-compatible default)
```

**Rationale:** CLI takes precedence over policy default, which takes precedence over no symbols. This gives the user explicit control while allowing policies to declare a sensible default.

### D6: No diagnostic change for violations

**Chosen:** Violations do not carry the condition set name. The condition set is a scanning parameter, not a dimension of the diagnostic output.

**Rationale:** The CLI invocation already documents which condition set was used (visible in CI logs, command history). Adding it to every violation line would add noise without value. Diagnostics remain deterministic across modes — the same source under the same symbols produces the same violations.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Users expect condition sets to filter reflection-based contracts too | Document explicitly: "Condition sets affect Roslyn/source method-body scanning only. Reflection/IL scanners analyze the assemblies provided to the run." |
| Policy defines a `default_condition_set` that references a nonexistent key | This is an error (exit code 2). The loader could validate this eagerly, but deferring to first use is consistent with how contract IDs are validated. |
| Users mix condition set A with assemblies compiled under condition set B and get confusing results | Document that assemblies are analyzed as-compiled; source scanning respects configured symbols. The user is responsible for providing the right assemblies for the analysis they want. |
| No `--define` inline override limits CI flexibility | Deferred — can be added later without breaking changes. Policy-first approach keeps symbols documented and reproducible. |
