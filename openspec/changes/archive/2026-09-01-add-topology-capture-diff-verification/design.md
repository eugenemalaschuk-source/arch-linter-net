## Context

The declared-topology model and evaluator already own canonical subject classification,
relationship witnesses, completeness evidence, applicability projection, and strict/audit
integration. They intentionally do not own CLI orchestration or policy mutation. Issue #511
must expose a review workflow without reconstructing those semantics from YAML, diagnostics, or
an independently scanned graph.

The workflow needs to be useful before a topology is declared as well as after one is reviewed.
Capture therefore observes a requested supported subject kind from the analysis session, whereas
diff and verify consume the topology evidence produced by ordinary validation for a declared
topology.

## Goals / Non-Goals

**Goals:**

- Provide `topology capture`, `topology diff`, and `topology verify` CLI operations with stable
  JSON suitable for CI and AI review.
- Expose a Core capture API so hosts can obtain the same deterministic first-party observations
  without duplicating session traversal.
- Keep structural mapping, forbidden relationships, unmapped subjects, and stale declarations
  as separate, reviewable categories.
- Make verification delegate to the existing normal validation path and its strict/audit result
  semantics.
- Prove the lifecycle with realistic .NET server/library and Unity-style fixtures.

**Non-Goals:**

- Writing or merging generated candidates into a reviewed topology policy.
- Diagram parsing, a graph UI, a score/coverage percentage, a topology-specific baseline, or a
  second applicability/result envelope.
- Inventing type selectors or converting raw candidates into automatically approved mappings.

## Decisions

### Capture records observations, not approved policy

`topology capture` accepts a policy and an explicit subject kind (`type`, `namespace`, `project`,
or `assembly`) and writes or prints a versioned JSON capture document. The document contains
deterministically ordered first-party subject and relationship candidates together with the
requested kind and source identity. It is a review input, not a policy fragment that the command
installs or edits. This preserves useful support for all existing subject kinds, including type
topology whose schema intentionally has no exact-type selector.

An alternative considered was emitting ready-to-merge YAML. That would imply selectors and
component groupings the evaluator cannot safely infer, especially for type and same-named
namespace subjects. A raw, typed capture makes every approval step explicit.

### Core owns canonical capture facts; CLI owns command and rendering

Add a public Core topology-capture request/outcome service. It creates one normal analysis
session and shares the validation evaluator's first-party observation projection, then converts
only the necessary stable facts into public capture records. The CLI uses this service through
`ICliRuntime`; JSON/human file and console I/O remain CLI concerns.

This avoids using the graph-export model as a surrogate: graph levels do not express every
declared-topology subject kind or its canonical identity. It also avoids leaking scanner internals
or asking consumers to reparse topology YAML.

### Diff projects the ordinary validation result

`topology diff` runs ordinary validation for its selected strict/audit mode and extracts the sole
declared-topology applicability record and its native evidence. Its deterministic report has
separate structural (ambiguous mappings), relational (observed prohibited directed edges with
witnesses), unmapped, and stale-declaration sections. Reviewed out-of-scope subjects remain
visible evidence but are not reclassified as drift. It reports a typed error when the policy has
no declared topology.

The alternative—re-evaluating a captured JSON document—would lose type/context/layer facts and
could diverge from strict/audit validation. Live diff therefore shares the evaluator; captures
remain artifacts for review and documentation.

### Verify preserves normal validation semantics

`topology verify` calls the normal validation runtime once for the selected strict/audit mode and
renders its topology result. Its pass/fail exit state is the validation outcome's existing state;
the command adds no independent success criterion. This makes the CLI's focused topology view
and ordinary validation semantically identical.

### Output is versioned and byte-stable

Capture and diff JSON documents include a fixed document kind/version and ordered arrays. The
same unchanged inputs produce byte-identical JSON. Human output is deterministic but is not a
separate machine contract. Commands never target the policy input or an imported policy source as
their capture output destination.

## Risks / Trade-offs

- [Capture output can be mistaken for policy] → label it as a draft review artifact, omit policy
  write behavior, and document the explicit hand-authoring/review step.
- [A second session could disagree with validation] → share the evaluator's validation observation
  projection and run the focused verifier through the normal validation runtime once.
- [New public Core API drifts from reviewed surface] → add API approval coverage and use the
  explicit reviewed-public-API update lifecycle only after the behavioral tests pass.
- [Fixture-only proof misses CLI registration] → cover command catalog, handler output, and
  end-to-end realistic .NET and Unity scenarios.

## Migration Plan

The workflow is additive. Existing policies without `topology` retain their behavior; they can use
capture to inspect candidates, while diff and verify return an explicit no-topology diagnostic.
No migration or automatic policy rewrite occurs, and removing the new command/API restores prior
behavior without data migration.

## Open Questions

None. The command names, JSON-first artifact shape, and strict/audit delegation are fixed for this
change; detailed field names follow existing CLI JSON conventions and are locked by byte-stability
tests.
