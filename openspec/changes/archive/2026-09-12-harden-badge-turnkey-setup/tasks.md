## 1. Trust-bound configuration

- [x] 1.1 Extend the versioned setup model/schema/parser with immutable IDs, producer metadata, audience/approval fields, strict HTTPS/pin/bound/path validation, and generated-config self-validation.
- [x] 1.2 Add approved GitHub/provider capability inspection and evidence parsing; ensure provider-plan input is cost metadata only and missing proof is fail-closed.

## 2. Real generated bundle and wiring

- [x] 2.1 Render adopter-bound `wrangler.jsonc` and registry seed from actual inputs; remove all synthetic fixture values from generated output.
- [x] 2.2 Generate producer, trusted publisher, and optional metadata-only renewal workflows; compute exact producer Git blob SHA and emit correct Shields/SVG README regions.
- [x] 2.3 Preserve atomic, conflict-free writes and rollback while validating every generated artifact before commit.

## 3. Doctor and conformance

- [x] 3.1 Implement bounded doctor observations for identity, pins, OIDC, checks/rules, artifact/evidence, expiry, revocation, quota, and cache state; support fresh-unavailable and healthy-observed states.
- [x] 3.2 Add packed/conformance tests that execute generated Relay configuration and verify identities, producer digest, workflows, registry, README, schema, doctor, renewal, rollback, and redaction.
- [x] 3.3 Update issue/PR documentation and archived OpenSpec, run strict validation, and record downstream live-provider ownership without deferring these fixes.
