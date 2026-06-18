## Context

ArchLinterNet is a public .NET architecture linter with four packages: Core, Cli, Testing, Unity. First Ice needs published preview packages from NuGet.org. Currently:

- No `.github/workflows/` directory exists.
- `Directory.Build.props` sets `VersionPrefix=0.1.0` and `VersionSuffix=preview.658`.
- `ArchLinterNet.Cli` has `PackAsTool=true` with `ToolCommandName=arch-linter-net`.
- `docs/reference/release-process.md` describes a manual tag/push flow that does not match the required two-step workflow_dispatch approach.
- All four `.csproj` files define `PackageId` already.

The requirement is explicit workflow separation: PR CI validates code only; release workflow is started from the GitHub Actions UI and builds versioned packages, optionally publishes packages, creates GitHub release records, and deploys documentation.

## Goals / Non-Goals

**Goals:**

- PR CI workflow runs `make restore` and `make acceptance` on every pull request and push to main.
- PR CI workflow has no access to `NUGET_API_KEY`, no `dotnet pack`, no publish, no tag/release creation.
- Release workflow runs only from the GitHub Actions UI via `workflow_dispatch` with `version` and `publish` inputs.
- Release workflow validates version input format before proceeding.
- Release workflow builds and tests in Release configuration with the explicit version.
- Release workflow packs all four projects with `PackageVersion=${{ inputs.version }}`.
- Release workflow uploads `.nupkg` files as workflow artifacts.
- Release workflow publishes to NuGet.org only when `publish=true`, using `NUGET_API_KEY` in that step only.
- Release workflow mirrors packages to GitHub Packages, creates or updates a GitHub Release, and deploys documentation to GitHub Pages when `publish=true`.
- Release workflow uses `--skip-duplicate` to prevent accidental double-publish failures.
- Release process documentation reflects the two-step dry-run/public procedure.

**Non-Goals:**

- Tag or GitHub Release automation.
- Automatic publication on merge.
- Publishing stable 1.0.0.
- Internal/private package feeds.
- Using GitHub Packages as primary storage; it is only a repository-visible mirror.
- Runtime code changes.

## Decisions

### Use separate workflow files, not conditional jobs in one file

Two distinct files (`ci.yml` and `release-nuget.yml`) instead of one workflow with conditional logic.

Alternatives considered:

- Single workflow with `if: github.event_name == 'workflow_dispatch'` for release jobs: simpler file count, but mixes security contexts and makes PR permissions harder to constrain.
- Reusable workflow called from both: adds indirection for no real benefit at this scale.

Separate files make the permission boundary explicit and easier to audit.

### Run full acceptance in PR CI

The PR workflow uses `make restore` followed by `make acceptance` instead of only raw `dotnet build` and `dotnet test` commands.

Alternatives considered:

- Raw restore/build/test only: faster and simpler, but misses docs, architecture, formatting, and code-size checks that already define repository acceptance.
- Duplicate every acceptance command in YAML: more explicit, but causes drift from the Makefile.

Using `make acceptance` keeps the PR gate aligned with local validation.

### Pack all four projects in release workflow

All four package IDs are already defined in `.csproj` files and listed in issue requirements. Pack all of them rather than trying to detect which ones First Ice needs.

Alternatives considered:

- Pack only Core + Cli (minimum for First Ice): reduces artifacts, but issue explicitly lists all four package IDs.
- Make package list a workflow input: more flexible, but adds complexity and the issue says recommended package projects are the four listed.

Packing all four is simpler and matches the issue specification.

### Pass explicit version via MSBuild PackageVersion property

The release workflow passes `-p:PackageVersion=${{ inputs.version }}` at pack time. For build and test steps, also pass `-p:Version=${{ inputs.version }}` to ensure consistent versioning throughout.

Alternatives considered:

- Modify `Directory.Build.props` in a commit before release: creates extra commits, messy history.
- Use a build matrix or script to update props: more moving parts, same result.

MSBuild property override is the standard .NET approach for CI version injection.

### Validate version input with a regex check

Add a step that rejects empty or obviously invalid version strings before any build work starts. Use a simple regex pattern to catch obviously bad input.

Alternatives considered:

- Use `dotnet pack` and let it fail: slower feedback, no clear error message.
- Accept any string and let NuGet.org reject it: worse UX, wastes build minutes.

Early validation is cheap and improves developer experience.

### Publish release records from GitHub UI workflow

Official publication is initiated from the GitHub Actions UI, not from local machines. When `publish=true`, the workflow publishes to NuGet.org, mirrors to GitHub Packages, creates or updates a GitHub Release, and deploys docs to GitHub Pages.

Alternatives considered:

- Local `make publish`: useful for internal workflows, but weaker auditability and easier to run from an unprepared environment.
- GitHub Release-only distribution: fills the repository UI, but does not satisfy NuGet consumption.

The GitHub UI workflow gives a single audited release path while still populating repository Releases and Packages.

## Risks / Trade-offs

- [Secret exposure in PR context] → PR CI workflow has no `NUGET_API_KEY` in environment. Release workflow accesses secrets only in publish-only steps with explicit `if` conditions.
- [Version format mistakes] → Input validation step rejects obviously invalid versions before build starts.
- [NuGet.org downtime during publish] → `--skip-duplicate` prevents double-publish on retry. Actual NuGet.org outages are outside control; operator retries manually.
- [Stale workflow after .csproj changes] → Workflow packs all projects from `src/`, so new projects are automatically included. Removing a project requires workflow update.
- [First Ice depends on specific version] → Published package IDs and versions must be recorded in issue/PR notes as acceptance criteria.
