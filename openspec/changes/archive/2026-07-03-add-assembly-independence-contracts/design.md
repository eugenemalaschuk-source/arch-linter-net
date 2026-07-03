## Context

`strict_independence`/`audit_independence` (`independence-contracts` capability) evaluate mutual independence between namespace/layer glob patterns via `ArchitectureAnalysisSession.CheckIndependenceContract` — for every pair of layers, it scans types by namespace and reports cross-references. This is purely namespace-based and has no concept of compiled-assembly references.

Issue #51 asks for a parallel, assembly/project-level contract: given a set of named .NET assemblies, none may directly reference another. This must be added through the contract-catalog + handler-registry pipeline (`contract-handler-execution` capability, built by the `validation-handlers` change): a family becomes executable by (1) adding a model + YAML group, (2) registering it in `ArchitectureContractCatalog.Build`, (3) implementing an `IArchitectureContractHandler`, and (4) one DI registration — `ArchitectureContractExecutor` dispatches every family generically over `FamiliesInOrder` with no changes required.

No assembly-reference-graph resolution exists anywhere in the codebase today (confirmed: no `GetReferencedAssemblies()` calls). `Context.TargetAssemblies` (`IReadOnlyCollection<Assembly>`, on `ArchitectureAnalysisSession.Context`) already holds every assembly resolved from `analysis.target_assemblies` via `ArchitectureAssemblyResolutionService`, matched by `assembly.GetName().Name`. `Assembly.GetReferencedAssemblies()` gives direct assembly-level references for free from these already-loaded assemblies — no new `.csproj`/`.deps.json` parsing is needed.

Unity's `.asmdef` checks (`asmdef-contracts`/`asmdef-validation-service`) are a structurally unrelated, Unity-only JSON manifest scanner (`ArchitectureAsmdefScanner`) and must not be touched or reused beyond the shared contract/handler pattern.

## Goals / Non-Goals

**Goals:**
- Let a policy declare `strict_assembly_independence`/`audit_assembly_independence` contracts naming a set of assemblies that must not directly reference one another.
- Produce deterministic `ArchitectureViolation`s identifying the source assembly, the forbidden target assembly, and the contract id/name.
- Implement entirely through the existing contract catalog/handler seam, with zero changes to `ArchitectureContractExecutor`, the CLI, or the public validator beyond wiring supplied by that seam.
- Fail loudly at policy-load time if a listed assembly name isn't resolvable via `analysis.target_assemblies`, rather than silently skipping it at check time.
- Leave `strict_independence`/`audit_independence` and Unity `.asmdef` checks byte-for-byte behaviorally unchanged.

**Non-Goals:**
- Transitive assembly reference path resolution — this change only detects direct references (`Assembly.GetReferencedAssemblies()` is a direct-reference API; deeper graph walking is deferred).
- Parsing `.csproj`/`ProjectReference` elements or `.deps.json` — resolution is via already-loaded reflection `Assembly` objects, same as every other contract family.
- Any interaction with the namespace-independence-driven dependency-edge coverage mechanism (`ArchitectureAnalysisSession.Coverage.cs`) or the `FindIndependenceConflicts` policy-consistency cross-check — both are namespace-axis concerns and out of scope for an assembly-axis contract.
- Baseline-specific wiring — baseline suppression operates generically over `ArchitectureViolation` and needs no new code; this change relies on that existing genericity rather than adding baseline logic itself.
- Any changes to Unity `.asmdef` scanning or its Core application service.

## Decisions

**1. Reuse `ArchitectureViolation` and `ArchitectureIgnoredViolation` rather than introducing new types.**
The existing violation record already carries contract name/id, a source identifier, a forbidden-target identifier, and evidence — sufficient to report "source assembly X directly references forbidden assembly Y under contract Z". Introducing a parallel `AssemblyViolation`/`ArchitectureIgnoredAssemblyViolation` pair would duplicate the ignore-matching (`ArchitectureIgnoreMatcher.IsIgnored`) and reporting/JSON-serialization paths for no behavioral gain, contradicting the project's bias against speculative abstractions. Docs will call out explicitly that for this family, `source_type`/`forbidden_reference` in `ignored_violations` hold assembly simple names, not C# type names.
*Alternative considered*: a dedicated violation subtype with `SourceAssembly`/`ForbiddenAssembly` named properties. Rejected for this change — no consumer currently needs to distinguish violation shape by family at the type level (existing families already vary widely in what their generic fields mean), and it would require touching every violation-consuming code path (JSON output, baseline matching, CLI formatting) for a naming-clarity benefit only.

