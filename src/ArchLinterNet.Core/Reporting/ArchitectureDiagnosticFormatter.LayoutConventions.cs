using ArchLinterNet.Core.Model;

namespace ArchLinterNet.Core.Reporting;

// LayoutConventionDiagnostic-specific formatting, split out of ArchitectureDiagnosticFormatter.cs
// to keep both files under the repository's file-size lint budget (make/lint.mk
// CS_SIZE_LINT_ERROR_LINES). See ArchitecturePolicyDocumentLoader.WhenFields.cs for the same idiom.
public sealed partial class ArchitectureDiagnosticFormatter
{
    private static string FormatLayoutConventionContextForHumans(LayoutConventionDiagnostic layoutConvention)
    {
        List<string> parts = new();
        if (layoutConvention.DataUnavailable)
        {
            parts.Add("path-based layout checks unavailable");
        }

        if (layoutConvention.MatchedFilePath != null)
        {
            parts.Add($"file: {layoutConvention.MatchedFilePath}");
        }

        if (layoutConvention.ExpectedTypeKind != null)
        {
            parts.Add($"expected_kind: {layoutConvention.ExpectedTypeKind}, actual_kind: {layoutConvention.ActualTypeKind}");
        }

        if (layoutConvention.ExpectedTypeName != null)
        {
            parts.Add($"expected_name: {layoutConvention.ExpectedTypeName}, actual_name: {layoutConvention.ActualTypeName}");
        }

        if (layoutConvention.ExpectedCounterpartName != null)
        {
            parts.Add($"expected_counterpart: {layoutConvention.ExpectedCounterpartName}");
        }

        if (layoutConvention.WhenExpression != null)
        {
            string whenSuffix = FormatWhenExpressionForHumans(layoutConvention.WhenExpression);
            parts.Add(whenSuffix.TrimStart(',', ' '));
        }

        return parts.Count == 0 ? string.Empty : $" ({string.Join("; ", parts)})";
    }

    private static void ApplyLayoutConventionCiFields(LayoutConventionDiagnostic layoutConvention, Dictionary<string, object?> obj)
    {
        if (layoutConvention.MatchedFilePath != null)
            obj["matched_file_path"] = layoutConvention.MatchedFilePath;

        if (layoutConvention.ExpectedTypeKind != null)
            obj["expected_type_kind"] = layoutConvention.ExpectedTypeKind;

        if (layoutConvention.ActualTypeKind != null)
            obj["actual_type_kind"] = layoutConvention.ActualTypeKind;

        if (layoutConvention.ExpectedTypeName != null)
            obj["expected_type_name"] = layoutConvention.ExpectedTypeName;

        if (layoutConvention.ActualTypeName != null)
            obj["actual_type_name"] = layoutConvention.ActualTypeName;

        if (layoutConvention.ExpectedCounterpartName != null)
            obj["expected_counterpart_name"] = layoutConvention.ExpectedCounterpartName;

        if (layoutConvention.DataUnavailable)
            obj["data_unavailable"] = layoutConvention.DataUnavailable;

        ApplyWhenExpressionCiFields(layoutConvention.WhenExpression, obj);
    }
}
