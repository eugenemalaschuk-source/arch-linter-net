## 1. Pull Request CI Workflow

- [ ] 1.1 Create `.github/workflows/ci.yml` with `pull_request` and `push` to `main` triggers.
- [ ] 1.2 Configure CI workflow with read-only repository permissions.
- [ ] 1.3 Add .NET setup for `10.0.x`.
- [ ] 1.4 Add restore, Release build, and Release test steps using `dotnet restore`, `dotnet build --configuration Release --no-restore`, and `dotnet test --configuration Release --no-build`.
- [ ] 1.5 Verify CI workflow does not call `dotnet pack`, read `NUGET_API_KEY`, publish packages, create tags, or create GitHub Releases.

## 2. Manual NuGet Release Workflow

- [ ] 2.1 Create `.github/workflows/release-nuget.yml` with only a `workflow_dispatch` trigger.
- [ ] 2.2 Add required `version` string input and required `publish` boolean input defaulting to `false`.
- [ ] 2.3 Configure release workflow with minimal repository permissions.
- [ ] 2.4 Add an early version validation step that rejects empty or obviously invalid package versions.
- [ ] 2.5 Add .NET setup for `10.0.x`.
- [ ] 2.6 Add restore, Release build, and Release test steps using the explicit version input where appropriate.
- [ ] 2.7 Pack `ArchLinterNet.Core`, `ArchLinterNet.Cli`, `ArchLinterNet.Testing`, and `ArchLinterNet.Unity` with `PackageVersion` set from the workflow input.
- [ ] 2.8 Upload generated `.nupkg` files as workflow artifacts.
- [ ] 2.9 Add a publish step guarded by `publish == true` that uses `NUGET_API_KEY`, NuGet.org source `https://api.nuget.org/v3/index.json`, and `--skip-duplicate`.
- [ ] 2.10 Verify `NUGET_API_KEY` is scoped only to the publish step.

## 3. Release Documentation

- [ ] 3.1 Update `docs/reference/release-process.md` to describe preview versioning and the manual release workflow.
- [ ] 3.2 Document the dry-run procedure with `publish=false` and artifact inspection.
- [ ] 3.3 Document the public publication procedure with `publish=true`.
- [ ] 3.4 Document NuGet.org API key setup using repository secret `NUGET_API_KEY`.
- [ ] 3.5 Document that published package IDs and versions must be recorded in issue or PR notes.
- [ ] 3.6 Remove or clearly mark old tag/GitHub Release automation as out of scope for the initial preview release process.

## 4. Validation

- [ ] 4.1 Review both workflow files for separation: CI validates only, release workflow packs/publishes only manually.
- [ ] 4.2 Run `rtk make restore` if needed for no-restore targets.
- [ ] 4.3 Run `rtk make verify` and resolve any lint, architecture, formatting, or test failures caused by the change.
- [ ] 4.4 After merge, run the manual release workflow with `publish=false` and confirm versioned `.nupkg` artifacts upload successfully.
- [ ] 4.5 After artifact inspection, run the manual release workflow with `publish=true` and confirm packages are available from NuGet.org.
