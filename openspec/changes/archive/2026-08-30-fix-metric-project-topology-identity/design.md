## Context

See proposal.md. The ordinary topology evaluator still supports legacy
validation behavior whose `project` selector is an output assembly simple
name. Metric semantics are stricter: contributor and relation ownership must
come from the resolved artifact binding introduced for #517. The reviewed API
snapshot has a separate repository-owned update mechanism.

## Goals / Non-Goals

**Goals:**

- Ensure metric project subjects and external-source identity lookup use the
  normalized project path from one exact resolved artifact binding.
- Fail closed when the legacy policy-facing simple-name selector cannot
  distinguish those canonical project subjects.
- Regenerate the full reviewed Core API baseline rather than editing a partial
  textual diff.

**Non-Goals:**

- Changing normal validation topology semantics or the existing policy selector
  syntax.
- Adding a project-path selector to policy YAML.
- Changing the CLI report schema or metric catalog.

## Decisions

### Keep canonical and policy-facing project identities separate

Metric-only topology observation will preserve two values for a project
subject: the normalized discovered project path for subject identity,
deduplication, dependencies, and contributors; and the legacy assembly simple
name for evaluating existing policy selectors. This avoids a breaking YAML
change while removing discovery-order ownership from the metric projection.

The alternative—replacing the policy selector spelling with project paths—would
break existing policies and requires a schema migration. Reusing the legacy
simple name internally leaves the reported bug intact.

### Make duplicate legacy selector matches unassessable

If a selected project node covers more than one distinct canonical project
identity via the same simple-name project selector, measurement has no policy
authority to allocate them separately. The evaluator will add
`missing_required_input` rather than merge them into a trusted contributor set.
This is deliberately metric-only; ordinary validation keeps its established
behavior.

### Regenerate the approval fixture from its canonical surface description

The Core approval fixture is independent from the policy-owned
`architecture/api` snapshots updated by `make public-api-update`. Regenerate
its complete contents from `CorePublicApiSurfaceApprovalTests`' canonical
reflection description, verify the generated output differs only where the
public models changed, and commit the resulting fixture. No hand-written
partial baseline is accepted.

## Risks / Trade-offs

- [A deliberately broad existing project selector becomes unassessable when
  duplicate output names appear] → This is the required fail-closed behavior;
  users can eliminate duplicate outputs or choose a different topology scope.
- [Metric and validation internal projections differ] → The split is narrow,
  documented, and covered by direct metric regression tests; validation keeps
  its compatibility behavior.
