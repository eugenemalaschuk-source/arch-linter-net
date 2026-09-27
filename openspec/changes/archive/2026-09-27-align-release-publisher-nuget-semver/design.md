## Context

`calculate_version.py` owns the NuGet package-version validation used by `version_override`. History-forensics was updated to share that parser, but `publish_release_assets.py` still keeps a local `_RELEASE_TAG` expression that permits only stable and `-preview.N` tags.

## Goals / Non-Goals

**Goals:**

- Reuse `parse_package_version` when validating the release tag.
- Continue requiring the canonical lowercase `v` prefix and a valid full package version.
- Cover `rc`, `alpha`, and build-metadata release tags with regression tests.

**Non-Goals:**

- Change Checkpoint B release-scope authorization or NuGet publication policy.
- Change GitHub Release tag naming or candidate/package identity binding.
- Broaden the version syntax beyond what the existing package-version parser accepts.

## Decisions

### Share the package-version parser

The publisher strips exactly the required leading `v` tag prefix and validates the remainder with `parse_package_version`. This keeps tag validation aligned with candidate preparation without introducing a second SemVer grammar. Invalid versions continue to fail before GitHub CLI calls.

## Risks / Trade-offs

- Build metadata can appear in an accepted version tag even though it does not affect SemVer precedence. The publisher preserves the full tag string as the immutable identity; predecessor selection separately handles precedence.

## Migration Plan

No workflow or bundle migration is needed. Existing stable and preview tags remain valid; previously rejected accepted package versions can now complete GitHub Release publication.
