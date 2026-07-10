# Dependency maintenance

This repository uses Dependabot for routine dependency discovery and reviewable update pull requests. It does not auto-merge dependency changes.

## Dependabot operating model

`.github/dependabot.yml` covers:

- NuGet manifests under the repository root;
- GitHub Actions references under `.github/workflows`.

Dependabot checks both ecosystems weekly on Monday in the `Europe/Paris` time zone. Minor and patch version updates are grouped by ecosystem to avoid a large number of small pull requests. Major updates remain separate because they require focused compatibility review. Pull-request limits provide a final bound on update noise.

Security updates remain subject to the repository's normal CI, CodeQL, SonarCloud, architecture, and maintainer-review gates. Dependency updates are never auto-merged.

## Manually pinned bootstrap dependencies

Some developer tools are installed by repository scripts rather than by a manifest supported by Dependabot. Their trust inputs are pinned explicitly:

| Tool | Reviewed version | Immutable input |
|---|---:|---|
| RTK | `v0.42.4` | release commit `8a7dd7e5570d7744d4b6508479a3674fe8c49286` |
| OpenSpec | `1.6.0` | exact npm package version |

RTK's Cargo fallbacks build the reviewed release commit. The Unix remote installer is loaded from that commit and receives the reviewed release version explicitly. The Windows fallback resolves the fixed release tag instead of the moving latest release.

## Upgrade procedure

Manual bootstrap pins must be upgraded in a dedicated pull request:

1. Review the upstream release notes, repository changes, and any security advisories.
2. For RTK, select a reviewed release tag and the exact upstream commit referenced by that release. Verify that the commit's `Cargo.toml` version matches the intended release.
3. Update `RTK_VERSION`/`RtkVersion` and `RTK_COMMIT`/`RtkCommit` together in:
   - `tools/scripts/configure_rtk_unix.sh`;
   - `tools/scripts/configure_rtk_windows.ps1`.
4. For OpenSpec, verify the published package version against the upstream `package.json`, then update the exact version in:
   - `tools/scripts/install_unix_tools.sh`;
   - `tools/scripts/install_windows_tools.ps1`.
5. Do not replace a pin with `latest`, a moving branch such as `main` or `develop`, or an unqualified Git install.
6. Validate script syntax and the repository gates:

   ```bash
   sh -n tools/scripts/configure_rtk_unix.sh
   bash -n tools/scripts/install_unix_tools.sh
   rtk make acceptance
   ```

   On Windows, parse the two PowerShell scripts and run `rtk make acceptance` before merging.
7. Confirm the diff changes only the intended dependency pins and any required compatibility updates.

## Workflow token permissions

Workflows should define a read-only top-level permission default. Jobs may elevate only the scopes required for their operation. The release workflow currently keeps write permissions isolated to package authentication, GitHub Release creation, and GitHub Pages deployment jobs; those permissions must not be broadened merely to simplify configuration.
