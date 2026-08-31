## Context

See [proposal.md](proposal.md). `ArchitectureReferenceGraph` currently retains only the successful
subset from the scanner's fail-closed API. The topology and external-dependency metric projections
therefore cannot distinguish an empty complete scan from an incomplete one. The external IL
projection has the same information-loss boundary. Validation continues to consume its established
silent best-effort projections and must retain its diagnostic behavior.

## Goals / Non-Goals

**Goals:**

- Carry direct-reference completeness through the session-owned metric fact projections.
- Fail a metric closed only when incomplete or non-exact evidence could contribute to its selected
  node.
- Preserve stable values, contributor ordering, and existing validation behavior for complete
  evidence.

**Non-Goals:**

- Change the validation scanner contract or turn scanner degradation into validation violations.
- Retry, repair, or load missing dependency assemblies during measurement.
- Change metric policy syntax or report schema.

## Decisions

### Cache scanner results with completeness

`ArchitectureReferenceGraph` will cache both referenced types and an `IsComplete` flag from
`ArchitectureReferenceScanner.TryGetReferencedTypes`. Its existing `GetReferencedTypes` method
will retain best-effort behavior for validation callers, while an internal completeness-aware method
will serve metric projections. This avoids a second reflection scan and avoids an externally
observable validation change.

### Add metric-only incomplete-source sets

The topology projection will retain identities of source subjects whose direct reference scan is
incomplete. The external-dependency fact index will expose the source `Type` set with incomplete
reflection or IL evidence. These are projections alongside facts, not applicability records, so
only `ArchitectureMetricEvaluator` interprets them.

For outgoing relations, an incomplete selected source invalidates the metric. For incoming
relations, an incomplete mapped source invalidates the metric because its omitted endpoint might
be the selected node. For external groups, an incomplete source invalidates a selected source node.
This conservative rule follows the no-partial-subset contract.

### Preserve IL scan completeness without changing validation output

The IL scanner will provide an internal facts-plus-completeness result for metrics and retain the
existing facts-only method for validation-compatible callers. The metric scanner may classify any
unavailable method body, generic context, malformed IL, or member-resolution failure as
incomplete even if other facts were recovered. Validation keeps its pre-existing best-effort and
exception behavior rather than sharing the failure handling.

### Short-circuit explicit unresolved root assemblies

Measurement will treat `ArchitectureAnalysisContext.MissingAssemblyNames` as required evidence
missing, even when build-state preflight has no project discovery and an `allow_empty` topology
would otherwise evaluate to zero. This is applied at the snapshot's measure seam, after runner
setup resolves target assemblies and before metric evaluation. Validation's existing configuration
diagnostics remain unchanged.

The declared target-assembly set is one report input, so this block applies to every selected
metric ID in that report, including a public-surface metric that would not otherwise enumerate the
missing root. A caller needing an independently scoped report must use a policy with an independent
target-assembly declaration.

### Preserve type-universe completeness

`ArchitectureTypeIndex` will retain whether any target assembly raised
`ReflectionTypeLoadException` while supplying its ordinary best-effort `AllTypes` list. Metric
evaluation will turn a partial type universe into `missing_required_input` before any metric can
count it. Validation consumers retain their current type-loading behavior. This protects type,
namespace, project, footprint, external-dependency, and public-surface calculations from a
partially loadable target assembly.

## Risks / Trade-offs

- [A transient metadata-resolution limitation suppresses a numeric metric] → report a typed
  `missing_required_input` state and retain no partial value, so consumers can distinguish it from
  zero.
- [Incoming metrics become conservative when one mapped source is incomplete] → this is necessary
  because the unknown missing edge can target any selected node.
- [A type-load failure makes even an otherwise independent selected metric unassessable] → the
  policy-declared target assembly set is the report's required input; create a separately scoped
  policy for an independent report.
- [Completeness metadata expands cached session state] → it is one Boolean per scanned type and a
  small source set, while avoiding rescan work.

## Migration Plan

1. Add completeness-aware scanner projections and evaluator guards with regressions.
2. Run focused Core and CLI tests plus repository gates.
3. Archive the change after the implementation is accepted; rollback is a normal code revert with
   no persisted data or policy migration.
