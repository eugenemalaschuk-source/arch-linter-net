# Self-policy capability matrix

Reviewed classification of every contract family the ArchLinterNet engine currently supports
against ArchLinterNet's **own** repository. It answers one question per family: does this family
express a real invariant of this repository, and is that invariant already executable?

Scope of the review: the contract groups declared by `schema/dependencies.arch.schema.json`
(`$defs.contracts`) on the current `main`, checked against the real `src/` layout, the shipped
package graph, and the seams produced by the #451/#452/#453 refactors.

Decisions use four values:

| Decision | Meaning |
| --- | --- |
| **adopt** | The family expresses a real invariant with acceptable precision, and a strict rule now enforces it. |
| **already covered** | An equivalent or stronger executable guard already exists; a second rule would add noise, not governance. |
| **N/A** | The family does not model a meaningful invariant of this repository. |
| **defer** | A concrete prerequisite, unsupported semantic, or unacceptable false-positive risk blocks adoption today. |

Every adopted guard has a focused negative regression in
`tests/ArchLinterNet.Core.Tests/SelfPolicyNegativeRegressionTests.cs`, or is covered by the strict
gate's own coverage summary. The regressions mutate the real policy and assert the mutation is
caught, so a guard cannot silently degrade into a no-op.

## The governed shape

```text
ArchLinterNet.Cli ──┐
                    ├─► ArchLinterNet.Core ──► ArchLinterNet.CEL
ArchLinterNet.Testing ─┘
```

- `ArchLinterNet.Cli` — packed .NET tool (`PackAsTool`), executable, not a library boundary.
- `ArchLinterNet.Core` — engine package consumers reference directly.
- `ArchLinterNet.Testing` — NUnit adapter package.
- `ArchLinterNet.CEL` — standalone, dependency-light, host-agnostic expression engine.

Project discovery is authoritative: `analysis.solution: ArchLinterNet.slnx`, with
`project_exclude` removing `tests/**` and `benchmarks/**`. Those two roots are deliberately outside
the governed project universe — they are neither shipped nor consumer-visible, and their build
outputs are not part of the analysed assembly set. `samples/` contains no project files.

## Matrix

### Dependency and layering families

| Family | Repository invariant | Evidence source | Decision | Notes |
| --- | --- | --- | --- | --- |
| `dependency` | Namespace-level direction between Core's internal layers and the hosts | Declared `layers` | **already covered** | 21 pre-existing strict rules; unchanged by this work. |
| `allow-only` (namespace) | — | — | **N/A** | The internal layer graph is expressed as targeted forbidden edges plus ordered layers; an allow-only restatement would duplicate them with weaker locality. |
| `layer-order` | `cli`/`testing` → `core` → `cel`, and `cli` → `core_validation` → `core_execution` → `core_model` | Declared `layers` | **already covered** | `core-layering`, `core-application-seam-layering`. |
| `cycle` | Packages must not form dependency cycles | Declared `layers` | **already covered** | `package-cycles`. |
| `independence` (layer) | — | — | **N/A** | Mutual independence between layers is already implied by the directional rules above; the meaningful independence statement here is at assembly level (below). |
| `protected-surface` | `Core.Scanning` / `Core.Discovery` / `Core.Resolution` internals are Core-only | Declared `layers` | **already covered** | Three pre-existing rules. |
| `layer-template` | — | — | **N/A** | Layer templates repeat one ordered-layer shape across sibling containers. Core's internal layers are not sibling instances of one shape; they are a single ordered stack. |
| `acyclic-sibling` | — | — | **N/A** | Same reason: there is no set of sibling module namespaces to keep mutually acyclic. |

### External, package, and framework families

