## Context

The repository already has authoritative, typed seams for validation,
assessment completion, baseline debt, policy weakening, effective policy
inventory, metric budgets, declared topology, and normalized external findings.
Those seams deliberately avoid a universal score and several are attached to
different current command/application outcomes. See `proposal.md` for the
product motivation and the delta spec for behavior.

## Goals / Non-Goals

**Goals:**

- Project one reviewed, versioned summary from existing authority results.
- Keep required evidence assessability separate from conformance and debt.
- Make the same Core-owned result available through CLI human/JSON and the
  Testing builder.
- Reuse the established 0/1/2 gate exit categories.

**Non-Goals:**

- A second evaluator, baseline comparer, waiver lifecycle, policy inventory,
  applicability join, metric engine, or SARIF reader.
- SARIF output, PR Markdown, sticky publication, badge generation, or generic
  CI/service dashboards.
- New policy fields, health scores, percentages, letter grades, or automatic
  policy/baseline/waiver/budget edits.

## Decisions

### Compose typed receipts in Core

Add public health request, outcome, summary, dimension, state, and reason
types under Core's existing model/validation boundaries. The health application
service creates one existing analysis snapshot for the requested strict,
audit, or combined scope, obtains the existing debt-gate receipt for the same
request, and passes those immutable receipts to a pure health projector. The
projector reads only typed output fields; it never parses a policy, opens an
artifact, or calls a family evaluator.

This preserves the source-of-truth split: current validation determines
current conformance/completion; the baseline verifier owns reviewed/new finding
debt; the policy-weakening comparer owns change-time weakening; and policy
inventory owns manual-waiver debt. Duplicating their logic in a health command
would risk treating absent evidence as zero or collapsing independent debt.

### Preserve dimensions instead of inventing a score

The summary has a top-level gate and health classification plus deterministically
ordered, typed dimensions. Each dimension exposes its configured/assessable
state and stable reasons, with the source evidence retained for drill-down.
The projector applies only summary precedence; it does not reinterpret native
evidence or merge units. `not_configured` and `not_applicable` are explicit,
so a missing authority cannot look healthy.

The gate follows the established three states. Health describes the most severe
independent governance condition: unassessability first, then present
governance failure, regression/degradation, reviewed debt, and health. A
blocking debt-gate receipt remains independently visible rather than forcing
health to relabel reviewed and change-time evidence as ordinary current
conformance failure.

### Add a focused Health command and Testing entry point

Add a `health` top-level CLI command with policy, baseline, selected modes,
optional paired policy contexts, build-preparation, and human/JSON format
options matching the existing read-only `gate` command. It invokes the Core
health service once, renders the Core formatter result, and maps its gate to
the existing exit categories. Invalid invocation and runtime failures retain
the existing machine error envelope; valid unassessable health produces the
normal health model with exit 2.

Expose the same request through `ArchitectureEngine` and add a corresponding
`ArchitectureValidationBuilder` operation. This keeps NUnit consumers on typed
Core output rather than requiring them to parse CLI JSON. CLI-only rendering,
command-definition, docs, and focused CLI tests remain outside Core.

### Evolve reviewed public APIs deliberately

The new public Core and Testing members will be compared with the reviewed API
snapshots only after implementation. The integration owner will use the
explicit preview/update lifecycle, inspect the resulting drift, and then run
the read-only API check. This avoids treating a lint command as an API rewrite.

## Risks / Trade-offs

- [The gate receipt does not itself contain full assessment completion] →
  combine it with the same-request snapshot outcomes and retain the two
  authorities as separate dimensions.
- [Some families are optional or compatibility cache results may omit newer
  evidence] → preserve explicit absent/not-configured states; only configured
  required evidence can cause health unassessability.
- [Health orchestration evaluates canonical authorities rather than accepting
  ad-hoc JSON] → the command remains reproducible and does not trust stale or
  wrong-context serialized summaries, at the cost of using the normal
  read-only analysis path.
- [Public API expansion] → review and snapshot the exact public model after
  focused tests establish behavior.

## Migration Plan

The capability is additive. Existing validation, gate, measurement, and badge
commands retain their contracts. Users opt into the new `health` command or
the Testing-builder operation; no policy migration or persisted-data rewrite
is required.
