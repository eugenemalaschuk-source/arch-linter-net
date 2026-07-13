## MODIFIED Requirements

### Requirement: Imports use an explicit ordered field
The root and fragments SHALL support a top-level `imports` sequence containing
non-empty explicit portable relative file paths. Each path SHALL resolve
relative to the document that declares it. An import string SHALL be Unicode
NFC and contain one or more non-empty `/`-separated segments. A segment SHALL
be `.` or `..`, or a portable file-name segment that contains no control
character or `<`, `>`, `:`, `"`, `/`, `\\`, `|`, `?`, or `*`; does not end in dot
or space; and whose case-insensitive basename before its first `.` is not a
Windows reserved device name: `CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`,
`LPT1`–`LPT9`, `COM¹`–`COM³`, or `LPT¹`–`LPT³`. Backslashes, leading slashes,
empty segments, drive/URI colons, non-NFC strings, and interpolation tokens
(`${`, `$(`, `%...%`, or a leading `~`) SHALL be rejected. The grammar SHALL be
validated before any host filesystem resolution. Absolute paths, UNC/device
paths, URI-like values, globs, environment interpolation, and non-scalar
entries SHALL be rejected.

#### Scenario: Root combines inline content with imports
- **WHEN** a root defines ordinary `layers`, `analysis`, or `contracts` content
  and an ordered `imports` sequence
- **THEN** the root inline content and imported fragment content participate in
  one composed policy

#### Scenario: Portable slash-separated path
- **WHEN** an import entry is `policy/contracts/domain.yml`
- **THEN** every supported host treats it as the same sequence of relative
  segments before canonical filesystem resolution

#### Scenario: Platform-native or interpolation form
- **WHEN** an import uses `\\`, `/etc/policy.yml`, `C:\\policy.yml`,
  `\\\\server\\share\\policy.yml`, `\\\\?\\C:\\policy.yml`, a URI, a glob, or
  an interpolation token
- **THEN** policy loading fails before the target is read with a portable-path
  grammar diagnostic

#### Scenario: Reserved basename has an extension
- **WHEN** an import segment is `NUL.yml`, `COM1.arch.yml`, `LPT¹.yaml`, or
  `NUL.tar.gz`
- **THEN** policy loading rejects the segment before filesystem resolution on
  every supported host
