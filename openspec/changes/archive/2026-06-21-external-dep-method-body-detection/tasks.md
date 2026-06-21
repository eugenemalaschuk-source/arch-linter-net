## 1. IL Scanner Extension for External Dependencies

- [x] 1.1 Create `ArchitectureExternalDependencyIlScanner` class in `src/ArchLinterNet.Core/Scanning/` that reuses IL reading logic from `ArchitectureIlMethodBodyScanner` (opcode decoding, metadata token resolution) but applies `ArchitectureExternalDependencyResolver.MatchesGroup` instead of `ArchitectureForbiddenCallMatcher`
- [x] 1.2 The scanner shall enumerate methods and constructors on each source type, read IL bytearrays, resolve metadata tokens to `MemberInfo`, extract the declaring type's full name and namespace, and match against the external dependency group's namespace/type prefixes
- [x] 1.3 Return violations with source type, source member (`TypeName.MethodName`), forbidden external group name, and matched external references (deduplicated and sorted)

## 2. Wire Method Body Scanning into External Contract Runner

- [x] 2.1 In `ArchitectureContractRunner.Checking.cs`, extend `CheckExternalContract` to call the new IL scanner after the existing type-level scan
- [x] 2.2 Pass the same source types, external dependency group, ignored violations, usage tracker, baseline candidates, and contract group to the new scanner
- [x] 2.3 Merge method body violations with type-level violations in the same return list
- [x] 2.4 Ensure baseline candidate entries for method-body violations use the same contract group format

## 3. Test Fixtures

- [x] 3.1 Add test fixture types in `tests/ArchLinterNet.Core.Tests/ExternalDependencyContractTestFixtures.cs`: a core type with a method calling a vendor SDK method, a core type with a constructor calling a vendor SDK constructor, a core type with a property accessing a vendor SDK type, a core type with a generic reference to a vendor SDK type, and an adapter layer type that is allowed to use vendor SDK
- [x] 3.2 Add a Unity-style fixture: a core type with `UnityEngine.Debug.Log(...)` in a method body

## 4. Unit Tests

- [x] 4.1 Add test: method call to forbidden external group inside method body is detected as a violation
- [x] 4.2 Add test: constructor call to forbidden external group inside method body is detected as a violation
- [x] 4.3 Add test: property accessor to forbidden external group inside method body is detected as a violation
- [x] 4.4 Add test: generic type reference to forbidden external group inside method body is detected as a violation
- [x] 4.5 Add test: allowed adapter layer type using vendor SDK does not produce a violation
- [x] 4.6 Add test: strict external contract with method-body violation fails validation
- [x] 4.7 Add test: audit external contract with method-body violation reports without failing strict validation
- [x] 4.8 Add test: existing type-level external dependency detection still works (backward compatibility)
- [x] 4.9 Add test: third-party package internals are not scanned

## 5. Integration and Acceptance

- [x] 5.1 Run `rtk make acceptance` to verify all existing tests pass
- [x] 5.2 Run `rtk make lint` to verify code quality
- [x] 5.3 Validate at least one Unity-style rule (core code forbidden from using `UnityEditor` namespace via method body)
- [x] 5.4 Validate at least one server-style rule (domain code forbidden from using infrastructure SDK namespaces via method body)

## 6. Documentation

- [x] 6.1 Update `openspec/specs/external-dependency-contracts/spec.md` with the delta spec from this change
- [x] 6.2 Update policy authoring docs to describe method-body detection as static reference analysis (not semantic data-flow)
- [x] 6.3 Update AI-facing guidance if the policy surface description changes
