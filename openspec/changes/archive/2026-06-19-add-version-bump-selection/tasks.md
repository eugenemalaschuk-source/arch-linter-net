## 1. Version Calculation Script

- [x] 1.1 Create `tools/release/calculate_version.py` with:
      - `SemVerVersion` dataclass with `major`, `minor`, `patch`, `preview` (optional int), `__str__`, and comparison operators
      - `detect_latest_tag()` function that runs `git tag`, parses output, filters to SemVer-compatible tags, returns highest by SemVer precedence
      - `calculate_next_version(latest_tag, release_type)` function implementing increment rules
      - CLI entry point using `argparse` with `--release-type` and `--version-override` arguments
      - Prints final `PACKAGE_VERSION` to stdout, exits with code 1 on error

## 2. Unit Tests

- [x] 2.1 Create `tests/release/test_calculate_version.py` with `unittest.TestCase` covering:
      - preview increments from last preview (`v0.1.1-preview.2` + preview → `0.1.1-preview.3`)
      - preview starts after stable (`v0.1.0` + preview → `0.1.1-preview.1`)
      - patch finalizes preview train (`v0.1.1-preview.2` + patch → `0.1.1`)
      - patch after stable increments patch (`v0.1.0` + patch → `0.1.1`)
      - minor bumps minor (`v0.1.0` + minor → `0.2.0`)
      - major bumps major (`v0.1.0` + major → `1.0.0`)
      - minor after preview produces stable (`v0.1.1-preview.2` + minor → `0.2.0`)
      - major after preview produces stable (`v0.1.1-preview.2` + major → `1.0.0`)
      - no valid tags + no override → SystemExit with code 1
      - no valid tags + override → override used
      - v-prefix stripped from output
      - invalid override rejected with clear error
      - invalid tags ignored (only SemVer tags considered)
      - latest version determined by SemVer precedence, not lexicographic sort
      - numeric prerelease parts compared as numbers (`preview.9` < `preview.10`)

## 3. Workflow Update

- [x] 3.1 Edit `.github/workflows/release-nuget.yml`: replace `version` input with `release_type` choice (preview, patch, minor, major) and optional `version_override` text input
- [x] 3.2 Remove `env.PACKAGE_VERSION: ${{ inputs.version }}` from job-level environment
- [x] 3.3 Add `fetch-depth: 0` to checkout step so tags are available
- [x] 3.4 Replace version validation step with version calculation step:
      ```yaml
      - name: Setup uv
        uses: astral-sh/setup-uv@v5

      - name: Calculate package version
        shell: bash
        run: |
          PACKAGE_VERSION="$(uv run python tools/release/calculate_version.py \
            --release-type "${{ inputs.release_type }}" \
            --version-override "${{ inputs.version_override }}")"
          echo "PACKAGE_VERSION=$PACKAGE_VERSION" >> "$GITHUB_ENV"
          echo "Calculated PACKAGE_VERSION=$PACKAGE_VERSION"
      ```
      Note: `setup-uv` is already present in the workflow from the release acceptance gate change — ensure it runs before the calculate step.
- [x] 3.5 Update upload-artifact name to use `${{ env.PACKAGE_VERSION }}` instead of `${{ inputs.version }}`

## 4. Documentation

- [x] 4.1 Update `docs/reference/release-process.md`:
      - Replace manual version input instructions with release-type dropdown flow
      - Add version calculation rules reference
      - Update dry-run and publication step examples
      - Document `version_override` as emergency recovery path

## 5. Validation

- [x] 5.1 Run tests: `uv run python -m unittest discover -s tests/release`
- [x] 5.2 Run `rtk make acceptance` to confirm no regressions
- [x] 5.3 Review all changes for correctness: test coverage, workflow YAML syntax
