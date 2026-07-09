using ArchLinterNet.Core.Contracts;

namespace ArchLinterNet.Core.Contracts.Validators;

internal sealed class DuplicateIdValidator : IArchitecturePolicyDocumentValidator
{
    public void Validate(ArchitectureContractDocument document)
    {
        var groups = ArchitectureContractFamilyBindings.All
            .SelectMany(binding => new[] { binding.Strict(document.Contracts), binding.Audit(document.Contracts) });

        foreach (var group in groups)
        {
            var duplicates = group
                .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Duplicate contract IDs found: {string.Join(", ", duplicates)}. Each contract ID must be unique within its contract type and mode group.");
            }
        }
    }
}
