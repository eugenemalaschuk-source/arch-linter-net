## 1. Annotation source definitions

- [x] 1.1 Add the exact port and infrastructure role attributes from the manifest with their reviewed targets and shared metadata properties.
- [x] 1.2 Add the exact UI role attributes from the manifest with their reviewed targets and shared metadata properties.
- [x] 1.3 Add the exact Unity/client role attributes, keeping `UnityRuntime` and `UnityEditor` assembly-only and the remaining roles type-only.

## 2. Contract coverage

- [x] 2.1 Extend manifest-driven source contract checks for the ports/infrastructure, UI, and Unity/client families.
- [x] 2.2 Add representative annotations to the C# 9 consumer fixture without introducing framework references or conflicting role evidence.
- [x] 2.3 Run focused annotation source tests and the C# 9 consumer build; inspect formatting and run relevant OpenSpec validation.
