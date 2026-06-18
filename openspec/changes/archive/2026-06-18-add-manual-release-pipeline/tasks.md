## 1. Pull Request CI Workflow

- [x] 1.1 Create `.github/workflows/ci.yml` with `pull_request` and `push` to `main` triggers.
- [x] 1.2 Configure CI workflow with read-only repository permissions.
- [x] 1.3 Add .NET setup for `10.0.x`.
- [x] 1.4 Add restore and full acceptance steps using `make restore` and `make acceptance`.
- [x] 1.5 Verify CI workflow does not call `dotnet pack`, request publishing identity tokens, publish packages, create tags, or create GitHub Releases.

## 2. Manual NuGet Release Workflow

- [x] 2.1 Create `.github/workflows/release-nuget.yml` with only a `workflow_dispatch` trigger.
- [x] 2.2 Add required `version` string input and required `publish` boolean input defaulting to `false`.
- [x] 2.3 Configure release workflow with minimal repository permissions.
- [x] 2.4 Add an early version validation step that rejects empty or obviously invalid package versions.
- [x] 2.5 Add .NET setup for `10.0.x`.
- [x] 2.6 Add restore, explicit Release build, and `make acceptance` steps before packing.
- [x] 2.7 Pack `ArchLinterNet.Core`, `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, and `ArchLinterNet.Unity` with `PackageVersion` set from the workflow input.
- [x] 2.8 Upload generated `.nupkg` files as workflow artifacts.
- [x] 2.9 Add a publish step guarded by `publish == true` that uses NuGet.org Trusted Publishing, NuGet.org source `https://api.nuget.org/v3/index.json`, and `--skip-duplicate`.
- [x] 2.10 Verify the NuGet package job has `id-token: write` and does not use a classic NuGet API key.
- [x] 2.11 Add a GitHub Pages documentation deployment job guarded by `publish == true` after successful NuGet publication.

## 3. Release Documentation

- [x] 3.1 Update `docs/reference/release-process.md` to describe preview versioning and the manual release workflow.
- [x] 3.2 Document the GitHub Actions UI dry-run procedure with `publish=false` and artifact inspection.
- [x] 3.3 Document the GitHub Actions UI public publication procedure with `publish=true`.
- [x] 3.4 Document NuGet.org Trusted Publishing setup and state that classic API keys are not used for automated publishing.
- [x] 3.5 Document that published package IDs, versions, and GitHub Pages deployment URL must be recorded in issue or PR notes.
- [x] 3.6 Remove or clearly mark old tag/GitHub Release automation as out of scope for the initial preview release process.

## 4. Validation

- [x] 4.1 Review both workflow files for separation: CI validates only, release workflow packs/publishes only manually.
- [x] 4.2 Run `rtk make restore` if needed for no-restore targets.
- [x] 4.3 Run `rtk make acceptance` and resolve any lint, architecture, formatting, or test failures caused by the change.
- [ ] 4.4 After merge, run the manual release workflow with `publish=false` and confirm versioned `.nupkg` artifacts upload successfully.
- [ ] 4.5 After artifact inspection, run the manual release workflow with `publish=true` and confirm packages are available from NuGet.org and documentation is deployed to GitHub Pages.
