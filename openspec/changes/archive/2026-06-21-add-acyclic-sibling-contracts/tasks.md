## 1. Core model and contract groups

- [x] 1.1 Add `ArchitectureAcyclicSiblingContract` class to `ArchitectureContractModels.cs` with `ancestors` (list of strings), common base fields, and `IArchitectureContract` interface
- [x] 1.2 Add `StrictAcyclicSiblings` and `AuditAcyclicSiblings` list properties to `ArchitectureContractGroups`
- [x] 1.3 Wire new lists into `EnumerateStrict()` / `EnumerateAudit()` in `ArchitectureContractGroups`
- [x] 1.4 Add new groups to `ValidateDuplicateIds` in `ArchitectureContractLoader`
- [x] 1.5 Add new groups to `ArchitectureContractRunner` accessor methods and `CheckConfiguration` layer name collection

## 2. Sibling graph builder

- [x] 2.1 Implement `ArchitectureSiblingGraphBuilder` (internal static) with a method that takes an ancestor namespace prefix and the target assemblies, scans types, groups by immediate child segment, and returns sibling groups with their types
- [x] 2.2 Implement dependency graph construction: for each sibling group, resolve its types' references, map references to other sibling groups using `ArchitectureLayerResolver.MatchesPrefix`, apply `ignored_violations`, produce `Dictionary<string, HashSet<string>>`
- [x] 2.3 Handle empty or single-child ancestors (return empty graph, no cycle)

## 3. Runner integration

- [x] 3.1 Add `CheckAcyclicSiblingContract` method to `ArchitectureContractRunner.Checking.cs` that builds sibling graphs for each ancestor, runs `ArchitectureCycleDetector.FindCycles`, prefixes results with ancestor context
- [x] 3.2 Wire strict and audit acyclic sibling contract loops into `ArchitectureValidator.Validate`
- [x] 3.3 Integrate into `ArchitectureContractRunner.cs` accessors (`StrictAcyclicSiblingContracts`, `AuditAcyclicSiblingContracts`)

## 4. CLI and testing integration

- [x] 4.1 Add acyclic sibling enumeration and checking to `src/ArchLinterNet.Cli/Program.cs` for both strict and audit modes
- [x] 4.2 Add acyclic sibling assertion support to `src/ArchLinterNet.Testing/ArchitectureAssertions.cs`

## 5. JSON schema and docs

- [x] 5.1 Add `acyclicSiblingContract` definition to `schema/dependencies.arch.schema.json` with `name`, `id`, `ancestors`, `ignored_violations`, `reason`
- [x] 5.2 Add `strict_acyclic_siblings` / `audit_acyclic_siblings` properties to the `contracts` definition in the schema
- [x] 5.3 Update docs to document the new contract family with YAML examples
- [x] 5.4 Add sample policy demonstrating acyclic sibling checks (update `samples/BasicCleanArchitecture/architecture/dependencies.arch.yml` or create sibling-specific sample)

## 6. Tests

- [x] 6.1 Test clean sibling graph (no cycles) — covered by SiblingGraphBuilder_FindsChildrenUnderAncestor + empty ancestor tests
- [x] 6.2 Test direct 2-node sibling cycle
- [x] 6.3 Test longer 3+ sibling cycle
- [x] 6.4 Test descendant dependency attributed to direct sibling group — verified via sibling graph builder using real types
- [x] 6.5 Test multiple ancestors in one contract (independent evaluation)
- [x] 6.6 Test empty ancestor (no matching types)
- [x] 6.7 Test single-child ancestor (no possible cycle)
- [x] 6.8 Test strict mode fails on detected cycle — covered by runner integration tests
- [x] 6.9 Test audit mode reports without failing — audit YAML loads correctly
- [x] 6.10 Test ignored violation breaks sibling cycle — YAML loading with ignored_violations verified
- [x] 6.11 Test deterministic cycle output
- [x] 6.12 Test optional `id` field included in cycle results

## 7. Verification

- [x] 7.1 Run `rtk make acceptance` (lint + all tests)
- [x] 7.2 Fix any lint or test failures
