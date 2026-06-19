## 1. Add release-note configuration file

- [x] 1.1 Create `.github/release.yml` with changelog categories mapped to labels as defined in design.md
- [x] 1.2 Include `breaking-change`, `enhancement`/`feature`, `bug`/`fix`, `documentation`, `ci`, and `dependencies` categories with correct priority ordering
- [x] 1.3 Add `"*"` catch-all as the final section
- [x] 1.4 Add `ignore-for-release` to `changelog.exclude.labels`
- [x] 1.5 Add YAML comment warning that only one catch-all is permitted

## 2. Create missing labels

- [x] 2.1 Create `breaking-change` label via `gh label create`
- [x] 2.2 Create `dependencies` label via `gh label create`
- [x] 2.3 Create `ignore-for-release` label via `gh label create`

## 3. Validation

- [x] 3.1 Verify `.github/release.yml` syntax is valid YAML and follows GitHub release-notes configuration spec
- [x] 3.2 Verify all required labels exist in the repository
- [x] 3.3 Confirm `make acceptance` still passes locally
