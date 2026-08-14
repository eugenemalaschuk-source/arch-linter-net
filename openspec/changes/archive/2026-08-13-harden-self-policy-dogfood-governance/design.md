# Design — hardening the self-policy into executable governance

## Decision 1: solution discovery, with tests and benchmarks excluded

`analysis.solution: ArchLinterNet.slnx` makes the solution the project inventory instead of a
hand-maintained list. `project_exclude` removes `tests/**` and `benchmarks/**`: those roots are
neither shipped nor consumer-visible, and their build outputs are not part of the analysed assembly
set, so pulling them into the governed project universe would only produce `unknown`-classified
coverage noise. Exclusion is by root rather than by include-narrowing so that a new top-level
production folder is discovered by default rather than silently skipped.

`analysis.target_assemblies` stays explicit, so discovery seeds source roots but does not change
which assemblies are loaded (per `project-discovery`: explicit `target_assemblies` takes precedence).

**Consequence, accepted deliberately:** declaring project discovery brings the run under build-state
preflight, so an ordinary run now needs a verified build receipt. The canonical gate therefore passes
`--ensure-built`, which replaces the two explicit `dotnet build` invocations the old target made. It
prepares and verifies the project graph and writes nothing under `architecture/`.

## Decision 2: whole-assembly public API membership, not `surface_selector`

#525/#527 delivered intentional bounded membership. It was evaluated per assembly and deliberately
not adopted:

- a `surface_selector` governs only matching types and leaves every other exported type ungoverned;
- for `Core`, `Testing`, and `CEL` the entire exported surface *is* what the NuGet packages publish,
  so whole-assembly membership is strictly stronger and states the truth;
- selected membership earns its place when a package exports types that are technically public but
  explicitly unsupported. That is not this repository's shape today.

`ArchLinterNet.Cli` is excluded entirely: it is `PackAsTool` with `ToolCommandName:
arch-linter-net`. Its compatibility boundary is the command line, already governed by the CLI
command-dispatch specs and tests; its assembly surface is an implementation detail.

`api_comparison: exact` is used everywhere it is adopted, so additions, removals, and signature
changes are all reviewed deltas. The Core snapshot is large (~3000 entries); that size is the honest
measure of the package's exported surface, and the churn it produces is the intended signal.

## Decision 3: source-set expansion over copy-paste, with explicit subtraction

The DI-container and MSBuild-evaluation invariants were 26 near-identical rules. They become 2
authored rules over one reviewed `all_declared_layers` set, each subtracting the layers where the
dependency *is* the architecture (`exclude_sources: [core, core_composition]` and
`[core, core_discovery]`). The umbrella `core` layer is subtracted alongside its child because its
namespace prefix contains the allowed child.

This also widened enforcement: `core_schema`, `core_profiling`, `cel`, and the three `cli_*`
sub-layers were previously ungoverned by these invariants and now are.

**Limitation found and recorded:** layer-kind globs use the dot-segment grammar, which rejects both
a bare `*` and a partial-segment `core*`. Because every layer key here is one underscore-joined
segment, no glob can express "every declared layer", so the set lists members explicitly. A new layer
must be added to that list — a one-line review signal next to the layer declaration, not an automatic
escape hatch.

## Decision 4: name-suffix matching for checkers, interface evidence everywhere else

The issue's rule is to prefer interface/base/attribute evidence over name guessing *where such
evidence exists*. It exists for diagnostics (`ArchitectureDiagnostic` base type), diagnostic payloads
(`IArchitectureDiagnosticPayload`), and both policy-validator seams (real interfaces) — all four use
it.

It does not exist for family checkers: they are static classes bound through the
`ArchitectureContractChecker` **delegate**, so nothing structural selects them. `name_suffix: Checker`
is the only available evidence and currently matches exactly the 27 checker types plus the delegate
that defines the seam, which is subtracted via `exclude_types_matching: [{ base_type:
System.MulticastDelegate }]`. Zero false positives today, and the negative regression fails loudly if
that stops being true.

## Decision 5: one gate, thin wrappers

`make lint-architecture` runs the CLI's strict path and is the authoritative answer. Everything else
is a thin wrapper over an already-shipped CLI capability (`policy check`, `public-api
diff|update`, `explain`) — no second orchestration framework, no ad-hoc reimplementation of product
validation. `SelfArchitecturePolicyTests` stays as parity evidence that the Testing adapter reaches
the same verdict.

Write separation is structural: `public-api-check` and `public-api-update-preview` never write;
`public-api-update` is the only writing command and is never invoked by lint, acceptance, or CI.

Race safety is unchanged: `_lint-dotnet` still serializes the build-output-mutating chain, and
`_acceptance-test` still takes it as an order-only prerequisite.

## Decision 6: negative regressions mutate the real policy in place

Each regression copies the real policy, applies one exact anchored mutation, and writes the copy as a
sibling of the original so every repository-relative input (`ArchLinterNet.slnx`,
`architecture/api/*.txt`) resolves against the same policy boundary. A temp directory would break
those paths.

Anchors are asserted to occur exactly once, so removing or rewording a guard breaks its regression
instead of silently turning the mutation into a no-op. Copies are deleted in setup and teardown and
are gitignored as a crash guard. Both self-policy fixtures move to the E2E bucket, matching the
documented placement rule for build/filesystem-driven integration work.
