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
        else if (layoutConvention.ActualTypeKind != null)
        {
            parts.Add($"actual_kind: {layoutConvention.ActualTypeKind}");
        }

        if (layoutConvention.ExpectedTypeName != null)
        {
            parts.Add($"expected_name: {layoutConvention.ExpectedTypeName}, actual_name: {layoutConvention.ActualTypeName}");
        }

        if (layoutConvention.ExpectedCounterpartName != null)
        {
            parts.Add($"expected_counterpart: {layoutConvention.ExpectedCounterpartName}");
        }

        if (layoutConvention.ExpectedRoles != null)
        {
            parts.Add(
                $"expected_roles: [{string.Join(", ", layoutConvention.ExpectedRoles)}], " +
                $"actual_role: {layoutConvention.ActualRole ?? "unclassified"}");
        }

        if (layoutConvention.ExpectedAbstractClass != null)
        {
            parts.Add(
                $"expected_abstract_class: {layoutConvention.ExpectedAbstractClass}, " +
                $"actual_abstract: {layoutConvention.ActualIsAbstract}");
        }
        else if (layoutConvention.ActualIsAbstract != null)
        {
            parts.Add($"actual_abstract: {layoutConvention.ActualIsAbstract}");
        }

        if (layoutConvention.ExpectedDeclarationCount != null)
        {
            parts.Add(
                $"expected_declaration_count: <= {layoutConvention.ExpectedDeclarationCount}, " +
                $"actual_declaration_count: {layoutConvention.ActualDeclarationCount}, " +
                $"declaration_paths: {string.Join(", ", layoutConvention.DeclarationPaths ?? Array.Empty<string>())}");
        }

        if (layoutConvention.WhenExpressions is { Count: > 0 })
        {
            string whenSuffix = FormatWhenExpressionsForHumans(layoutConvention.WhenExpressions);
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

        if (layoutConvention.ExpectedRoles != null)
            obj["expected_roles"] = layoutConvention.ExpectedRoles.ToArray();

        if (layoutConvention.ActualRole != null)
            obj["actual_role"] = layoutConvention.ActualRole;

        if (layoutConvention.ExpectedAbstractClass != null)
            obj["expected_abstract_class"] = layoutConvention.ExpectedAbstractClass;

        if (layoutConvention.ActualIsAbstract != null)
            obj["actual_abstract"] = layoutConvention.ActualIsAbstract;

        if (layoutConvention.ExpectedDeclarationCount != null)
            obj["expected_declaration_count"] = layoutConvention.ExpectedDeclarationCount;

        if (layoutConvention.ActualDeclarationCount != null)
            obj["actual_declaration_count"] = layoutConvention.ActualDeclarationCount;

        if (layoutConvention.DeclarationPaths != null)
            obj["declaration_paths"] = layoutConvention.DeclarationPaths.ToArray();

        if (layoutConvention.DataUnavailable)
            obj["data_unavailable"] = layoutConvention.DataUnavailable;

        ApplyWhenExpressionsCiFields(layoutConvention.WhenExpressions, obj);
    }
}
