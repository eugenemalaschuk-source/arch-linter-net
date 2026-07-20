using ArchLinterNet.Core.Asmdef;
using ArchLinterNet.Core.Contracts;
using ArchLinterNet.Core.Model;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public class AsmdefValidatorTests
{
    [Test]
    public void Validate_NonExistentFile_ThrowsTypedRootPolicyDiagnostic()
    {
        ArchitecturePolicyImportException exception = Assert.Throws<ArchitecturePolicyImportException>(() =>
            AsmdefValidator.Validate(contractPath: "nonexistent.yml"))!;

        Assert.That(exception.Diagnostic!.Location!.Role, Is.EqualTo(ArchitecturePolicyDocumentRole.Root));
    }
}
