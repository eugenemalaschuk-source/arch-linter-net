# Tasks: inheritance-implementation-contracts

## 1. Policy model and loading

- [x] 1.1 Add `ArchitectureInheritanceContract` and `ArchitectureInterfaceImplementationContract` to `ArchitectureContractModels.cs` with strict/audit group properties (`strict_inheritance`, `audit_inheritance`, `strict_interface_implementation`, `audit_interface_implementation`) wired into `AllStrict`/`AllAudit`
- [x] 1.2 Add fail-closed load-time validation in `ArchitecturePolicyDocumentLoader` (source-surface + base-type selector required for inheritance; interface selector + location expectation required for interface implementation), mirroring attribute-usage validation

## 2. Execution

- [x] 2.1 Add matcher/scanner logic for base-type chain walking and implemented-interface enumeration with generic-type-definition matching and defensive reflection (reuse/extend `ArchitectureTypeRoleMatcher` posture)
- [x] 2.2 Add `ArchitectureAnalysisSession.Inheritance.cs` implementing `CheckInheritanceContract` (source surface resolution, transitive base chain matching, ignores, deterministic ordering)
- [x] 2.3 Add `ArchitectureAnalysisSession.InterfaceImplementation.cs` implementing `CheckInterfaceImplementationContract` (allowed-only/forbidden location evaluation via existing `IsAllowedLocation`/`ResolveProjectAssemblyNames`, ignores, deterministic ordering)
- [x] 2.4 Register both families in `ArchitectureContractCatalog.Build` (after `attribute_usage`, before `coverage`) and add handlers in `ArchitectureContractHandlers.cs` + registration in `ServiceCollectionExtensions`

## 3. Diagnostics and reporting

- [x] 3.1 Extend `ArchitectureViolation` with inheritance/implementation fields; add `InheritanceDiagnostic` and `InterfaceImplementationDiagnostic` records and `ArchitectureDiagnosticKind` entries
- [x] 3.2 Update `ArchitectureDiagnosticMapper` and `ArchitectureDiagnosticFormatter` (and policy-consistency handling if family lists are enumerated there) for both new families

## 4. Schema, capabilities, tooling

- [x] 4.1 Add the four new contract group keys to `schema/dependencies.arch.schema.json`
- [x] 4.2 Add capability entries to `archlinternet.capabilities.json`
- [x] 4.3 Update `tools/scripts/architecture_coverage_report.py` (+ its tests) to recognize the new families — verified no-op: the script derives families from policy coverage contracts, not a hardcoded family list

## 5. Tests

- [x] 5.1 Add fixtures: framework base type leakage (MonoBehaviour-like), transitive inheritance, generic base type, nested type, application port + adapter implementations, inherited interface implementation, generic interface, interface-extends-interface
- [x] 5.2 Add `InheritanceContractTests`: direct/transitive/generic/nested/prefix violations, source-surface filtering, interface-not-matched, strict failure, audit-only, ignored + unmatched ignores, loader validation errors, deterministic ordering
- [x] 5.3 Add `InterfaceImplementationContractTests`: misplaced/forbidden/allowed adapter cases, inherited + generic interface matching, interface-extends-interface exclusion, single-violation-for-both-lists, strict failure, audit-only, ignored + unmatched ignores, loader validation errors, deterministic ordering

## 6. Docs and AI guidance

- [x] 6.1 Add `docs/contracts/inheritance.md` and `docs/contracts/interface-implementation.md`; link from `docs/contracts/index.md` and `docs/policy-format/index.md`
- [x] 6.2 Update `docs/policy-format/supported-capabilities.md`, `docs/ai/capabilities.md`, and `docs/ai/policy-authoring-guide.md` with the new families (including a Unity MonoBehaviour boundary example and a server application-port example)

## 7. Validation

- [x] 7.1 Run `make fmt`
- [x] 7.2 Run `make acceptance` and fix all failures
