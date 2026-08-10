## Context

The v0.6.0 packed-artifact gate answers "does one immutable candidate install and behave
identically everywhere?". The v0.6.1 release has to answer a different question: "can a real
external consumer delete the workarounds 0.6.0 forced on it?". Those are not the same evidence,
and the existing gate cannot fail when a workaround is still required.

## Goals / Non-Goals

- Goal: prove F1–F11 and #465 from the installed candidate, not from source-tree tests.
- Goal: make the *shape* of the canonical consumer policy release-blocking evidence.
- Goal: emit an explicit PASS/FAIL publication statement for the candidate version.
- Non-goal: changing any product runtime behavior, public API, or schema format.
- Non-goal: making refactoring story #450 or self-policy hardening #464 a release prerequisite.

## Decisions

### One synthetic modular consumer, not one fixture per finding

The consumer-cleanup matrix needs a solution large enough that per-module copy-paste would be the
obvious 0.6.0 authoring, and composed enough that the policy cannot be a monolith. One 23-project
fixture (20 modules, shared abstractions, a composition host, an excluded test project) satisfies
every finding's scenario and is built once per gate run, which keeps the added platform cost near
one extra build rather than one per scenario.

Two scenarios need their own copy because they mutate the consumer (adding a module to prove
enrolment; adding an out-of-boundary call to prove the namespace allowance still enforces).
Everything else shares the built fixture.

The alternative — a fixture per finding — was rejected: it multiplies build cost across four
platform jobs and produces no policy-shape evidence, because no single policy would then be the
canonical consumer path.

### `dependencies.cycles.arch.yml` resolves assemblies directly

The baseline subcommands have no `--ensure-built` option, and the fixture's main policy relies on
solution discovery with build-state preflight. The strict-cycles probe therefore declares its two
target assemblies with `assembly_search_paths` instead. It also declares *two* cycle contracts —
one genuinely cyclic, one ordinary acyclic inter-layer edge — so the regression proves the scope
restriction rather than merely that a cycle can be baselined.

### Policy shape is typed counters, not prose

"No avoidable copy-paste remains" is only checkable if it is measured. Each platform record
carries counters (authored vs expanded directional contracts, governed modules and projects,
copied project inventories, inline public-API signatures) and the aggregator applies the
workaround-shape rules to them. This is what lets the gate fail a candidate whose scenarios all
pass but whose canonical policy is still workaround-shaped.

### Tracked defects block the release without breaking the build

Executing the matrix found a real defect: on a composed policy the effective-schema failure
reports an unrelated imported-fragment location as its primary provenance, and inapplicable
constant-discriminator branches resurface when an independent defect exists (#471).

Three options were considered:

1. Fix #471 inside this change. Rejected: the issue explicitly requires defects found by the gate
   to be tracked under #434 rather than folded into the gate, and the fix is in the Core schema
   diagnostic projection, not in the gate.
2. Let the scenario throw. Rejected: a permanently red `make acceptance` cannot distinguish "known
   release blocker" from "someone broke the build today", and it blocks unrelated work.
3. Record the scenario as `failed` with its tracking issue and let the aggregator refuse
   authorization. Chosen.

The registry is deliberately two-sided: an unregistered failure fails the gate, and a registered
scenario that starts passing *also* fails the gate, so an entry cannot outlive its defect.

## Risks / Trade-offs

- Platform job duration grows. Mitigated by sharing one built fixture and raising the release job
  budget from 30 to 45 minutes; the matrix adds roughly one minute locally.
- The tracked-defect registry could be abused as a suppression list. Mitigated by requiring a
  tracking issue per entry, keeping the failure visible in the evidence artifact and its Markdown
  summary, and failing the gate when an entry becomes stale.

## Migration Plan

None. No product runtime, public API, schema, or policy semantics change. The release workflow
gains a longer timeout and a stricter aggregation step; the packaged README and release docs
advance from the 0.6.0 to the 0.6.1 public adoption package line.

## Open Questions

None.
