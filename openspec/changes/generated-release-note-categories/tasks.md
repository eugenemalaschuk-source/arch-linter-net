## 1. Add release-note configuration file

- [ ] 1.1 Create `.github/release.yml` with changelog categories mapped to labels as defined in design.md
- [ ] 1.2 Include `breaking-change`, `enhancement`/`feature`, `bug`/`fix`, `documentation`, `ci`, and `dependencies` categories with correct priority ordering
- [ ] 1.3 Add `"*"` catch-all as the final section
- [ ] 1.4 Add `ignore-for-release` to `changelog.exclude.labels`
- [ ] 1.5 Add YAML comment warning that only one catch-all is permitted

## 2. Create missing labels

- [ ] 2.1 Create `breaking-change` label via `gh label create`
- [ ] 2.2 Create `dependencies` label via `gh label create`
- [ ] 2.3 Create `ignore-for-release` label via `gh label create`

## 3. Validation

- [ ] 3.1 Verify `.github/release.yml` syntax is valid YAML and follows GitHub release-notes configuration spec
- [ ] 3.2 Verify all referenced labels exist in the repository
- [ ] 3.3 Confirm `make acceptance` still passes locally
