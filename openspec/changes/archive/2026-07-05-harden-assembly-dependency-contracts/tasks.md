## 1. Evidence hardening

- [x] 1.1 Change `CheckAssemblyDependencyContract` evidence from `sourceAssembly.Location` to `"{Source} -> {Forbidden}"`
- [x] 1.2 Update `AssemblyDependencyContractTests` to assert the new deterministic evidence

## 2. Explicit dependency_depth field

- [x] 2.1 Add `DependencyDepth` (`DependencyDepthMode`, default `Direct`) to `ArchitectureAssemblyDependencyContract`
- [x] 2.2 Add `DependencyDepth` (`DependencyDepthMode`, default `Direct`) to `ArchitectureAssemblyAllowOnlyContract`
- [x] 2.3 Reject `dependency_depth: transitive` at policy-load time in `ValidateAssemblyDependencyContracts` with an actionable error
- [x] 2.4 Reject `dependency_depth: transitive` at policy-load time in `ValidateAssemblyAllowOnlyContracts` with an actionable error
- [x] 2.5 Add `dependency_depth` property (`direct`/`transitive` enum, default `direct`) to both `$defs.assemblyDependencyContract` and `$defs.assemblyAllowOnlyContract` in `schema/dependencies.arch.schema.json`

## 3. Tests

- [x] 3.1 `ArchitectureAssemblyDependencyContract.DependencyDepth` defaults to `Direct`
- [x] 3.2 `ArchitectureAssemblyAllowOnlyContract.DependencyDepth` defaults to `Direct`
- [x] 3.3 Explicit `dependency_depth: direct` loads successfully for both families
- [x] 3.4 `dependency_depth: transitive` fails policy loading with an actionable error for both families
- [x] 3.5 Confirm `assembly_allow_only` direct-only, declared-assembly-scoped semantics remain unchanged (no new tests needed; existing coverage unmodified)

## 4. Docs

- [x] 4.1 Update `docs/contracts/assembly-dependency.md` with `dependency_depth` field and evidence format
- [x] 4.2 Update `docs/reference/yaml-schema.md` assembly dependency/allow-only sections
- [x] 4.3 Update `docs/policy-format/supported-capabilities.md`
- [x] 4.4 Update `docs/ai/policy-authoring-guide.md`

## 5. Validation

- [x] 5.1 Run `make fmt`
- [x] 5.2 Run `make acceptance`; document any reproduced pre-existing environment blocker
- [x] 5.3 Run `openspec validate --all`
- [x] 5.4 Run self-policy dogfooding (`--mode strict` and `--mode audit`)
- [x] 5.5 Run `openspec archive harden-assembly-dependency-contracts`
