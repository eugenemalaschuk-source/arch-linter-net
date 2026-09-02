## MODIFIED Requirements

### Requirement: Markdown is safe and concise for canonical evidence
The CLI SHALL escape every repository-controlled or artifact-controlled value
according to its Markdown context. Inline-code values SHALL not close or extend
their code span, and plain-text values SHALL not create links, GitHub
autolinks, mentions, comments, HTML, headings, or control-character-driven
structure. A complete clean report SHALL omit empty blocker and non-blocking-
debt drill-down sections, and empty bounded sections SHALL not emit `Showing 0
of 0` detail text.

#### Scenario: Hostile evidence cannot alter report structure
- **WHEN** a policy, finding, remediation, or change-artifact value contains
  backticks, HTML-comment delimiters, Markdown-link syntax, or control
  characters
- **THEN** the rendered output displays it as inert text and retains the
  report's intended section structure

#### Scenario: Artifact text cannot create GitHub references
- **WHEN** a plain-text artifact value contains an email address, an `@user`
  or `@org/team` mention, an issue reference, or an `owner/repository#number`
  reference
- **THEN** the Markdown displays the value as inert text without creating a
  GitHub autolink or notification-producing mention

#### Scenario: Clean complete report has no empty debt drill-downs
- **WHEN** a report is complete, passes, and has no blockers or non-blocking
  debt
- **THEN** the Markdown contains neither blocker nor debt drill-down headings
  and no empty bounded-detail indicator

#### Scenario: Baseline lifecycle remains distinct from accepted debt
- **WHEN** a debt-gate receipt contains matched and non-matched baseline
  lifecycle entries
- **THEN** only matched entries render as non-blocking existing debt, while
  non-matched lifecycle states remain blocking baseline-integrity evidence