| Family | Repository invariant | Evidence source | Decision | Notes |
| --- | --- | --- | --- | --- |
| `external-dependency` | DI-container APIs confined to `Core.Composition`; MSBuild/Buildalyzer APIs confined to `Core.Discovery` | `external_dependencies` groups + `all_declared_layers` source set | **adopt** | Replaces 26 copy-pasted rules with 2 authored rules expanded over a reviewed layer inventory and subtracted with `exclude_sources`. Coverage widened: `core_schema`, `core_profiling`, `cel`, and the three `cli_*` sub-layers are now governed too. |
| `external-allow-only` | — | — | **N/A** | The repository has exactly two governed external groups; an allow-only restatement would have to enumerate every permitted BCL namespace to say the same thing. |
| `package-dependency` | No shipped project declares a test/benchmark framework; Buildalyzer/MSBuild and the DI container are declared only by Core | `packages` groups + `shipped_assemblies` source set | **adopt** | Three authored rules; two use `exclude_sources: [ArchLinterNet.Core]`. |
| `package-allow-only` | CEL declares **no** `PackageReference` at all | `shipped_assemblies` | **adopt** | `allowed: []`. This is CEL's product promise, not a style preference. Deliberately *not* applied to Core/Cli/Testing: freezing their full package graph as an exhaustive allow-list has no compatibility rationale (non-goal in #464). |
| `framework-dependency` | — | — | **already covered** | The allow-only rule below is strictly stronger. |
| `framework-allow-only` | Every shipped project carries only the implicit `Microsoft.NETCore.App` | `framework_references` group + `shipped_assemblies` | **adopt** | Catches accidental `Microsoft.AspNetCore.App` / `Microsoft.WindowsDesktop.App` adoption, which would silently narrow which hosts can consume the packages. |

### Assembly families

| Family | Repository invariant | Evidence source | Decision | Notes |
| --- | --- | --- | --- | --- |
| `assembly-dependency` | CEL references no other shipped assembly | `analysis.target_assemblies` | **adopt** | Complements `cel-must-not-depend-on-*`: a namespace rule only fires once code *uses* a type, this fires on the declared reference. |
| `assembly-allow-only` | Core references only CEL; Cli/Testing reference only Core | `adapter_assemblies` source set | **adopt** | `direct` depth only — the engine does not resolve transitive assembly-reference paths, and the rules do not claim to. |
| `assembly-independence` | Cli and Testing never reference each other | `analysis.target_assemblies` | **adopt** | Two separately consumable packages. |

### Project families

| Family | Repository invariant | Evidence source | Decision | Notes |
| --- | --- | --- | --- | --- |
| `project-metadata` (friend assemblies) | Each shipped project's `InternalsVisibleTo` set is exactly the reviewed one | Solution discovery | **adopt** | Four rules, one per shipped project, including `allowed_friend_assemblies: []` for Testing. |
| `project-metadata` (forbidden project references) | No shipped project references a `tests/**` or `benchmarks/**` project | `production_projects` project set | **adopt** | Reuses solution-discovered project sets instead of a duplicated path inventory. |
| `project-metadata` (properties) | A shipped project is never a test project | Solution discovery | **adopt** | Only `IsTestProject` is frozen. Incidental SDK/build-style properties are deliberately **not** frozen. |
| `coverage` `scope: project` | Every discovered production project maps to a declared layer | Solution discovery | **adopt** | Complements assembly and namespace coverage; a new `src/` project cannot silently escape governance. |
| `coverage` `scope: assembly` / `namespace` | — | — | **already covered** | Pre-existing. |
| `coverage` `scope: dependency_edge` | — | — | **defer** | Would require enumerating the intended edge pairs of a 21-layer graph. The directional rules already state the forbidden edges; an edge-coverage inventory would restate them in a second, hand-maintained form with no new failure it catches. |
| `coverage` `scope: semantic_role` | — | — | **N/A** | The repository declares no `classification` block — see semantic families below. |
| `coverage` `scope: rule_input` | Adopted strict rules keep matching real code | Contract IDs | **adopt** (extended) | Now also covers the two source-set-expanded external rules through their authored IDs (94 → 132 covered inputs). See the limitation note below for the families it cannot accept. |

### Public API families

