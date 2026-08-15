## Context

`decompose-god-classes` removes existing handwritten partial aggregates, but its strict declaration-count rule cannot prevent a new god node from being created in a shared command root or helper namespace. The current CLI command boundary is already strict and valuable: eight hand-authored command layers are independent and reflection composes their modules. Its peer inventory, however, must be changed whenever a command is added, and `Cli.Commands` still contains two shared output writers at its root.

The current recursive conventions are audit-only. Interfaces must be located in `Abstractions`, but that folder can hold concrete types; the `Exceptions` inverse rule forbids only ordinary classes; nested command abstractions have no local dependency-direction rule. This is enough to measure debt, not enough to make the next independent module safe by default.

The target is a modular monolith, not premature microservices. Microsoft guidance describes a bounded context as a cohesive model with explicit integration points, while modular-architecture practice connects that boundary to team ownership and lower delivery coupling. Within this repository, CLI commands are the first concrete modules; Core's technical subsystems and CEL's intentional evaluation graph are not mechanically recast as domains by this change.

## Goals / Non-Goals

**Goals:**

- Make every new direct CLI command module visible to architecture policy without modifying a central peer inventory.
- Make module ownership and internal dependency direction explicit, feature-first, and suitable for parallel changes.
- Keep reuse intentional: no direct sibling dependency and no generic shared-code bucket.
- Strengthen `Abstractions`, `Models`, and `Exceptions` conventions recursively, preserving the current audit findings until the corresponding production code has been migrated.
- Provide a scaffold that creates a compiling, governed command in its own paths and test fixture.
- Retain the existing command-independence rule as migration evidence, then replace only its hand-maintained inventory once dynamic policy proves parity.

**Non-Goals:**

- Do not split the repository into separate NuGet packages, processes, or microservices.
- Do not force Core's technical layers or CEL's parser/binder/evaluator graph into the CLI command template; their boundaries need separate domain analysis and decomposition work.
- Do not permit a catch-all `Shared`, `Common`, or `Utils` module as an escape hatch.
- Do not infer business cohesion, ownership, runtime DI resolution, or data-flow semantics from static code facts.
- Do not weaken the strict no-handwritten-production-partials destination of `decompose-god-classes`.

## Decisions

### A direct child namespace is the module unit

For `ArchLinterNet.Cli.Commands`, a module is a namespace whose first segment after that container is its name. Every deeper namespace belongs to that module. Namespace ownership, rather than a folder-name convention alone, is authoritative because the linter analyses compiled type references; source paths remain evidence for the scaffold and layout rules.

This lets the module contract discover `Baseline` through `Validate` and future `Inspect` without editing an eight-item layer list. A type at the container root belongs to no module and is therefore a deliberate migration diagnostic, not silent shared infrastructure.

### A module profile has a minimal feature-first shape

The CLI-command profile is:

```text
Commands/<Command>/
  EntryPoint/       command-module and System.CommandLine wiring
  Application/      one use-case handler and collaborators
  Abstractions/     interfaces or abstract classes only, when needed
  Models/           dependency-free data types, when needed
  Exceptions/       dependency-free exception types only, when needed
```

`EntryPoint` and `Application` separate host wiring from behaviour. The three convention folders are optional, avoiding empty ceremony. The first-party direction is `EntryPoint → Application → Abstractions → Models`; `Application` and `EntryPoint` may also use `Models` and `Exceptions`; `Models` and `Exceptions` are leaves. A module root has no production behaviour after migration.

Existing commands are migrated in audit mode before this becomes strict. This preserves working CLI behavior and avoids a broad move-only refactor merely to satisfy a new shape.

### The linter gets a dynamic module-container contract, not another YAML inventory

The contract discovers immediate child namespaces under a configured container, forbids edges between distinct children, and applies an exhaustive allowed-segment/profile check within every child. It reports discovered modules and concrete source/target evidence deterministically in human, JSON, and SARIF output.

This is preferred over repeating `strict_independence.layers` and `strict_layer_templates.containers` because those lists make every new module touch the same YAML lines and therefore cause the merge conflicts this change is intended to remove. The existing strict command-independence contract stays in place until an audit contract and a parity test prove the dynamic discovery covers the same eight modules; only then is the manual peer list retired.

### Cross-module reuse is a published boundary, never a sibling shortcut

