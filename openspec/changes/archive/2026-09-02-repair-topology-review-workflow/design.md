## Context

The initial topology-review implementation introduced a public `Topology` application seam, but its capture service consumes an Execution-owned validation observation directly. It also performs output collision checks by path text alone and publishes directly to the requested file. Consequently, protected-contract tests fail, trusted policy inputs can be reached through aliases, and the documented fixtures do not constitute executable end-to-end evidence.

The existing ordinary validation evaluator remains the source of topology semantics. The repair must preserve that single-evaluator design, respect Core layering, and update both reviewed public-API snapshots.

## Goals / Non-Goals

**Goals:**

- Keep Execution-owned observation types inside the permitted Validation-to-Execution boundary.
- Use a neutral, internal validation projection for topology capture.
- Prove CLI capture, diff, and verify against real .NET and Unity-style assembly inputs.
- Make output protection cover physical aliases and publication failures.
- Make nested command diagnostics identify the complete topology command path.

**Non-Goals:**

- Introducing a second topology evaluator or changing ordinary validation semantics.
- Broadening Core's protected Execution importer policy.
- Automatically accepting captured topology into a policy.
- Adding network access or a Unity editor dependency to test execution.

## Decisions

### Project observations before the Topology boundary

`ArchitectureAnalysisSnapshot` will translate evaluator observations into Validation-owned internal records containing only topology-owned strings and DTOs. `Topology` will consume only this projection; it will neither import `Core.Execution` nor expose Execution generic arguments through compiler-generated closures.

This retains the existing evaluator and its permitted Validation-to-Execution dependency. Allow-listing `Topology` as an Execution importer was rejected because it would convert an implementation convenience into a permanent architectural exception.

### Minimize the public capture service surface

The engine request/outcome records and `ArchitectureEngine.CaptureTopology` remain the supported consumer API. The service interface and concrete implementation become internal composition details. Both Core API approval baselines will be regenerated from the resulting intentional public surface.

Keeping every composition service public was rejected because callers need the engine operation, not a second service contract.

### Treat trusted inputs as physical files and publish atomically

The command guard will build one manifest of policy, imported policy, resolved assembly, receipt, project, baseline, and other analysis input paths. It will reject an output whose existing file identity is the same as an existing manifest entry, including symlink and hard-link aliases. Documents will be written to a sibling temporary file and atomically replaced only after successful generation; failed writes retain the original target and clean up their temporary artifact.

Comparing `Path.GetFullPath` values was rejected because it does not recognize filesystem aliases. Special-casing only `.asmdef` files was rejected because every trusted read input needs the same guarantee.

### Execute fixture lifecycle through the real CLI runtime

NUnit acceptance tests will build the .NET fixture and materialize Unity-style fixture assemblies in its `Library/ScriptAssemblies` layout. They will invoke the real CLI runtime for capture, diff, and verify, assert deterministic documents, native topology categories, strict/audit exits, and hashes of all trusted inputs before and after. Test-only build/materialization replaces the prior manual README procedure; it does not require Unity.

Renderer-only or fake-runtime tests were rejected because they cannot prove command wiring, filesystem guards, or evaluator behavior.

### Resolve usage hints from command ancestry

The CLI host will inspect the parsed command ancestry and select `topology capture`, `topology diff`, or `topology verify` before considering similarly named top-level commands. This prevents a leaf name such as `diff` from selecting the baseline-diff help text.

### Reuse the normal validation execution seam

Topology diff and verify will construct their `ValidationRequest` through the same CLI execution
mapper as ordinary validation and will attach external evidence through that same post-native
binding step. This carries waiver evaluation date, external-evidence bindings, and assessment
context through unchanged, rather than maintaining a second partial orchestration path.

## Risks / Trade-offs

- [Filesystem identity differs by platform] → Encapsulate identity in the existing filesystem abstraction and exercise physical aliases where the host supports them.
- [Fixture assembly materialization adds test time] → Build once per fixture test setup and keep assemblies minimal and deterministic.
- [Internalizing services alters public API] → Regenerate and review both approval snapshots and retain the engine-level public operation.
- [Atomic replacement behavior varies by filesystem] → Use the repository's file-system temporary and replace primitives and add a failed-publication regression test.

## Migration Plan

1. Add the neutral observation projection and remove the Execution import from `Topology`.
2. Introduce alias-aware input-manifest checks and atomic publication with tests.
3. Add real fixture lifecycle and command-ancestry integration tests.
4. Regenerate the intentional Core public-API approvals, run architecture/API/test gates, and push the repair commit to PR #758.

Rollback is a normal commit revert; the change adds no persisted schema migration and does not write policy inputs.

## Open Questions

None.
