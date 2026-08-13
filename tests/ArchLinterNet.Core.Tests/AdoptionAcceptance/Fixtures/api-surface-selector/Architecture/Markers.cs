using System;

namespace Synthetic.ApiSurfaceSelector.Architecture;

// User-owned orthogonal API-membership marker. Deliberately never mapped in the fixture's
// `classification:` block: membership must not require mapping into semantic classification.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class PublicApiContractAttribute : Attribute;

// Semantic-role marker, mapped to role "ValueObject" in the fixture's `classification:` block.
// Distinct from PublicApiContractAttribute: applying this alone selects nothing in the API
// surface — API membership and semantic role are orthogonal.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ValueObjectRoleAttribute : Attribute;

// Marks the escaping-scenario type. Kept separate from PublicApiContractAttribute so the
// fail-closed escape contract never has to be part of the fixture's permanent, always-green
// contract set.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class EscapeDemoApiContractAttribute : Attribute;
