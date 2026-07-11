using ArchLinterNet.Core.Asmdef;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

[TestFixture]
public class AsmdefValidatorTests
{
    [Test]
    public void Validate_NonExistentFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() =>
            AsmdefValidator.Validate("nonexistent.yml"));
    }
}