| Family | Repository invariant | Evidence source | Decision | Notes |
| --- | --- | --- | --- | --- |
| `public-api-surface` — Core, Testing, CEL | The exported surface of each shipped library is a reviewed compatibility contract | Reviewed snapshots under `architecture/api/` | **adopt** | `api_comparison: exact`, so additions, removals, and signature changes are all reviewed deltas. |
| `public-api-surface` — Cli | — | — | **N/A** | `ArchLinterNet.Cli` is a packed executable tool (`PackAsTool`, `ToolCommandName: arch-linter-net`). Its compatibility boundary is the **command line**, already governed by the CLI command/dispatch specs and tests; its assembly surface is an implementation detail of the executable package. |
| `public-api-surface` — `surface_selector` (#525) | — | — | **N/A** *(evaluated, not adopted)* | A bounded selected surface governs only matching types and leaves every other exported type ungoverned. For these three assemblies the whole exported surface *is* the shipped compatibility surface, so whole-assembly membership is strictly stronger and states the truth about what the packages export. Selected membership is the right tool when a package exports types that are technically public but not a supported contract; that is not this repository's shape. Revisit if a shipped assembly ever gains a deliberately-unsupported exported region. |

### Structural and semantic families

| Family | Repository invariant | Evidence source | Decision | Notes |
| --- | --- | --- | --- | --- |
| `type-placement` | Family checkers live in the extracted checker seam (#452); every diagnostic type lives in `Core.Model` | `name_suffix` / `base_type` matchers | **adopt** | The checker rule uses name-suffix matching deliberately: checkers are static classes bound through the `ArchitectureContractChecker` **delegate**, so no interface or base type selects them. `exclude_types_matching: [{ base_type: System.MulticastDelegate }]` subtracts the delegate that defines the seam. |
| `interface-implementation` | `IArchitectureDiagnosticPayload` implementations stay in `Core.Model` (#453); raw/typed policy-document validators stay in their loading seams (#451) | Real interfaces | **adopt** | Three rules. Interface evidence is used in preference to naming where it exists. |
| `inheritance` | — | **already covered** | — | The one real base-type invariant (`ArchitectureDiagnostic` residency) is expressed as `type-placement`, which states *where the type must live*; an inheritance contract states *what a type may not inherit from*, and this repository has no forbidden base type. |
| `composition` | — | — | **already covered** | `di-container-stays-in-the-composition-boundary` already confines container APIs to `Core.Composition` at namespace level, and the repository has no service-locator API to forbid separately. Adding a composition contract over the same boundary would produce a second diagnostic for the same edge. |
| `attribute-usage` | — | — | **N/A** | The repository declares no first-party attribute type whose placement is architecturally meaningful. Adding one purely to make a selector possible is an explicit non-goal of #464. |
| `layout-conventions` | — | — | **defer** | File/folder conventions here are real but weakly specified (partial classes deliberately split one type across many files, e.g. `ArchitectureAnalysisSession.*.cs`), so `require_type_name_matches_file_name` and matching-interface expectations would produce large false-positive sets. Revisit only with a specified convention to enforce. |
| `method-body` | — | — | **defer** | No forbidden call pattern is currently agreed for this codebase. The nearest candidate (direct `File`/`Directory` use outside `Core.IO`) is not yet true — infrastructure seams exist but adoption is partial — so a strict rule would need a broad ignore list, which is an explicit non-goal. |
| `context-dependency` / `context-allow-only` / `port-boundary` | — | — | **N/A** | All three consume the semantic role/metadata index, which requires a `classification` block. See below. |
| Semantic classification / `semantic_role` coverage | — | — | **N/A** | The repository has no trustworthy role evidence: no first-party role attributes, and its namespaces are *layer* names (`Core.Execution`, `Core.Reporting`) rather than role names (`Domain`, `Adapter`, `Port`). Deriving roles from these namespaces would restate the layer graph under a second vocabulary, and one-primary-role semantics would then force an arbitrary choice for types that are simultaneously e.g. "reporting" and "model-facing". Introducing annotations to make this possible is explicitly out of scope (#565–#574 own that work). |
| `asmdef` | — | — | **N/A** | Unity `.asmdef` validation is a Core *capability*; this repository ships no Unity assets to validate. |

## Recorded engine limitations found during this review

These are behaviours confirmed against the current engine, recorded so future self-policy work does
not re-derive them or author policy that looks plausible but is not executable.

1. **Layer-kind source-set globs cannot express every declared namespace layer.** Layer globs use
   the dot-segment grammar (`Bare wildcard '*' is not allowed`, `Partial segment wildcard 'core*' is not allowed`). Because every namespace-layer key here is a single underscore-joined segment,
   `all_declared_layers` lists its members explicitly. Semantic role selectors do not provide a
   namespace coverage input, so they are governed through their enclosing namespace layers. A newly
   declared namespace layer must be added to that list; the omission is a one-line review signal next
   to the layer declaration, not an automatic escape.
1. **`scope: rule_input` coverage accepts only dependency, layer, allow-only, cycle, method-body,
   independence, protected-surface, and external contract IDs.** Assembly, package, framework,
   project-metadata, type-placement, interface-implementation, public-api, and coverage contracts are
   rejected at load time. Those families are therefore guarded by their own negative regressions
   instead of by rule-input coverage.
1. **`framework_allow_only` sees the SDK's implicit `Microsoft.NETCore.App`.** An empty `allowed`
   list fails on every project. The reviewed baseline group `base_runtime_framework` exists for this
   reason.
1. **Assembly dependency/allow-only families are `direct`-depth only.** `dependency_depth: transitive` is rejected; the adopted rules do not claim to prove transitive paths.
1. **Declaring `analysis.solution` brings the run under build-state preflight.** Ordinary validation
   never builds, and a build receipt does not survive the process that created it, so *every*
   self-policy run — the canonical gate and each negative regression — passes `--ensure-built`
   (which prepares and verifies the project graph and writes nothing under `architecture/`). This
   is why both self-policy fixtures live in the E2E bucket and carry an explicit `[CancelAfter]`
   duration exemption.
1. **In-process validation from `ArchLinterNet.Core.Tests` resolves `Core`/`CEL`/`Testing` to the
   test host's own output directory**, not to `src/*/bin`, because those assemblies are already
   loaded in the test process. `assembly_search_paths` does not override that. `--ensure-built`'s
   post-build probing paths are what make the Testing-adapter parity run resolve the real shipped
   outputs.

### One defect found and fixed

Authoring the assembly-graph rules surfaced a real engine defect:
`ArchitectureAnalysisSession.CheckAssemblyDependencyContract`/`CheckAssemblyAllowOnlyContract` used
the id-only `IsContractSelected` overload, so selecting a source-set-expanded assembly contract by
its authored id ran **nothing** — silently passing. The package, framework-reference, and external
families already used the expansion-aware overload, and `source-set-expansion` requires "the
authored id selects every instance", so this was a defect against an agreed spec rather than a
missing capability. Fixed in place, with a pipeline regression in
`SourceExpansionPipelineIdentityTests`.

## Developer workflow

| Command | Writes? | Purpose |
| --- | --- | --- |
| `make lint-architecture` | no | **Canonical** read-only strict self-policy gate. Part of `make lint` / `make acceptance`. |
| `make audit-architecture` | no | Diagnostic audit-mode run. |
| `make policy-check` | no | Fast policy-only validation — schema, imports, composition, contract references. No project evaluation or assembly loading. |
| `make public-api-check` | no | Reviewed-snapshot drift for every governed assembly. |
| `make public-api-update-preview` | no | Dry run of the snapshot rewrite. |
| `make public-api-update` | **yes** | The explicit, human-initiated snapshot rewrite. Never invoked by lint, acceptance, or CI. |
| `make explain-architecture SOURCE=… TARGET=…` | no | Provenance/explain debugging for one edge. |

`SelfArchitecturePolicyTests` runs the same policy through the `ArchLinterNet.Testing` adapter as
parity evidence inside `make test`. It is evidence that both hosts agree, not a competing definition
of success.
