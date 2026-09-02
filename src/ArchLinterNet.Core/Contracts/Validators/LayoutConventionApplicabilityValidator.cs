using ArchLinterNet.Core.Contracts.Families;
using ArchLinterNet.Core.Contracts.PolicyImports;

namespace ArchLinterNet.Core.Contracts.Validators;

/// <summary>Validates the reviewed, bounded folder inventory before it reaches evaluation.</summary>
internal sealed class LayoutConventionApplicabilityValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        ValidateGroup(document, document.Contracts.StrictLayoutConventionApplicability,
            document.Contracts.StrictLayoutConventions, "strict_layout_convention_applicability");
        ValidateGroup(document, document.Contracts.AuditLayoutConventionApplicability,
            document.Contracts.AuditLayoutConventions, "audit_layout_convention_applicability");
    }

    private static void ValidateGroup(
        ArchitectureContractDocument document,
        IReadOnlyList<ArchitectureLayoutConventionApplicabilityContract> inventories,
        IReadOnlyList<ArchitectureLayoutConventionContract> conventions,
        string groupName)
    {
        HashSet<string> conventionIds = conventions
            .Where(convention => !string.IsNullOrWhiteSpace(convention.Id))
            .Select(convention => convention.Id!)
            .ToHashSet(StringComparer.Ordinal);

        for (int inventoryIndex = 0; inventoryIndex < inventories.Count; inventoryIndex++)
        {
            ArchitectureLayoutConventionApplicabilityContract inventory = inventories[inventoryIndex];
            string path = ArchitecturePolicyProvenancePath.AppendIndex(
                ArchitecturePolicyProvenancePath.Property($"contracts.{groupName}"), inventoryIndex);
            document.Provenance.SetValidationSubject(path);
            ValidateInventory(document, inventory, conventionIds);
        }
    }

    private static void ValidateInventory(
        ArchitectureContractDocument document,
        ArchitectureLayoutConventionApplicabilityContract inventory,
        IReadOnlySet<string> conventionIds)
    {
        if (string.IsNullOrWhiteSpace(inventory.Name))
        {
            throw new InvalidOperationException("Layout convention applicability inventory must declare a non-empty name.");
        }

        string scope = NormalizePath(inventory.Scope, "scope", inventory.Name, allowCurrentDirectory: true);
        if (!IsUnderConfiguredSourceRoot(scope, document.Analysis.SourceRoots))
        {
            throw new InvalidOperationException(
                $"Layout convention applicability inventory '{inventory.Name}' scope '{inventory.Scope}' must be under a configured analysis.source_roots entry.");
        }

        if (inventory.ExpectedFolders is null || inventory.ExpectedFolders.Count == 0)
        {
            throw new InvalidOperationException(
                $"Layout convention applicability inventory '{inventory.Name}' must declare at least one expected_folders entry.");
        }

        var folderIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ArchitectureLayoutConventionExpectedFolder folder in inventory.ExpectedFolders)
        {
            if (string.IsNullOrWhiteSpace(folder.Id) || !folderIds.Add(folder.Id))
            {
                throw new InvalidOperationException(
                    $"Layout convention applicability inventory '{inventory.Name}' has a missing or duplicate expected_folders id.");
            }

            _ = NormalizePath(folder.Path, "expected_folders.path", inventory.Name, allowCurrentDirectory: true);
            if (string.IsNullOrWhiteSpace(folder.ConventionId) || !conventionIds.Contains(folder.ConventionId))
            {
                throw new InvalidOperationException(
                    $"Layout convention applicability inventory '{inventory.Name}' expected folder '{folder.Id}' references unknown same-mode layout convention id '{folder.ConventionId}'.");
            }
        }
    }

    internal static string NormalizePath(string value, string field, string inventoryName, bool allowCurrentDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Layout convention applicability inventory '{inventoryName}' requires a non-empty {field} path.");
        }

        string normalized = value.Replace('\\', '/').Trim().TrimEnd('/');
        if (normalized.Length == 0)
        {
            normalized = ".";
        }

        if ((!allowCurrentDirectory && normalized == ".")
            || value.Contains('\\')
            || normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.Contains("//", StringComparison.Ordinal)
            || (normalized != "." && normalized.Split('/').Any(segment => segment is "" or "." or "..")))
        {
            throw new InvalidOperationException(
                $"Layout convention applicability inventory '{inventoryName}' {field} path '{value}' must be a normalized repository-relative path without '.' or '..' segments.");
        }

        return normalized;
    }

    private static bool IsUnderConfiguredSourceRoot(string scope, IReadOnlyList<string> sourceRoots)
    {
        foreach (string root in sourceRoots)
        {
            string normalizedRoot = root.Replace('\\', '/').Trim().TrimEnd('/');
            if (normalizedRoot.Length == 0)
            {
                normalizedRoot = ".";
            }

            if (scope == normalizedRoot || normalizedRoot == "." || scope.StartsWith(normalizedRoot + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
