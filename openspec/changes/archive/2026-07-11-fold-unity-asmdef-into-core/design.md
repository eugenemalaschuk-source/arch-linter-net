## Context

The asmdef-only validation pipeline already lives in Core:

`AsmdefValidator` → `ArchitectureEngine.ValidateAsmdef` → `IAsmdefValidationService` → policy loader, repository-root resolver, and asmdef scanner.

The former Unity project contributed only the first facade in that chain and referenced Core alone. It did not isolate a framework dependency or implementation that Core could not own.

## Goals / Non-Goals

**Goals:**

- expose the existing convenience API from `ArchLinterNet.Core.Asmdef`;
- preserve strict-asmdef-only behavior, returned `ArchitectureViolation` values, signatures, and public parameter names;
- remove the redundant assembly/package/test/release surfaces;
- keep one authoritative asmdef execution path;
- make repository policy and documentation describe the real package graph;
- verify the actual NuGet artifacts produced by pull-request validation.

**Non-Goals:**

- changing asmdef contract semantics;
- adding Unity runtime/editor integration or a Unity Package Manager package;
- moving asmdef validation into the full validation-mode/baseline pipeline;
- redesigning the Core composition root.

## Decisions

1. **Core owns the convenience facade.** `AsmdefValidator` lives beside `AsmdefValidationRequest`, `AsmdefValidationOutcome`, and `AsmdefValidationService` in `ArchLinterNet.Core.Asmdef`.
2. **Public signatures remain stable apart from namespace/package.** Both `Validate(string contractPath)` and `Validate(string contractPath, out IReadOnlyCollection<ArchitectureViolation>)` are retained, including the `contractPath` parameter name used by named-argument callers.
3. **The facade remains thin.** It owns only a lazy default `ArchitectureEngine` and delegates all behavior to `ValidateAsmdef`.
4. **No compatibility package is retained.** The product is still in preview and no public package listing was found during implementation verification. Keeping an empty forwarding package would preserve the complexity this change removes.
5. **Tests move rather than duplicate.** The former Unity facade tests become Core tests; existing engine/service tests remain the lower-level seam coverage. A named-argument test protects source compatibility for the retained parameter name.
6. **Historical archive references remain historical.** Older archived OpenSpec changes may describe the former adapter boundary. Active specs, solution/build/release configuration, the active architecture blueprint, and current documentation use the new Core-owned model.
7. **Package output is validated, not inferred.** Pull-request validation runs `make pack` and requires exactly one package each for `ArchLinterNet.Core`, `ArchLinterNet.Cli`, and `ArchLinterNet.Testing`, with no Unity artifact.

## Validation Strategy

- compile the three remaining production projects through the solution;
- run the migrated facade tests and existing asmdef engine/service tests;
- compile a named-argument call using `contractPath` through the Core test suite;
- run the repository self-policy against Core, CLI, and Testing;
- run OpenSpec validation and the full repository CI gates;
- run `make pack` in pull-request CI and verify exactly Core, CLI, and Testing NuGet artifacts are produced.
