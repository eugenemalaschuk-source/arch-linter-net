## Context

The first #832 implementation copied the Relay fixture's `wrangler.jsonc`,
used the upstream repository commit as `producer.workflow_sha`, treated a
declared provider plan as all capability proof, and generated only one
publisher-shaped workflow. The trusted promotion contract requires the exact
consumer producer workflow blob SHA, and Relay OIDC requires immutable adopter
identity and workflow claims.

## Decisions

1. **Make the generated consumer workflow the producer authority.** Setup emits
   a dedicated `.github/workflows/architecture-health-badge-producer.yml` using
   the canonical CLI workflow, and computes its Git blob SHA with the exact
   UTF-8 bytes written. Registry `producer.workflow_sha` is this blob SHA;
   reusable publisher pins remain separate fields.

2. **Parameterize the Relay bundle at generation time.** The shipped TypeScript
   source remains generic, while `wrangler.jsonc` is rendered from a strict
   template containing only adopter-supplied IDs, alias, endpoint/audience,
   disclosure profile, and exact workflow binding. Synthetic fixture values are
   forbidden in output.

3. **Separate declaration from proof.** `--provider-plan` contributes only to
   cost estimation. A non-dry-run Relay setup requires a capability evidence
   document produced by the GitHub/provider inspector; missing, stale, or
   contradictory facts remain unavailable. No option name can assert a check,
   Rules API, OIDC, quota, or deployment success.

4. **Use a bounded doctor observation protocol.** Doctor reads local generated
   state and accepts a fresh, identity-bound observation produced by the
   approved inspector for remote-only facts. It reports fresh installations as
   unavailable until first evidence, while accepting a shape-validated
   observation for a healthy state. Public projection remains redacted.

5. **Validate before writing.** The same closed schema is applied to input and
   generated config. Every managed path is normalized and confined below the
   output root; all conflicts are found before writes and rollback restores
   original bytes after a later write failure.

## Non-goals

- Operating a project-owned Relay or silently provisioning cloud resources.
- Changing canonical Health, promotion, OIDC cryptography, or Relay lifecycle
  semantics.
- Moving these correctness defects to downstream #834 or released-artifact
  #806 validation.
