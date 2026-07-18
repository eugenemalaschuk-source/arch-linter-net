## Why

`ArchLinterNet.CEL` (#322/#324 line) is a complete, standalone CEL expression engine
already merged to `main`, and `openspec/specs/cel-policy-model/spec.md` (#162) already
defines the exact schema-backed `when` fields ArchLinterNet policies are allowed to
declare. Neither is usable yet: `ArchLinterNet.Core` has a `ProjectReference` to
`ArchLinterNet.CEL` but zero lines of code that call it, and no policy YAML field
accepts `when` today. Until Core compiles and caches `when` expressions at policy-load
time, the documented model stays fail-closed and unusable by design
(`docs/internal/cel-policy-model.md`: "No live ArchLinterNet runtime may accept `when`
fields until #163 implements compilation and evaluation").

## What Changes

- Add the optional `when` YAML field to the two selector shapes the cel-policy-model
  spec allows it on: `layers.<name>.selector` and the contextual
  dependency/allow-only selectors (`source`, `forbidden`/`allowed`, `exclude`) —
  and only those; `ArchitectureContextSelector` is also reused, unmodified in
  accepted keys, by port-boundary/adapter-binding contracts, which stay
  fail-closed for `when`.
- Add Core-owned CEL context/object schemas (`subject`; `source`; `source` +
  `target` + `dependency`) built once from the public `ArchLinterNet.CEL` API,
  matching the closed member lists in `cel-policy-model`.
- Add a new policy-document validator that compiles every `when` field through
  `ArchLinterNet.CEL` at load time (forcing a boolean result), fails policy
  loading with an actionable message on any compile diagnostic, and caches the
  compiled predicate on the owning selector so later evaluation never
  re-parses.
- Add typed context factories and an evaluation-result wrapper in
  `ArchLinterNet.Core.Execution` that turn Core architecture facts into
  immutable CEL values and compiled-predicate evaluation results, ready for
  #164 to wire into selector/contract matching — this change does not perform
  that wiring itself.
- Update `schema/dependencies.arch.schema.json` so the JSON Schema used for
  composed (multi-file) policies accepts `when` in the same closed set of
  locations, without opening it up for port-boundary/adapter-binding
  selectors.
- Add focused Core.Tests coverage: successful compile/load, compile-failure
  translation, context-schema selection, boolean-result enforcement, cache
  reuse, cache isolation between policy sessions, and the literal-only fast
  path.

Non-goals: implementing CEL language/evaluator behavior (owned entirely by
`ArchLinterNet.CEL`); changing the CEL Profile v1 function catalog; wiring
expression evaluation into actual selector/contract matching or contract
execution (#164); public user-facing docs/examples (#161); regex, plugins, or
reflection-based context values.

## Capabilities

### New Capabilities

- `core-cel-integration`: Core's compilation-at-load, caching, diagnostic
  translation, and typed-context-factory integration with the public
  `ArchLinterNet.CEL` API for the `when` fields defined by `cel-policy-model`.

### Modified Capabilities

(none — `cel-policy-model` already normatively describes the `when` field
locations, schemas, and fail-closed semantics this change implements against;
no requirement in that spec changes. `policy-document-validation-pipeline`
gains a new validator entry through its documented extension mechanism, which
is implementation detail, not a change to that capability's own requirements.)

## Impact

- `src/ArchLinterNet.Core/Contracts/ArchitectureContractModels.cs` (`ArchitectureLayerSelector`)
- `src/ArchLinterNet.Core/Contracts/ArchitectureContextSelector.cs`
- `src/ArchLinterNet.Core/Contracts/ArchitecturePolicyDocumentLoader.cs` (raw-YAML key allow-lists)
- `src/ArchLinterNet.Core/Contracts/Validators/ArchitecturePolicyDocumentValidatorPipeline.cs` (new entry)
- New: `src/ArchLinterNet.Core/Contracts/Expressions/` (schemas)
- New: `src/ArchLinterNet.Core/Contracts/Validators/ExpressionCompilationValidator.cs`
- New: `src/ArchLinterNet.Core/Execution/Expressions/` (typed context factories, evaluation wrapper)
- `schema/dependencies.arch.schema.json`
- `tests/ArchLinterNet.Core.Tests/ArchLinterNet.Core.Tests.csproj` (new `ArchLinterNet.CEL` `ProjectReference`)
- `tests/ArchLinterNet.Core.Tests/` (new test files)
- No changes to `ArchLinterNet.CEL`, `ArchLinterNet.Cli`, or `ArchLinterNet.Testing`.
