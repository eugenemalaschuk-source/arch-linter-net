## ADDED Requirements

### Requirement: Reference runtime preserves transport-only authority boundaries
The reference `badge-relay/v1` runtime SHALL implement private registry,
OIDC-authorized publication, and atomic per-alias state without importing the
Core or CLI evaluator, accessing source repositories, or accepting a GitHub
App, PAT, arbitrary artifact URL, or callback URL. It SHALL retain only the
minimum private publication envelope and hashes required for replay protection;
raw JWTs, source identity, source names, SHAs, PR/run data, and detailed errors
SHALL not appear in public responses or public storage routes.

#### Scenario: Two registered repositories remain isolated
- **WHEN** two synthetic immutable repository identities are registered to
  distinct aliases
- **THEN** either publisher is unable to prepare, renew, invalidate, or revoke
  the other alias
- **AND** no public route reveals the other identity or receipt
