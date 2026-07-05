## 1. Schema tightening

- [x] 1.1 Narrow `dependency_depth` enum to `["direct"]` in `$defs.assemblyDependencyContract`
- [x] 1.2 Narrow `dependency_depth` enum to `["direct"]` in `$defs.assemblyAllowOnlyContract`

## 2. Defensive runtime guard

- [x] 2.1 Add `RequireDirectDependencyDepth` helper in `ArchitectureAnalysisSession.AssemblyDependency.cs`
- [x] 2.2 Call it from `CheckAssemblyDependencyContract`
- [x] 2.3 Call it from `CheckAssemblyAllowOnlyContract`

## 3. Tests

- [x] 3.1 Programmatic `ArchitectureAssemblyDependencyContract` with `DependencyDepth: Transitive` throws at check time
- [x] 3.2 Programmatic `ArchitectureAssemblyAllowOnlyContract` with `DependencyDepth: Transitive` throws at check time

## 4. Docs

- [x] 4.1 Update `docs/reference/yaml-schema.md` to describe schema + loader + defensive runtime enforcement

## 5. Validation

- [x] 5.1 Run `make fmt`
- [x] 5.2 Run `make acceptance`; document any reproduced pre-existing environment blocker
- [x] 5.3 Run `openspec validate --all`
- [x] 5.4 Run self-policy dogfooding (`--mode strict` and `--mode audit`)
- [x] 5.5 Run `openspec archive fix-assembly-dependency-review-findings`
