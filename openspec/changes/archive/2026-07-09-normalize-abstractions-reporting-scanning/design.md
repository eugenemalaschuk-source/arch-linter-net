## Context

`docs/internal/core-architecture-blueprint.md`'s "Core interface namespace convention" section (added by the archived `2026-07-03-normalize-core-abstractions` change) already defines the classification rule this change must apply: an interface earns a move into `<Module>.Abstractions` when it is consumed from a Core module *other than* the one that defines it (plus `Composition`, which wires everything). That change's own inventory table classified all 6 Reporting/Scanning interfaces as "internal feature seam" — consumed only from their own folder plus `Composition` — and left them in place. Issue #201 asks to re-inspect Reporting and Scanning for the same missing-`Abstractions` pattern; the right way to honor both the issue and the existing convention is to re-run the same classification check against the current codebase rather than invent a new rule or move interfaces wholesale.

Re-running the check (grepping every candidate interface for which folders under `src/ArchLinterNet.Core` and which test/CLI/Testing/Unity projects reference it) found one interface whose consumption has changed since 2026-07-03: `IArchitectureAsmdefScanner` (defined in `Scanning`) is now a constructor dependency of `ArchLinterNet.Core.Asmdef.AsmdefValidationService` — the `Asmdef` module, which did not exist as a consumer at the time of the prior audit. The other 5 interviewed interfaces are unchanged: still consumed only from their own folder plus `Composition`.

## Goals / Non-Goals

**Goals:**
- Apply the blueprint's existing classification rule to Reporting and Scanning, moving only the interface(s) that now cross an internal module boundary.
- Keep the move behavior-preserving: no signature, DI lifetime, or registration-order changes.
- Update the blueprint's inventory table so it reflects the current, re-audited state, and fix the inconsistency where the doc's infrastructure-seams narrative (line ~158) already described `IArchitectureAsmdefScanner` as consumed by an "Asmdef" seam while its own inventory table (line ~194) still called it an internal feature seam.
- Record why the other 5 interfaces stay put, so a future reader doesn't mistake the narrow scope for an incomplete pass.

**Non-Goals:**
- Moving all Reporting/Scanning interfaces regardless of consumption (explicitly ruled out by #201's own non-goal: "no new abstraction model beyond the existing project convention" — the existing convention is boundary-crossing-based, not folder-based).
- Changing `IArchitectureAsmdefScanner`'s visibility, signature, or DI lifetime.
- Adding new `architecture/dependencies.arch.yml` contracts (out of scope per #201; #215 tracks self-policy guardrail additions for the whole #183 story).
- Touching `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, or `ArchLinterNet.Unity` (audited — none define interfaces).

## Decisions

### Classification rule: reuse the existing boundary-crossing test, don't invent a folder-based one

Applying the blueprint's documented rule verbatim (consumed from a Core module other than its own, plus `Composition`) to each of the 6 candidates:

| Interface | File (current) | Consumed from (beyond own folder + Composition) | Verdict |
|---|---|---|---|
| `IArchitectureAsmdefScanner` | `Scanning/ArchitectureAsmdefScanner.cs` | `Asmdef` (`AsmdefValidationService`) | **Move** → `Scanning.Abstractions` |
| `IArchitectureDiagnosticFormatter` | `Reporting/ArchitectureDiagnosticFormatter.cs` | — | Stays (internal feature seam) |
| `IArchitectureSarifFormatter` | `Reporting/ArchitectureSarifFormatter.cs` | — | Stays (internal feature seam) |
| `IArchitectureSourceScanner` (internal) | `Scanning/ArchitectureSourceScanner.cs` | — | Stays (internal feature seam) |
| `IArchitectureIlMethodBodyScanner` (internal) | `Scanning/ArchitectureIlMethodBodyScanner.cs` | — | Stays (internal feature seam) |
| `IArchitectureExternalDependencyIlScanner` (internal) | `Scanning/ArchitectureExternalDependencyIlScanner.cs` | — | Stays (internal feature seam) |

Moving the other 5 anyway (a "normalize everything in these two folders" reading of #201) was considered and rejected: it would contradict the blueprint's own documented rule for zero discoverability gain, and #201's acceptance criteria says "interfaces **that belong to** abstractions" and its own task list says "move **appropriate** interfaces" — both read as selective, criteria-based moves, not a blanket folder sweep.

### `IArchitectureAsmdefScanner` moves as a replaceable infrastructure seam, following the `IO` split-file pattern

`Asmdef.AsmdefValidationService` already consumes other Core infrastructure seams the same way (`IArchitecturePolicyDocumentLoader`, `IArchitectureRepositoryRootResolver`), both already living in their module's `Abstractions` namespace. Splitting `Scanning/ArchitectureAsmdefScanner.cs` into `Scanning/Abstractions/IArchitectureAsmdefScanner.cs` (interface only, namespace `ArchLinterNet.Core.Scanning.Abstractions`) and leaving `ArchitectureAsmdefScanner` (concrete class) in `Scanning/ArchitectureAsmdefScanner.cs` matches every other moved interface's file layout exactly — no new pattern introduced.

### No `architecture/dependencies.arch.yml` change needed

`core_scanning`'s layer definition uses namespace `ArchLinterNet.Core.Scanning`, and `NamespaceGlobPattern` (used throughout the policy engine) matches by prefix, so `ArchLinterNet.Core.Scanning.Abstractions` is already covered by the same layer and subject to the same dependency-direction rules as the rest of `Scanning` — confirmed by reading the prior change's design.md, which established this same fact for the original 5-module move.

## Risks / Trade-offs

- [Missed `using` directive after the split causes a compile error] → Only two call sites reference `IArchitectureAsmdefScanner` by name (`Asmdef/AsmdefValidationService.cs`, `Composition/ServiceCollectionExtensions.cs`), both identified up front; `make acceptance` (build + full test suite) would catch any missed reference as a compile error regardless.
- [Under-scoping: someone expects all 6 Reporting/Scanning interfaces to move] → Documented explicitly above and in the blueprint's inventory table update: the other 5 do not cross a module boundary today, so moving them would be speculative relocation ahead of actual cross-module need, which both the blueprint's own convention and #201's non-goals rule out.

## Migration Plan

1. Create `src/ArchLinterNet.Core/Scanning/Abstractions/IArchitectureAsmdefScanner.cs` with the interface, namespace `ArchLinterNet.Core.Scanning.Abstractions`.
2. Remove the interface declaration from `src/ArchLinterNet.Core/Scanning/ArchitectureAsmdefScanner.cs`, leaving only the concrete class (namespace stays `ArchLinterNet.Core.Scanning`), and add `using ArchLinterNet.Core.Scanning.Abstractions;`.
3. Update `using` directives in `Asmdef/AsmdefValidationService.cs` and `Composition/ServiceCollectionExtensions.cs`.
4. Update `docs/internal/core-architecture-blueprint.md`'s inventory table (move `IArchitectureAsmdefScanner` to the replaceable-infrastructure-seam row with the other Reporting/Scanning interfaces noted as re-audited internal feature seams) and reconcile the infrastructure-seams narrative sentence that already mentioned this consumer.
5. Build and run `make acceptance` (or the repo's documented acceptance gate) to confirm no missed reference and no behavior change.

No rollback beyond `git revert` is needed — this is a compile-time-verified, behavior-preserving rename.

## Open Questions

None — scope is fully determined by re-applying the existing, documented classification rule.