Default module-to-module references remain forbidden. A genuinely shared capability first receives a narrowly named namespace outside `Commands` (for example, `ArchLinterNet.Cli.Integration.OutputFormatting`), an owner, a policy reason, and a dependency contract. Both modules may depend on that published boundary, but neither can depend on the other.

The repository duplicates small unstable code instead of extracting it prematurely. A shared kernel is considered only when a stable concept has one owner and more than one verified consumer. This keeps conflict hot-spots and shared change cadence out of the command container.

### Folder purity needs semantic role and abstractness facts

`Exceptions` must accept only types classified by inheritance as `Exception`, independent of C# kind. `Abstractions` must accept interfaces and abstract classes, not concrete implementation, record, enum, struct, or delegate types. Existing source parsing already identifies type kinds and partial declarations; it is extended to carry abstractness into layout evaluation, while policy adds an all-declarations role/modifier expectation. Existing layout contracts are unchanged unless they opt in.

This resolves the current asymmetric rules without treating a file name as semantic evidence. The eight existing non-interface declarations in `Abstractions` are audited first and either move to a more precise contract/data location or receive a consciously revised convention; no automatic relocation is allowed.

### Reflection composition validates module membership before instantiation

`CliCommandModuleCatalog` retains reflection discovery, so scaffolding need not edit central registration. It first maps each candidate to a governed direct module and rejects candidates in the container root, a forbidden generic bucket, or an undeclared segment. It reports all root module candidates deterministically before requiring exactly one.

This prevents a helper class from changing the CLI surface just because it implements a marker interface. The architecture contract remains static evidence; the catalog is the runtime defence-in-depth check.

### Scaffold is a thin, non-overwriting CLI profile

The first scaffold profile is `cli-command`. It validates a PascalCase module name and command token, supports dry-run, creates only the module's own entry-point/application/test files, and creates convention folders only when requested. It never alters `Program.cs`, a command list, or a peer-layer inventory. A collision fails before writing; force is explicit.

The scaffold is intentionally not a universal clean-architecture generator. Once a future bounded context has a real repeated shape and integration model, it can receive a second profile rather than forcing its needs into the command template.

## Risks / Trade-offs

- [A strict profile creates ceremony for tiny commands] → only EntryPoint/Application are generated; convention folders are optional and small code may remain local until a stable boundary is needed.
- [Dynamic discovery mistakes a technical namespace for a module] → it uses only direct child namespaces with production types, reports discovery evidence, and has exact exclusions only for reviewed migration seams.
- [A named integration boundary becomes a new god node] → it is outside the container, owner- and reason-required, separately governed, and never a generic `Shared` namespace.
- [Reflection changes command behavior] → retain existing process-level CLI tests and add malformed candidate tests before enabling strict catalog membership checks.
- [Folder purity causes large churn] → start audit-only, decide each existing exception explicitly, and promote a rule to strict only after its audit report is empty.
- [A source-only rule misses runtime composition] → enforce module membership both statically and in the reflection catalog; retain human PR review for runtime behavior.

## Migration Plan

1. Add dynamic module discovery, profile validation, diagnostics, and fixtures in audit mode; retain the current strict CLI command-independence rule and compare inventories.
2. Add abstractness/role-purity layout evidence and audit existing `Abstractions` and `Exceptions`; move only types whose responsibility is understood.
3. Move root command output writers to separately named owned capabilities, and migrate one representative command to `EntryPoint`/`Application` to prove the profile and CLI behavior.
4. Constrain reflection candidates, add the command scaffold, and prove two independently scaffolded commands require no central registration or policy edit.
5. Promote passed rules to strict, remove the redundant manual peer inventory, update capability documentation, and run public API, architecture, lint, and cross-platform test gates.

Rollback keeps the existing strict command-independence policy and disables the new contract or scaffold profile independently; no public API snapshot is rewritten by this change.

## Open Questions

- Which existing non-interface declarations under `Abstractions` are true published contracts that deserve a new `Contracts` location, versus data that belongs under `Models`?
- Should a future domain profile use the same in-process integration namespace convention or a separate assembly/project when team ownership needs stronger compilation boundaries?
- Which command, after `Validate`, is the safest representative migration for proving the profile without conflating it with unrelated behavior work?
