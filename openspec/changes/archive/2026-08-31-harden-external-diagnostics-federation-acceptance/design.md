## Context

The #523 synthetic federation suite already exercises the reader, selector, normalized finding,
baseline, output, Testing, and shared applicability seams. Review found that the contract treats
the shared applicability projection as though it carries a selected diagnostic's full provenance,
although that projection deliberately contains only one policy-control state, reason codes, and
policy provenance.

## Goals / Non-Goals

**Goals:**

- Preserve the existing one-model applicability seam while stating its actual bounded contract.
- Prove source-location identity independently of message and fingerprint differences.
- Prove `rule_ids` authorization from trusted source selection through all materialized result
  projections.
- Keep the archived OpenSpec history and current specification connected by a standard archive.

**Non-Goals:**

- Add diagnostic provenance fields or a second result model to applicability.
- Change production trust, filtering, identity, normalization, baseline, or reporting behavior.

## Decisions

- Keep diagnostic provenance assertions on canonical finding, baseline, Human, JSON, SARIF, and
  Testing projections. Applicability assertions cover control state, empty or mapped reason
  codes, and general policy provenance only. This matches the existing shared model and avoids a
  test-only envelope.
- Use a pair of source results that differ only by location while retaining the same logical
  context, rule, severity, project, message, and source fingerprint. This makes loss of location
  from canonical identity directly observable.
- Use one selected and one unselected rule with the same source severity. Assert the excluded rule
  is absent from selection, normalized findings, baseline candidates, and rendered outputs.
- Record the contract correction as a new delta and archive it, rather than mutating the previous
  archived delta. The archive will explain the current-spec change in immutable history.

## Risks / Trade-offs

- [A delta requirement can replace too much text during archive] → Copy each complete current
  requirement into the modified delta before changing it.
- [Rendering assertions could accidentally pass because another finding contains the same text] →
  use distinct selected and excluded rule IDs and assert the complete rendered output omits the
  excluded ID.
