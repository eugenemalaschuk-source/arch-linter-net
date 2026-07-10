using NUnit.Framework;

namespace ArchLinterNet.Unity.Tests;

[TestFixture]
public class AsmdefValidatorTests
{
    [Test]
    public void Validate_NonExistentFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<System.IO.FileNotFoundException>(() =>
            AsmdefValidator.Validate("nonexistent.yml"));
    }
}
