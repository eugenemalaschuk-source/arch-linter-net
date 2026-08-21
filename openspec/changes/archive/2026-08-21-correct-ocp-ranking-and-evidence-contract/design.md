## Context

OCP analysis already consumes the retained `G0`/bottleneck population, but its
sort comparison stops at canonical path after score. The recent OCP projection
requirement also uses wording broader than the evidence model emits.

## Goals / Non-Goals

**Goals:**

- Apply the shared canonical file-finding total order to OCP findings.
- Align OCP scope and evidence wording with implementation and existing ignore
  semantics.

**Non-Goals:**

- Add per-finding copies of all commit-level TaskKey match provenance.
- Change OCP score components, identity boundaries, or JSON schema beyond
  ordering.

## Decisions

- Retain raw ranking inputs from the canonical logical file in each OCP finding:
  ordinary TaskKey spread, churn, and commit count. This is necessary to apply
  the capability-wide ranking contract without recomputing evidence.
- Reword the OCP requirement to refer to retained, non-ignored logical files and
  pair-exclusive TaskKey-pair provenance. This matches the `G0` boundary and
  the evidence actually projected by OCP findings.

## Risks / Trade-offs

- [Equal-score report order changes] → Add a regression where path order
  conflicts with the required numeric tie-breakers.
- [Specification drift] → Archive the correction delta and validate the
  synchronized capability spec.
