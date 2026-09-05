# Versioning and Release Meaning

ArchLinterNet follows Semantic Versioning 2.0, with an additional public release-train convention that explains what users should expect from each published version.

The project is still in the `0.x` initial-development range. Under SemVer, a `0.y.0` release may change compatibility. Always review the release notes before upgrading across minor versions.

## Release types

| Version shape | Public meaning | What users should expect |
| --- | --- | --- |
| `0.Y.0` | **Capability release** | A reviewed user-facing capability increment. It may add features, extend policy/schema/CLI behavior, and may include compatibility changes appropriate to pre-1.0 development. |
| `0.Y.Z`, `Z > 0` | **Maintenance release** | Stabilization of the already-released `0.Y` capability line: correctness/integration fixes, reliability fixes, documentation corrections, and behavior-preserving engineering cleanup. Unrelated next-minor capability work is intentionally excluded. |
| `*-preview.N` | **Preview release** | Early validation of a future public release. Behavior and compatibility may still change before the stable release. |
| `X.Y.Z-main.N` | **Development/dogfood build** | An installable build from `main` used for repository-authorized dogfooding. It is not a public release candidate and is not a stable NuGet.org release. |

For a released minor line, consumers should normally prefer the latest available patch version unless a specific repository has reviewed and pinned an older version for a documented reason.

## Capability releases

A capability release such as `0.8.0` is the point where ArchLinterNet publishes a coherent new product increment.

Before publication the release is expected to have:

- the reviewed capability scope implemented;
- public documentation synchronized with the shipped CLI/API/schema behavior;
- packed-artifact and consumer-shaped release acceptance completed;
- an immutable candidate that passed the repository's public release authority.

A capability release does **not** promise that no later patch will be needed. Real repositories can still expose adoption defects that were not reproduced by pre-release acceptance.

When upgrading across capability releases, review the GitHub Release notes and the evergreen [Adopt or Upgrade ArchLinterNet](../guides/upgrading.md) guide before changing policy or automation.

## Maintenance releases

A maintenance release such as `0.8.1` keeps the same `0.8` capability line and distributes stabilization work.

Maintenance releases may contain:

- correctness or integration fixes discovered during real adoption;
- release/reliability fixes;
- behavior-preserving architecture refactoring;
- behavior-preserving maintainability cleanup;
- documentation corrections required by those changes.

They are not used as a shortcut for unrelated capabilities planned for the next minor release.

There is no promise that every minor release has exactly one patch. A capability line may require zero, one, or several maintenance releases. The number is driven by actual adoption and stabilization needs.

## Why a patch can contain internal refactoring

A patch is not limited to a one-line bug fix. ArchLinterNet deliberately separates broad behavior-preserving engineering cleanup from the critical path of a capability release when that cleanup can be deferred safely.

Such refactoring may therefore be distributed in a maintenance release when it preserves supported public behavior. User-facing compatibility changes or new unrelated product capability still belong to the appropriate capability release and must be called out explicitly in release notes.

## Real-adoption fixes

Packed consumer-shaped acceptance is designed to catch installation, CLI-composition, platform, and external-repository issues before publication. It reduces risk but cannot prove every real adoption environment.

If a published capability release exposes a correctness or integration blocker in a real consumer, that is treated as product stabilization rather than optional technical-debt cleanup. The fix is shipped through the maintenance line, and additional patches may follow until the released capability line is practically adoptable.

## Reading release notes

Every public release should identify its release type near the top of the GitHub Release notes:

- **Capability release** — new reviewed capability line;
- **Maintenance release** — stabilization of an existing capability line;
- **Preview release** — early validation before stable publication;
- **Major release** — a new major compatibility line when ArchLinterNet reaches that stage.

The generated change categories below that summary describe the concrete delta: Features, Fixes, Documentation, CI/CD, Dependencies, Breaking Changes, and other reviewed changes.

NuGet package metadata also carries a short release-type summary and links to the full GitHub Release notes and this versioning policy.

## Upgrade guidance

When deciding whether to update:

1. **Patch within the same minor line:** normally update to the latest patch after reviewing the release notes. The intent is a more stable version of the same capability line, not unrelated new feature scope.
2. **Move to a new minor:** treat it as a deliberate capability upgrade. Review new features, compatibility notes, policy/schema changes, and required adoption steps.
3. **Use a preview:** only when intentionally evaluating the next release before stable publication.
4. **Use `main.N`:** only for authorized development/dogfood workflows that explicitly need an unreleased `main` state.

Pin the exact package/tool version that your repository has reviewed. Do not infer persisted schema or machine-contract versions from package SemVer; discover those contracts from the installed CLI as described in the upgrade guide.

## What SemVer does and does not mean here

This policy strengthens the project's communication around SemVer; it does not redefine SemVer itself.

In particular, while ArchLinterNet remains below `1.0.0`:

- minor capability releases may contain compatibility changes;
- patch releases are intentionally maintenance-oriented and exclude unrelated next-minor capability work;
- release notes remain the authority for the concrete user-visible delta of a specific version;
- persisted document/schema identifiers have their own compatibility lifecycles and are not derived mechanically from package version numbers.
