## Context

The evaluator validates produced-record provenance and record compatibility but
currently trusts the provenance stored on an expected membership entry. See
proposal.md for the trust-boundary consequence.

## Goals / Non-Goals

**Goals:**

- Fail closed before a malformed expected entry can contribute a trusted join.
- Preserve a deterministic, canonical diagnostic identity for the affected
  expected control.

**Non-Goals:**

- Reject or throw while constructing a public expected-entry model.
- Infer a replacement policy identity from malformed provenance.

## Decisions

- Validate expected provenance in the evaluator's existing expected-identity
  defect phase. This preserves the model's ability to represent malformed
  producer input as evidence rather than turning it into an exception.
- Add a dedicated stable expected-integrity reason code. Reusing the
  record-integrity code would misdescribe the failed authority and make
  downstream handling ambiguous.
- Build the reason provenance from the expected entry's family and control
  identity, omitting the untrusted policy identity. This reports the affected
  canonical control without claiming an unverified policy source.

## Risks / Trade-offs

- [An additive public reason code requires snapshot review] → update the
  reviewed Core public API snapshot through its explicit lifecycle.
- [Malformed provenance remains visible on the input object] → assessment state
  and emitted integrity reason remain fail-closed and canonical.
