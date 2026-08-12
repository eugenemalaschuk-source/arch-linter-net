using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

// Public API surface diagnostics carry a normalized delta record (added/removed/changed plus the
// previous signature) that human and JSON output must describe identically — SARIF exposes the same
// record through ArchitectureSarifFormatter's properties bag. Kept in its own partial file so the
// delta vocabulary lives in one place instead of being scattered through the general formatter.
public sealed partial class ArchitectureDiagnosticFormatter
{
    private static string FormatPublicApiSurfaceContextForHumans(PublicApiSurfaceDiagnostic publicApiSurface)
    {
        string reason = publicApiSurface.ForbiddenPublicConstant == true
            ? "forbidden_public_constant"
            : publicApiSurface.UnselectedFirstPartyDependency != null
                ? "unselected_first_party_dependency"
                : ReasonForDelta(publicApiSurface.ApiDeltaKind);
        string context = $" (reason: {reason}, assembly: {publicApiSurface.ApiAssemblyName}, " +
               $"visibility: {publicApiSurface.ApiVisibility}, signature: {publicApiSurface.UndeclaredApiSignature}";

        if (publicApiSurface.ApiDeltaKind != null)
        {
            context += $", delta: {publicApiSurface.ApiDeltaKind}";
        }

        if (publicApiSurface.PreviousApiSignature != null)
        {
            context += $", previous_signature: {publicApiSurface.PreviousApiSignature}";
        }

        if (publicApiSurface.UnselectedFirstPartyDependency != null)
        {
            context += $", unselected_dependency: {publicApiSurface.UnselectedFirstPartyDependency}";
        }

        return context + ")";
    }

    private static string ReasonForDelta(string? apiDeltaKind) => apiDeltaKind switch
    {
        "removed" => "removed_api_member",
        "changed" => "changed_api_signature",
        "selector-zero-match" => "selector_matched_nothing",
        _ => "undeclared_api_member",
    };

    private static void ApplyPublicApiSurfaceCiFields(PublicApiSurfaceDiagnostic publicApiSurface, Dictionary<string, object?> obj)
    {
        if (publicApiSurface.UndeclaredApiSignature != null)
            obj["undeclared_api_signature"] = publicApiSurface.UndeclaredApiSignature;

        if (publicApiSurface.ForbiddenPublicConstant != null)
            obj["forbidden_public_constant"] = publicApiSurface.ForbiddenPublicConstant;

        if (publicApiSurface.ApiAssemblyName != null)
            obj["assembly"] = publicApiSurface.ApiAssemblyName;

        if (publicApiSurface.ApiVisibility != null)
            obj["visibility"] = publicApiSurface.ApiVisibility;

        if (publicApiSurface.ApiDeltaKind != null)
            obj["api_delta_kind"] = publicApiSurface.ApiDeltaKind;

        if (publicApiSurface.PreviousApiSignature != null)
            obj["previous_api_signature"] = publicApiSurface.PreviousApiSignature;

        if (publicApiSurface.UnselectedFirstPartyDependency != null)
            obj["unselected_first_party_dependency"] = publicApiSurface.UnselectedFirstPartyDependency;
    }
}
