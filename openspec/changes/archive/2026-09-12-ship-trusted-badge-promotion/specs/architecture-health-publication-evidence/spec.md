## ADDED Requirements

### Requirement: Promotion and renewal preserve the product-owned evidence horizon
Every ready promotion or metadata-only renewal SHALL carry and verify the canonical publication-evidence validity horizon produced by the product. A consumer workflow, adapter, publisher timestamp, unchanged tree, or unchanged payload digest SHALL not create or extend a semantic horizon.

#### Scenario: Fresh metadata can renew before the horizon
- **WHEN** the current approved producer, required gate, artifact, and product-owned validity receipt are revalidated before expiry
- **THEN** a metadata-only renewal may commit a new transport lease under the current generation
- **AND** it does not rerun the architecture analysis or alter canonical disclosure bytes

#### Scenario: Missing evidence is unavailable
- **WHEN** the product-owned receipt is missing, malformed, unsupported, inconsistent, or expired at the supplied evaluation context
- **THEN** promotion and renewal return an explicit unassessable result
- **AND** they do not publish or preserve a ready result
