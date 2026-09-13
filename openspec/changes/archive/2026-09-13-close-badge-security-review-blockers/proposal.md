## Why

The follow-up security review of PR #858 found three remaining trust-boundary
defects in the #832 turnkey setup path: caller-supplied reusable-workflow pins
can redirect a write-capable publisher, `--capability-evidence` accepts an
unsigned local assertion as live proof, and the live OIDC inspector checks JWT
claims without authenticating the JWT signature. The managed writer also still
needs to reject filesystem-link escapes before it reads or writes consumer
files.

These are implementation blockers for #832. They cannot be deferred to the
downstream provider acceptance work because they determine which workflow and
which capability facts the generated trust contract accepts.

## What Changes

- Lock v1 publisher workflow and action pins to the shipped contract defaults;
  user input may not select another reusable publisher or action.
- Reject unsigned local capability-evidence JSON as proof and require the live
  inspector for setup capability decisions.
- Verify live GitHub OIDC JWTs as RS256 tokens against the fixed GitHub issuer
  JWKS before evaluating their identity, workflow, audience, and time claims.
- Reuse the repository filesystem containment guard for managed output reads
  and writes, rejecting symlink, junction, and reparse-point traversal.
- Add regression coverage for malicious pins, unsigned evidence, invalid JWT
  signatures, trusted NuGet installation ordering/configuration, and linked
  output subtrees.

## Impact

This is a security hardening change to the archived `badge-turnkey-setup`
capability. It preserves the CLI option name for compatibility but no longer
treats an unsigned `--capability-evidence` file as capability proof. It does
not publish a package, deploy a provider resource, or merge PR #858.
