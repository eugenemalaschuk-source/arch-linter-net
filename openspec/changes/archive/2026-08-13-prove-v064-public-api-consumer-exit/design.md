## Context

The 0.6.1 packed-artifact gate proved that a real consumer could delete the F1-F11 workarounds. It
did so with one large synthetic modular consumer (23 projects) whose composed policy carries the
release's own policy-shape evidence. `surface_selector` needs the same kind of proof, but the claim
it makes is narrower and orthogonal to that fixture's directional-dependency narrative: "a governed
assembly with a large incidental exported surface can shrink its reviewed API snapshot to an
intentional subset, selected by existing bounded evidence, without touching CLR visibility or
semantic role." Reusing the 23-project `modular-consumer` fixture for this would entangle two
unrelated release narratives in one fixture and risk destabilizing the already-passing 0.6.1
scenarios that fixture proves. A dedicated, smaller fixture is lower risk and easier to reason about.

## Goals / Non-Goals

- Goal: prove, from the installed v0.6.4 candidate, every consumer-exit item #526 enumerates:
  snapshot shrinkage, dual selector sources, role continuity, exact delta lifecycle, review-visible
  membership changes, fail-closed first-party escapes, a green full-policy strict run, and CLI/Testing
  parity.
- Goal: keep the new fixture and scenarios additive — no change to the existing consumer-cleanup
  matrix's fixtures, scenario IDs, or passing behavior.
- Non-goal: changing `surface_selector` runtime behavior (already shipped and unit-tested by #525).
- Non-goal: re-running or duplicating the 23-project `modular-consumer` policy-shape narrative.
- Non-goal: publishing v0.6.4 or advancing the packaged schema/README release-identity line — that
  remains a separate, deliberate release action gated on this PASS, not a side effect of it.

## Decisions

### A dedicated `api-surface-selector` fixture, not an extension of `modular-consumer`

`modular-consumer` is the release's policy-shape evidence for the *0.6.1* directional-dependency
finding set. Adding selector-specific types and contracts to it would make an already-large fixture
carry two unrelated release narratives, and any authoring mistake in the new contracts would risk
regressing the 17 already-passing 0.6.1 scenarios. A new, smaller fixture keeps the blast radius of
this change local to the scenarios it proves.

### One assembly, three sibling `strict_public_api_surface` contracts

The same assembly is governed by three contracts over the same source: `assembly-wide-api` (no
selector — the #94 baseline and the "no selector = unchanged behavior" proof), `marker-selected-api`
(`has_attribute` selector — the primary orthogonal-marker adoption path), and
`namespace-selected-api` (`namespace` selector — the second bounded selector source). Running all
three against one build makes the snapshot-reduction comparison a direct diff of three files
produced from the same exported-type inventory, rather than requiring separate builds per selector.

### The first-party-escape scenario uses a temporary, reverted contract

The escaping type (a selected type whose member returns an unselected first-party exported type) is
governed by a fourth contract, `escaping-selected-api`, appended to the policy only for that one
scenario and reverted immediately after (the same append/revert pattern
`AssertLayerOverlapAllowance` already uses). It is never part of the fixture's permanent contract set
so the "full-policy strict run is green" scenario is never in tension with the "fails closed" proof.

### Role continuity is proven with `strict_context_allow_only`, not `type_placement`

`type_placement.types_matching` does not support `role` — only `surface_selector` adds `role` on top
of the shared structural matcher. To prove a selected `ValueObject`-role type still participates in
an ordinary role-based governance rule unchanged, the fixture declares a `strict_context_allow_only`
contract with `source: { role: ValueObject }`, using the existing contextual-dependency contract
family rather than inventing a new mechanism.

### CLI/Testing parity reuses the packaged-Testing-consumer harness shape

`AssertRepeatedTestingEnsureBuilt` already builds an external `ArchLinterNet.Testing` consumer from
the isolated feed. The new parity scenario follows the same shape: an isolated external consumer
project resolves the candidate `ArchLinterNet.Testing` package, runs the same selected-surface
contract, and the scenario asserts its normalized violation output matches the CLI's `--format json`
output for the same contract byte-for-byte after canonicalization.
