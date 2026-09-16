# Generated consumer registry handoff (#918)

This is a pre-publication correctness fix in the #825 / #834 lane, not a
hosted Relay acceptance receipt or release authorization.

## Reproduced composition failure

Packed CLI `0.8.0-main.113` generates `configurations.setup_generated` in the
consumer's `.github/badge-promotion/registry.json`. Its publisher and renewal
templates pass that configuration identifier to the approved reusable workflow.
The old Python loader instead searches the upstream action's own registry.
A token-free loader probe using actual packed setup output failed with
`approved_configuration_invalid` at both the shipped publisher pin `ff9b19bf`
and audit source `3925e3be`. The consumer workspace cannot change the meaning
of an action's `__file__`, and `$/` references resolve in the defining repository.

## Corrected authority boundary

Only the reserved `setup_generated` identifier uses the caller registry. Existing
named reference configurations remain package-owned. Caller identity, revision
and branch come from GitHub's job environment, not a new workflow input.

The loader permits only `push` or `schedule` on a supported branch ref. It walks
the immutable commit's Git trees to prove that the fixed registry path is a
regular blob, then reads that exact revision through the Contents API. It rejects
symlinks, submodules, missing or ambiguous tree entries, truncated trees, invalid
encoding, sizes above 64 KiB, blob/size mismatch, duplicate JSON keys and invalid
configuration shapes. Repository/ref values must match the caller; repository
visibility is checked against provider metadata, not trusted from registry text.

The path requires a fixed six read-only API requests: commit, three tree levels,
contents and repository metadata. It does not follow caller-provided download
URLs, execute consumer code, check out a consumer tree, or fall back to workspace
files, a mutable branch tip, or another registry. Missing API authority fails
closed. Configuration data is not copied to the upstream project.

Loading configuration is not approval to publish. The existing resolver still
requires the exact squash/merged-tree relation, approved workflow blob, required
check, exact producer job/attempt, artifact integrity and semantic horizon before
a transport receives a payload. Configuration changes therefore remain reviewed
protected-branch authority; the resolver does not turn PR artifacts into policy.

## Remaining delivery work

The runtime fix alone does not update immutable installed publisher references.
After it is merged and reviewed, #835's distribution boundary must consistently
select a commit containing it (and #917), update the CLI/schema/bundle/inventory
pins and integrity digests, and re-run affected packed proofs. Never substitute
`main` or silently trust arbitrary SHAs. Do not change historical release assets.

#834 still owns complete distribution/platform/adversarial coverage and actual
isolated private GitHub -> Relay -> README acceptance, including expiry and
lifecycle operations. The earlier private `none` setup PASS and this loader fix
are narrower evidence. Neither closes #834, #836 or #825 or authorizes cloud
resources, final packages, tags or Pages deployment.
