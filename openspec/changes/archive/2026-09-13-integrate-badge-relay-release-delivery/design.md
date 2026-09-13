## Context

The existing `package_manifest.py` is the canonical authority for the four
NuGet packages and their symbols. The manual release workflow already freezes,
attests, verifies, and attaches those package subjects. Badge setup currently
ships Relay source and configuration assets inside `ArchLinterNet.Cli`, but
does not validate an internal file inventory, and the release workflow has no
transport-level subject set.

## Goals / Non-Goals

**Goals:**

- Keep NuGet package identity and Checkpoint B semantics unchanged.
- Add a small outer transport manifest that links to the existing package
  manifest, records exact transport subjects, and can be verified offline once
  downloaded.
- Produce deterministic archive bytes and independent attestation inventories.
- Ensure the installed CLI validates the same Relay source assets before setup.
- Make dependency lock/license evidence and release asset contents reviewable.

**Non-Goals:**

- Publishing a release, deploying Cloudflare resources, or changing provider
  behavior.
- Introducing a second release workflow, package manifest, provenance authority,
  or recursive manifest hash.
- Making a source checkout or mutable branch a supported consumer dependency.

## Decisions

1. **Use a sibling transport manifest, not an extension of the package
   manifest.** The package manifest remains exact and intentionally rejects
   extra root files. Transport files live under `artifacts/packages/transport`
   so existing package verification stays authoritative. A focused Python
   helper reuses package-manifest validation and implements only typed transport
   subject/archive mechanics.

2. **Freeze four transport subjects.** The candidate contains a deterministic
   `tar.gz` Relay distribution, the approved reusable publisher workflow and
   composite action bytes from their immutable bootstrap commit, and generated
   compatibility metadata. The outer transport manifest and derived checksums
   are evidence subjects, not recursive members of their own inventory.

3. **Bind compatibility to reviewed pins.** The checked-in release inventory
   declares `#806`, the `#825` handoff, the `badge-relay/v1` bundle,
   `badge-relay-config/v1`, promotion/publication protocol versions, storage
   migration `v1`, and the approved reusable workflow/action commit. Candidate
   metadata adds the exact CLI package version, source SHA, and package-manifest
   digest. The generator reads the pinned workflow/action bytes from Git and
   rejects floating references.

4. **Use deterministic archive construction.** Archive members are an explicit
   reviewed allowlist. Names are sorted, timestamps and ownership metadata are
   zeroed, and gzip metadata is fixed. Verification checks archive member names,
   regular-file safety, exact subject digests, and the derived checksum bytes.

5. **Reuse the existing provenance handoff.** Attestation and independent
   verification gain transport and transport-evidence inventories alongside the
   current package/evidence inventories. The release job verifies all three
   boundaries before NuGet push; GitHub Release attachment enumerates manifest
   paths only and never uses a glob.

6. **Validate installed Relay assets locally.** A checked-in
   `relay/bundle-manifest.json` declares the exact source/template/lockfile/
   notice set and approved pins. The CLI verifies those bytes before copying
   them into adopter output, preserving the existing generated bundle digest.
   No network lookup is involved in setup integrity validation.

## Risks / Trade-offs

- [Risk] A future Relay source or approved pin update requires synchronized
  manifest and package changes. → The bundle manifest and release inventory are
  checked by focused tests and the release generator fails on drift.
- [Risk] `npm audit` depends on the external npm advisory service. → The release
  gate also performs bounded offline lockfile/license checks; audit is limited
  to runtime dependencies and a bounded command timeout.
- [Risk] GitHub Release assets do not themselves prove post-publication
  consumer behavior. → #806 retains released-artifact verification ownership;
  this change only freezes and proves candidate bytes.

## Migration Plan

The next `release-nuget.yml` candidate run generates the transport directory and
attests it in parallel with the existing package subjects. Existing consumers
continue using the CLI package and generated workflows. No migration is needed
for existing Relay installations; future setup runs reject stale or altered
bundles before writes.
