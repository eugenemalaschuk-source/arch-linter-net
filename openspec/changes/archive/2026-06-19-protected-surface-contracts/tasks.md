## 1. Model and Schema

- [x] 1.1 Add `ArchitectureProtectedContract` model class to `ArchitectureContractModels.cs`
- [x] 1.2 Add `StrictProtected` / `AuditProtected` lists to `ArchitectureContractGroups`
- [x] 1.3 Update `EnumerateStrict` / `EnumerateAudit` to include protected contracts
- [x] 1.4 Add protected contract group to `ValidateDuplicateIds` in `ArchitectureContractLoader.cs`
- [x] 1.5 Add `protectedContract` JSON Schema definition in `dependencies.arch.schema.json`
- [x] 1.6 Add `strict_protected` / `audit_protected` entries to schema contracts properties

## 2. Core Enforcement

- [x] 2.1 Add `CheckProtectedContract` method to `ArchitectureContractRunner`
- [x] 2.2 Add strict/audit accessor methods to `ArchitectureContractRunner`
- [x] 2.3 Add protected contract check loop to `ArchitectureValidator.Validate()`

## 3. Violation Model and Reporting

- [x] 3.1 Add optional `SourceLayer`, `TargetLayer`, `AllowedImporters` properties to `ArchitectureViolation` record
- [x] 3.2 Update `ArchitectureDiagnosticFormatter` human output for protected violations
- [x] 3.3 Update `ArchitectureDiagnosticFormatter` JSON output for protected violations

## 4. CLI and Testing Adapter

- [x] 4.1 Add protected contract check loop to `Program.cs`
- [x] 4.2 Add protected contract check loop to `ArchitectureAssertions.cs`
- [x] 4.3 Update `CollectAvailableContractIds` in `Program.cs` (auto-included via `AllStrict`/`AllAudit`)

## 5. Self-Architecture and Documentation

- [x] 5.1 Update `architecture/dependencies.arch.yml` to protect ArchLinterNet.Core internals
- [x] 5.2 Update AI capabilities manifest if present
- [x] 5.3 Update YAML reference docs

## 6. Tests

- [x] 6.1 Add `ProtectedContractTests` with passing scenario (allowed importer references protected layer)
- [x] 6.2 Add failing scenario (non-allowed layer references protected layer)
- [x] 6.3 Add self-reference scenario (within protected layer, no violation)
- [x] 6.4 Add `allowed_types` override scenario
- [x] 6.5 Add `ignored_violations` scenario
- [x] 6.6 Add multiple protected layers scenario
- [x] 6.7 Add unknown layer name error scenario
- [x] 6.8 Add JSON output enrichment scenario
- [x] 6.9 Verify backward compatibility: existing tests pass without changes
