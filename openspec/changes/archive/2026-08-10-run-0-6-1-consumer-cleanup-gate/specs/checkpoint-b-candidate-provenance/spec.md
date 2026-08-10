## ADDED Requirements

### Requirement: The candidate agrees with itself about its release identity
The packed-artifact gate SHALL verify, from the installed candidate rather than the source tree,
that the candidate identifies one release everywhere a consumer can observe it: the installed
CLI's reported version, the packaged README's public adoption package line, the packaged
compatibility manifest's product version, and the schema identities the installed `schema list`
advertises. A mismatch SHALL fail the gate before publication.

#### Scenario: Packaged README names a stale package line
- **WHEN** the packaged README still identifies a previous release as the public adoption package
  line
- **THEN** the release-identity scenario fails and the candidate is not authorized

#### Scenario: Registry product version differs from the candidate
- **WHEN** the packaged compatibility manifest's product version differs from the candidate's
  public adoption package line
- **THEN** the release-identity scenario fails and the candidate is not authorized
