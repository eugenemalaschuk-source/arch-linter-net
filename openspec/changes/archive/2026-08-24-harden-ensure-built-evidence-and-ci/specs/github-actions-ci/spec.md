## ADDED Requirements

### Requirement: Windows installed-tool rebuild oracle is a required packed-artifact shard
Pull-request CI SHALL execute the installed `ArchLinterNet.Testing` `--ensure-built` replacement
oracle as a dedicated Windows packed-artifact scenario shard. The shard SHALL consume the immutable
candidate, emit its scenario evidence through the existing shard-evidence mechanism, and be required
by the stable Windows packed-artifact fan-in check.

#### Scenario: Windows PR run executes the replacement oracle
- **WHEN** the packed-artifact Windows matrix runs for a pull request
- **THEN** it invokes the dedicated Make target for the installed-tool rebuild oracle
- **AND** the Windows fan-in fails if that shard fails or its evidence is missing
