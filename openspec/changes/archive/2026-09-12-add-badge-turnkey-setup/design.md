## Context

The accepted #826 ADR makes the existing canonical CLI projection the sole owner of Gate, Health, counts, disclosure bytes, and semantic validity. Merged #830 supplies the reusable trusted promotion workflow and #828/#831 supply the Relay identity/storage and read-time-expiry contracts. The repository currently ships those runtime pieces plus packed Relay fixtures, but no consumer-facing setup, deploy bundle, or doctor command.

## Goals / Non-Goals

**Goals:**

- Add one versioned setup/doctor contract that can be invoked from the packed CLI and can generate a deterministic consumer project.
- Keep setup orchestration separate from canonical Health evaluation and trusted promotion; setup may validate and compose contracts but must not recompute Health or invent provenance.
- Make local output reviewable, atomic, idempotent, and safe for repeated attempts, with explicit `none` behavior and bounded Relay renewal cost.
- Ship the exact Relay source/configuration/migration inputs and schema metadata required by the generated deployment manifest.

**Non-Goals:**

- Operating a project-owned Relay, provisioning accounts, creating real cloud resources without explicit operator credentials, or publishing a release.
- Changing canonical Health/Gate semantics, the existing public raw endpoint, the Relay protocol, or the protected promotion workflow's trust implementation.
- Supporting arbitrary hosting providers, custom authenticated transports, ordinary GitHub Pages deployment, or silent fallback from an unsupported prerequisite.

## Decisions

1. **Use a nested CLI setup surface.** Add `badge architecture-health setup` and `badge architecture-health doctor` as the stable user entry points, with machine-readable JSON as the default contract and concise human output available through an explicit format option. This keeps setup beside the badge producer while avoiding a second top-level product concept.

   Alternatives considered: a standalone Python installer would not be available from the packed .NET tool; a top-level `setup` command would duplicate the existing badge domain and complicate help/schema discovery.

2. **Represent setup as a typed plan before writes.** Parse a closed `badge-relay-config/v1` document into a typed plan, validate visibility/profile/identity/limits, resolve shipped assets, and render all outputs in memory. A plan digest and component manifest are written with the managed files so repeated runs can compare approved state without overwriting unmanaged edits.

   Alternatives considered: free-form YAML would permit consumer-selected producers and unstable defaults; direct incremental writes would leave half-authorized destinations after failures.

3. **Make the generated project an explicit bundle projection.** The bundle contains the existing Relay `src`, `wrangler` configuration, package lock metadata, migrations, and fixed setup templates, selected by a manifest entry with SHA-256 digests. The CLI copies only allowlisted regular files and never shells out to package managers, executes generated code, or requires a source checkout.

   Alternatives considered: downloading a mutable repository branch would violate candidate reproducibility; embedding a second Relay implementation would create a trust-boundary fork.

4. **Use managed-region patching for consumer files.** Generated workflow and README changes use clearly delimited managed blocks and refuse ambiguous or conflicting existing blocks. Existing branch protection, secrets, workflow names, and content outside the managed block are preserved. `none` generates no public route and no deployment credentials.

   Alternatives considered: replacing entire workflow/README files is destructive; unconstrained text insertion makes repeated setup non-deterministic.

5. **Separate local diagnostics from public-safe diagnostics.** Internal doctor results may include path and provider capability context, while public rendering is projected through a closed code/message allowlist. Tokens, raw responses, source identity, and provenance never enter either generated URLs or public output.

## Risks / Trade-offs

- [Risk] Cloud provider account/plan and rules API behavior cannot be proven offline. → [Mitigation] setup validates only explicit local prerequisites and returns `unavailable`/actionable diagnostics when live capability proof is required; it never claims a deployment succeeded from a template alone.
- [Risk] A generated bundle can drift from the Relay runtime. → [Mitigation] bind the bundle manifest to exact shipped file digests, run bundle conformance tests, and make unknown compatibility identifiers fail closed.
- [Risk] Managed-block patching can conflict with manual edits. → [Mitigation] detect duplicate/partial markers, refuse the write atomically, and report the exact file and remediation instead of overwriting.
- [Risk] The feature spans CLI, templates, Relay, schemas, and docs. → [Mitigation] keep one typed setup plan as the integration seam, use disjoint implementation ownership for the setup engine and CLI/docs wiring, and run focused plus cross-cutting validation.

## Migration Plan

Existing public `github-raw` users and the legacy `architecture-policy` command are unchanged. Consumers adopt setup by running the versioned command against a new output directory or by opting into the managed workflow/README blocks; existing files are preserved unless an explicit conflict-free managed region is selected. If a generated deployment must be removed, revoke/tombstone the Relay alias through its owner flow before deleting local files; do not promise removal of already cached public copies.

## Open Questions

None. Account-specific identifiers and approvals are runtime inputs, not unresolved product-design decisions.
