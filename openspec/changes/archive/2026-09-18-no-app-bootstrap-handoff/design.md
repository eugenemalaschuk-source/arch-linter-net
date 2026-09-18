## Context

The current trusted bootstrap clones the exact consumer base ref, runs the
packed pinned CLI with its workflow-dispatch OIDC inspection, generates only
managed setup output, then uses a GitHub App token to push a deterministic
branch and create a PR. The App is not involved in capability inspection,
Relay registration, or subsequent OIDC publication, but its private key blocks
first adoption.

The predecessor `protected-bootstrap-pr` OpenSpec change deliberately selected
the App writer to avoid direct base-ref writes. This change supersedes that
writer decision while retaining the protected-branch invariant.

## Goals / Non-Goals

**Goals:**

- Keep trusted bootstrap's exact checked-out base tree, pinned CLI, OIDC
  inspection, required-check validation, and managed-output allowlist.
- Emit a private GitHub Actions artifact containing the generated output and a
  canonical handoff manifest bound to the base ref/SHA/tree, shipped publisher
  pins, configuration identity, and every managed file digest.
- Give an adopter a packaged CLI command that verifies the handoff before
  atomically applying it to a local checkout based on that exact tree; the
  adopter then opens and merges its normal PR.
- Remove all App credentials and all consumer remote-write operations from
  bootstrap.

**Non-Goals:**

- Do not make the handoff a public artifact, create a bot PR, push any ref,
  merge a PR, introduce a PAT, or run consumer-controlled code in the trusted
  workflow.
- Do not change the Relay protocol, required-check/evidence verifier, OIDC
  publisher, disclosure model, or ongoing publication/renewal path.

## Decisions

1. **A private artifact is the bootstrap boundary.** After trusted setup
   completes, the reusable workflow writes a closed handoff directory and
   uploads it with the repository's immutable pinned upload-artifact action.
   GitHub Actions artifacts are immutable once uploaded and stay within the
   consumer repository's normal access boundary. This replaces a GitHub App
   token, not the existing GitHub OIDC token.

2. **The manifest is content and context bound.** It contains a versioned
   schema, the exact base ref/commit/tree, expected repository identity and
   shipped publisher pins, and a sorted allowlisted file table of UTF-8 bytes,
   lengths, and SHA-256 digests. The workflow constructs it only after the
   existing setup command succeeds. It carries no secret, raw OIDC token,
   provider credential, full provenance receipt, or public URL.

3. **Apply is a packaged CLI operation, not shell extraction.** The new
   `badge architecture-health apply-handoff` command consumes a manifest and
   sibling payload directory. It rejects unknown schema, duplicate/missing or
   extra files, unsafe paths, symlinks/reparse points, invalid UTF-8, digest
   mismatch, shipped-pin mismatch, wrong repository identity, or a base
   commit/tree that differs from the caller's explicit expected values. It
   reuses the setup writer's atomic managed-file boundary, so a failed apply
   leaves no partial output.

4. **The owner supplies normal review, not a privileged writer.** Documentation
   instructs the owner to create a local branch at the handoff's exact base,
   run the verifier/apply command, inspect the diff, and open the repository's
   ordinary PR. Branch protection remains the only path into `main`.

5. **GitHub API/Actions credentials remain read-only at bootstrap.** The
   reusable workflow retains the minimum evidence/OIDC permissions already
   required for trusted setup but no longer requests a write-capable App token
   or invokes remote git/PR creation. The generated publisher retains its
   existing separately reviewed runtime permissions and exact pins.

## Risks / Trade-offs

- [Owner must create a normal PR] → This is intentional: it replaces an
  invisible privileged writer with a reviewable owner action and never writes
  to the base ref automatically.
- [Artifact is copied or modified locally] → The packaged verifier compares a
  closed manifest, file digests, paths, identity, pins, and base tree before
  writing. A stale base needs a new trusted bootstrap.
- [Artifact download is an extra first-setup step] → The workflow documents a
  concise normal-PR handoff; recurring OIDC publication has no artifact step.
- [Existing App callers exist] → Removing the optional inputs is safe because
  their only supported purpose was bootstrap branch/PR writing. A new exact
  publisher pin is required for adopters after this change.

## Migration Plan

1. Release a new candidate containing the workflow, CLI, templates, and
   documentation.
2. Existing App-based bootstrap callers may remove their writer-secret mappings;
   no generated installation requires an App at runtime.
3. Run trusted bootstrap using the new pin, download its private handoff, and
   apply it on an exact-base normal consumer branch.
4. Merge only through normal consumer review, then prove the existing OIDC
   publication and Relay lifecycle. Rollback restores the previous pinned
   candidate; it does not write or remove consumer refs.

## Open Questions

None. The artifact remains private to the consumer repository and the normal
PR remains intentionally owner-operated.
