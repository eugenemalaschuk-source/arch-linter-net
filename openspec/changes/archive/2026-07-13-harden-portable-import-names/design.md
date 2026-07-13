## Context

The portable authored-path grammar rejects exact Windows device names but does
not currently reject their extension-bearing forms. Windows treats the basename
before the first dot as a device name even when an extension is present. The
implementation handoff also asks #281 to assert a provenance-rich duplicate-ID
diagnostic even though #282 owns provenance in diagnostics and output.

## Goals / Non-Goals

**Goals:**

- Make authored import segments portable across Windows, Linux, and macOS by
  rejecting every Windows-reserved basename before filesystem access.
- Keep the #281/#282 boundary executable: #281 validates duplicate rejection;
  #282 validates the sources included in the resulting diagnostic.

**Non-Goals:**

- Implement runtime imports, modify production schemas, or change the
  `DuplicateIdValidator`.
- Broaden path normalization or alter repository-boundary semantics.

## Decisions

### Compare the basename before the first dot

For a normal segment, take the substring preceding its first `.` and compare it
case-insensitively against `CON`, `PRN`, `AUX`, `NUL`, `COM1` through `COM9`,
`LPT1` through `LPT9`, `COM¹` through `COM³`, and `LPT¹` through `LPT³`.
Reject the segment on a match before any host path API is invoked. This matches
Windows device-name behavior and prevents an authored graph from acquiring
host-specific interpretation.

Checking only an exact full segment was considered but rejected because it
admits `NUL.yml` and equivalent extension-bearing device names. Delegating to
host APIs was rejected because Unix hosts do not reject these names.

### Keep diagnostic-source assertions with provenance ownership

#281 must prove that a duplicate case-insensitive ID in one family/mode group
is rejected after composition. #282 must prove that the final diagnostic names
both origin descriptors. This preserves the existing separation between
composition/validation and provenance-to-diagnostic mapping.

## Risks / Trade-offs

- [Some Unix-valid paths become invalid imports] → The restriction is
  intentional because authored policy graphs must be portable to Windows.
- [Superscript matching can be missed in implementation] → Document the exact
  `¹`, `²`, and `³` forms and include fixture cases for them.
