## ADDED Requirements

### Requirement: Release jobs acquire approved publisher sources through one authority

Each release job requiring frozen transport verification SHALL obtain the separately approved publisher workflow and action commits through one repository-owned acquisition implementation using the existing reviewed pin authority. The implementation SHALL use the current repository's `origin` and exact commits with no tags and shallow fetching, and SHALL reject or fail on acquisition errors. Caller-supplied inventory, refs, mutable tags or workflow inputs SHALL NOT select the approved identities. Each job SHALL independently retain its candidate-bound frozen-transport verification after acquisition; acquisition alone SHALL NOT establish authorization or byte identity.

#### Scenario: Reviewed pins change at their authority
- **WHEN** the independently reviewed workflow and action commits change at the existing authority
- **THEN** every affected release job acquires that pair without per-job pin edits
- **AND** each job still verifies the exact approved source blobs and frozen candidate

#### Scenario: Source acquisition fails
- **WHEN** Git cannot acquire either approved commit
- **THEN** the job fails before its transport verification or publication

#### Scenario: Caller data names another source
- **WHEN** candidate inventory or other caller data contains unapproved publisher identities
- **THEN** it cannot alter the acquisition authority and existing verification rejects mismatched identities or bytes
