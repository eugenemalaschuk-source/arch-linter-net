## ADDED Requirements

### Requirement: Publisher protects report integrity across reruns, races, and text decoding

The privileged publisher SHALL distinguish a latest-attempt producer that is missing from a
failed, cancelled, ambiguous, or invalid producer. When the producer is missing only because a
partial rerun did not rerun it, the publisher SHALL preserve exactly one existing bot-authored
unified comment whose context marker binds it to the current PR head. All other unavailable
producer states SHALL remain fail closed.

The publisher SHALL re-read the pull-request head immediately before writing a sticky comment and
SHALL reject a mismatched artifact without writing it. It SHALL re-read the head after a comment
write; when that read observes a newer head, it SHALL replace only the comment it just wrote with a
fixed unavailable state bound to the observed head.

Before a report is ready for publication, the publisher SHALL strictly decode the hash-validated
report bytes as UTF-8. It SHALL reject malformed bytes and SHALL publish the one validated decoded
string without re-reading or leniently decoding the report file.

#### Scenario: Partial rerun preserves verified same-head evidence

- **WHEN** run attempt 2 contains no producer job because only failed jobs were rerun
- **AND** exactly one existing unified comment is bound to the current PR head
- **THEN** the publisher preserves that comment and reports the partial-rerun state without
  replacing it with unavailable metadata

#### Scenario: A push before comment mutation rejects the old report

- **WHEN** the report passed its initial current-head binding but the pre-write PR-head read sees a
  newer commit
- **THEN** the publisher performs no comment write for the old report
- **AND** it reports a stale-head rejection

#### Scenario: A push after comment mutation is repaired conservatively

- **WHEN** the post-write PR-head read sees a newer commit
- **THEN** the publisher replaces the comment it just wrote with fixed unavailable metadata bound
  to that newer head
- **AND** it does not leave the old report marked as current

#### Scenario: Malformed UTF-8 transport bytes are rejected

- **WHEN** a manifest-bound report has valid size and SHA-256 but contains malformed UTF-8 bytes
- **THEN** the publisher rejects it before a comment write
- **AND** it never substitutes replacement characters into the published report
