## MODIFIED Requirements

### Requirement: Packaged machine-readable output schemas
The immutable packaged schema registry SHALL publish the implemented `finding/v1`, `analysis-cache/v1`, and `analysis-profile/v1` JSON schemas only after output produced through their public writer or command paths validates against the exact packaged resource. Each descriptor's read/write support SHALL describe an implemented public contract: finding and cache readers SHALL reject or explicitly report unsupported future versions rather than interpreting them as v1; `analysis-profile` SHALL report write-only support until a public reader exists.

#### Scenario: Packaged schemas validate generated output
- **WHEN** a finding, persisted cache entry, or profile document is generated through its implemented public path
- **THEN** it validates against the matching exact packaged schema bytes

#### Scenario: Profile reader support is absent
- **WHEN** an installed consumer lists the `analysis-profile` descriptor
- **THEN** it reports write support and does not report read support

#### Scenario: Installed package validates output offline
- **WHEN** a freshly packed CLI/Core package is installed from a local feed in an offline directory
- **THEN** schema discovery and printed-resource byte equivalence for finding, cache, and profile formats succeed without a repository checkout or network access
