## 1. Release authorization

- [x] 1.1 Extend reviewed release target validation to exact
  `X.Y.Z-preview.N` targets.
- [x] 1.2 Preserve exact declaration selection and required-item state binding.
- [x] 1.3 Prove preview authority cannot authorize stable or another preview.

## 2. Preview scope

- [x] 2.1 Add reviewed `0.9.0-preview.1` declaration for the current v0.9
  dogfood foundation.
- [x] 2.2 Record open stable/performance lanes explicitly as non-blocking only
  for this preview.

## 3. Documentation

- [x] 3.1 Document exact stable/preview publication authority.
- [x] 3.2 Document first-next-minor preview use of exact
  `version_override=0.9.0-preview.1`.

## 4. Validation

- [x] 4.1 Run the complete release-tool test suite.
- [x] 4.2 Run strict OpenSpec validation.
- [x] 4.3 Run repository/release CI and review the final preparation diff.

## Completion evidence

- Implementation/preparation PR: #1001.
- Exact preparation head CI: 35716205657 SUCCESS.
- Package Validation: 35716205659 SUCCESS.
- CodeQL: 35716205699 SUCCESS.
- Qodana Community: 35716205675 SUCCESS.
- Canonical spec synchronized before archival on 2026-09-22.