**2. Direct assembly-reference detection via `Assembly.GetReferencedAssemblies()`, matched by simple name, no transitive walk.**
Every assembly in the contract is expected to already be present in `Context.TargetAssemblies` (resolved from `analysis.target_assemblies`). For each ordered pair `(source, forbidden)` in the contract's declared `assemblies` list, the check resolves both to `Assembly` instances by simple name and tests whether `forbidden`'s simple name appears in `source.GetReferencedAssemblies()`. This requires no new resolution infrastructure.
*Alternative considered*: building a full transitive reference graph across all resolved assemblies up front. Rejected for this change per the issue's explicit "decide direct vs. transitive" ask — direct-only matches the MVP scope, avoids the complexity/cost of graph traversal and cycle handling, and can be added later as a `dependency_depth`-style option (mirroring `ArchitectureDependencyContract`) without a breaking change to the direct-only shape.

**3. New loader validation: every contract-listed assembly name must exist in `analysis.target_assemblies`.**
Unlike namespace layers (which the layer resolver can fail on lazily per-check), an unresolvable assembly name here is unambiguously a policy authoring mistake — the assembly can never be loaded or referenced-graph-checked. Validating at load time (mirroring `ValidateLayerNamespaces`) turns this into one deterministic, actionable error message instead of a silently-empty (and therefore falsely "passing") check.
*Alternative considered*: skip unresolvable assemblies silently at check time, consistent with how independence contracts currently behave for dangling layers under `IsDanglingButCoveredByRuleInputCoverage`. Rejected — that escape hatch exists specifically for the `rule_input` coverage-contract mechanism, which does not apply to assembly names since they're deliberately excluded from `CollectLayerBearingContractIds`. Without that mechanism, a silent skip would let a checked-in typo quietly disable strict validation.

**4. `assembly_independence` contracts are NOT added to `CollectLayerBearingContractIds` (the `rule_input` coverage whitelist).**
That whitelist exists so `rule_input`-scoped coverage contracts can resolve a referenced contract's `layers` against `document.Layers` keys. Assembly names are never `document.Layers` keys, so including this family would either silently produce nothing or require special-casing coverage code — the same reasoning that already excludes `asmdef` and `layer_template`.

**5. Family remains resolvable in `ArchitectureContractCatalog.IsGroupResolvable`.**
Unlike `asmdef`/`layer_template`, this family's contracts have a normal `group`/`mode`/`id` shape suitable for CLI `--contract-id` selection and JSON `group` reporting, matching how `independence` itself is resolvable. No change to `IsGroupResolvable` is needed — it already treats every family as resolvable except the two explicitly excluded.

## Risks / Trade-offs

- [Risk] Confusing field reuse: `source_type`/`forbidden_reference` on `ArchitectureIgnoredViolation` will hold assembly names for this family but type names for others. → Mitigation: explicit callout in `docs/contracts/assembly-independence.md` and the YAML reference doc; no code-level ambiguity since each contract family only ever constructs its own violations/ignores.
- [Risk] Assembly simple-name collisions (two different assemblies sharing a simple name from different probing paths) could cause a false match or miss. → Mitigation: matches the exact identity semantics `ArchitectureAssemblyResolutionService` already uses for `analysis.target_assemblies` resolution (first-match-by-simple-name); no new ambiguity is introduced beyond what already exists for target assembly resolution.
- [Risk] Direct-only detection could give false confidence that two assemblies are fully decoupled when an indirect (transitive) path exists. → Mitigation: documented explicitly as a current limitation in the contract doc and YAML reference, consistent with the issue's instruction to decide and document this scope choice rather than silently imply full-graph coverage.

## Open Questions

None — all decisions above were confirmed during exploration before writing this design.
