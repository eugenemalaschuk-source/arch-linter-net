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
| OpenSpec | `1.6.0` | exact npm package version |

## Upgrade procedure

Manual bootstrap pins must be upgraded in a dedicated pull request:

1. Review the upstream release notes, repository changes, and any security advisories.

1. For OpenSpec, verify the published package version against the upstream `package.json`, then update the exact version in:

   - `tools/scripts/install_unix_tools.sh`;
   - `tools/scripts/install_windows_tools.ps1`.

1. Do not replace a pin with `latest`, a moving branch such as `main` or `develop`, or an unqualified Git install.

1. Validate script syntax and the repository gates:

   ```bash
   bash -n tools/scripts/install_unix_tools.sh
   make acceptance
   ```

   On Windows, parse the installer PowerShell script and run `make acceptance` before merging.

1. Confirm the diff changes only the intended dependency pins and any required compatibility updates.

## Workflow token permissions

Workflows should define a read-only top-level permission default. Jobs may elevate only the scopes required for their operation. The release workflow currently keeps write permissions isolated to package authentication, GitHub Release creation, and GitHub Pages deployment jobs; those permissions must not be broadened merely to simplify configuration.
