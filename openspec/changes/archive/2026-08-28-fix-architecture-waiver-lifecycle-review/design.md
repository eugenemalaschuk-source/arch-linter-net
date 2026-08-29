## Context

The initial #687 implementation treats expanded contracts as independent
waiver declarations even though their `IgnoredViolations` entries are shallow
copies of one authored object. It also validates malformed metadata before
the lifecycle evaluator can publish the required `invalid` evidence. Canonical
fingerprints must have one representation, and the human formatter must show
the structured values it asks reviewers to audit.

## Goals / Non-Goals

**Goals:**

- Aggregate lifecycle matching by authored waiver declaration across every
  selected expanded contract alias.
- Preserve deterministic, fail-closed `invalid` lifecycle evidence for malformed
  manual waivers.
- Enforce lowercase SHA-256 fingerprints and complete human waiver diagnostics.

**Non-Goals:**

- Change baseline semantics, #119/#121 gate composition, or the public
  lifecycle state vocabulary.
- Add downstream inventory, Health, report, or badge aggregation.

## Decisions

### Use authored declaration identity for uniqueness and lifecycle aggregation

The composed policy provenance object identity is shared by source-expanded
instances, so validation and lifecycle evaluation will use that authored
declaration identity rather than individual expanded descriptors. Matches are
aggregated with logical OR across the selected aliases before applying state
precedence. This retains one canonical record and prevents a matched alias from
being labelled stale because another alias was inspected first.

Alternative: deep-clone waiver objects per expansion. Rejected because it
destroys the direct authored-declaration relationship and would require a new
cross-instance grouping key.

### Emit invalid lifecycle evidence through the existing outcome path

Manual waiver validation will collect deterministic invalid records and report
them as fail-closed policy findings rather than throwing before an outcome can
be formed. The strict/compatibility profile does not downgrade malformed
structured data. Canonical records retain the available declaration/provenance
fields and use state `invalid`.

Alternative: keep load-time exceptions only. Rejected because canonical
downstream consumers cannot distinguish invalid waivers from missing evidence.

### Reject non-canonical fingerprints

The schema and model validator require `sha256:` plus lowercase hexadecimal.
This keeps YAML text equal to the generator's canonical value and removes a
configuration that validates but can never match.

### Render structured review fields directly

Human output will include `target` and `reason` in addition to existing
contract, source/reference, ownership, issue, expiry, state, and provenance.

## Risks / Trade-offs

- [Invalid policies need a partial validation outcome] → retain normal policy
  failure semantics while carrying deterministic canonical evidence.
- [Aliased contracts can have different selected scopes] → aggregate only the
  descriptors evaluated for the current mode/request.
- [Stricter fingerprint casing rejects prior user input] → this feature is new
  and docs publish the canonical generated lowercase form.
