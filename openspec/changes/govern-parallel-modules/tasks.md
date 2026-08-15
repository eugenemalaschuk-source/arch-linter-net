## 1. Module-container policy capability

- [x] 1.1 Add strict/audit module-container contract models, schema, raw validation, family registration, policy consistency, baseline identity, and provenance support.
- [x] 1.2 Build a deterministic first-party namespace inventory that maps every declared type to its immediate child module under a configured container and distinguishes container-root types from module descendants.
- [x] 1.3 Implement sibling-dependency checking for discovered modules, including deterministic human, JSON, and SARIF evidence for the container, source module, and target module.
- [x] 1.4 Implement the CLI-command profile: exhaustive allowed segments, reviewed root exceptions, generic-bucket rejection, and the EntryPoint/Application/Abstractions/Models/Exceptions dependency directions.
- [x] 1.5 Add unit, policy-validation, CLI integration, coverage, baseline, and output-format regression tests for discovery, no inventory bypass, sibling violations, profile violations, audit behavior, and stable diagnostics.

## 2. Recursive convention purity

- [x] 2.1 Preserve C# abstract-modifier facts through source parsing and declared-type/source-fact materialization without changing existing type-kind or partial-declaration semantics.
- [x] 2.2 Extend layout-convention schema, model, validation, and checking with all-declarations role/modifier purity expectations and diagnostics that name actual kind, role, and abstractness.
- [x] 2.3 Add focused tests proving recursive Abstractions folders accept interfaces/abstract classes and reject concrete or value types, and Exceptions folders reject every non-exception type kind.
- [x] 2.4 Update policy-format, contract, capability-matrix, and self-policy documentation with the exact audit-to-strict adoption rules and limitations of static evidence.

## 3. Migrate the repository without weakening existing boundaries

- [x] 3.1 Add the CLI command module-container contract in audit mode alongside the existing strict hand-authored command-independence rule; add a parity regression for the eight current direct modules.
- [x] 3.2 Audit and classify the eight current non-interface declarations in Abstractions; move each to a precise contract/model/implementation location or document a reviewed alternative before enabling strict folder purity.
- [x] 3.3 Move `Cli.Commands` root output writers to separately named, owner-governed CLI capabilities and add policy proving the root is no longer a shared behavior bucket.
- [x] 3.4 Migrate `Validate` into the EntryPoint/Application profile with process-level behavior parity tests, then use that migration to validate the profile's intended dependency direction.
- [x] 3.5 Migrate `Baseline`, `Cache`, `Explain`, and `Graph` into the profile in independently reviewable slices, preserving command behavior and focused command tests.
- [x] 3.6 Migrate `Policy`, `PublicApi`, and `Schema` into the profile in independently reviewable slices, preserving command behavior and focused command tests.
- [x] 3.7 Add strict recursive Models/Exceptions leaf rules and nested Abstractions direction rules only after their audit reports are empty; include negative self-policy regressions for every resolved bypass.

## 4. Safe reflection composition and scaffolding

- [x] 4.1 Extract a shared module-membership resolver usable by the policy checker and `CliCommandModuleCatalog`, with tests for root, undeclared, generic-bucket, and nested EntryPoint candidates.
- [x] 4.2 Update reflection command discovery to instantiate only governed module candidates and to report all root-module candidates deterministically before enforcing exactly one root module.
- [x] 4.3 Add the `cli-command` scaffold command/profile with PascalCase/token validation, deterministic dry-run output, collision protection, explicit force behavior, and no central registration or peer-inventory edits.
- [x] 4.4 Add scaffold templates for EntryPoint, Application, focused tests, and requested Models/Abstractions/Exceptions without generating empty convention folders.
- [x] 4.5 Add end-to-end scaffold tests proving two independently generated commands compile, pass module policy, and can be composed without changes to Program or a command registry.

## 5. Make the new default strict and reviewable

- [x] 5.1 Promote the discovered CLI module-container contract and recursive folder-purity conventions from audit to strict after all migration diagnostics are resolved.
- [x] 5.2 Retire the redundant hand-maintained command layer inventory and sibling-independence contract only after parity tests prove the discovered contract is authoritative; retain source-set and rule-input coverage for the new contract.
- [x] 5.3 Document the contributor workflow: local module ownership, published-contract/shared-kernel admission criteria, prohibited generic buckets, scaffold usage, and the required policy checks.
- [x] 5.4 Verify reviewed public API is unchanged, run formatter, policy check, architecture lint, full lint, focused and full tests, and `openspec validate govern-parallel-modules --strict`.

## 6. Self-review hardening

- [x] 6.1 Make ordinary scaffold creation atomic at each target so a race cannot overwrite a newly created source file without `--force`.
- [x] 6.2 Reject generic module buckets case-insensitively in policy checking and reflection composition.
- [x] 6.3 Reject malformed module-container namespace values during policy loading.
- [x] 6.4 Generate a focused scaffold fixture that validates the actual entry-point command module.

## 7. Review follow-up hardening

- [x] 7.1 Make the atomic no-clobber move an explicit requirement of every `IFileSystem` implementation, including test doubles.
- [x] 7.2 Roll back unchanged files created by an ordinary scaffold invocation when a later target collides, and add regressions for rollback and externally modified-file preservation.

## 8. Second review follow-up hardening

- [x] 8.1 Preserve canonical `/`-separated repository paths in scaffold plans and output on every supported operating system.
- [x] 8.2 Roll back empty directories created by an aborted ordinary scaffold plan, with a regression using the production filesystem implementation.

## 9. Third review follow-up hardening

- [x] 9.1 Serialize ordinary scaffold invocations with an atomically acquired repository-scoped lock held from preflight through rollback, preventing rollback ownership races and concurrent directory cleanup.
