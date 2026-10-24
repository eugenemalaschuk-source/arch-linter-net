using System.IO.Compression;
using System.Text;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    internal sealed partial class CandidatePackageFeed
    {
        private static void AssertPackedAnnotationsPackage(string packagePath, string candidateVersion)
        {
            using ZipArchive package = ZipFile.OpenRead(packagePath);
            ZipArchiveEntry nuspecEntry = package.GetEntry("ArchLinterNet.Annotations.nuspec")
                ?? throw new AssertionException("The packed annotations package has no nuspec.");
            string nuspec;
            using (StreamReader reader = new(nuspecEntry.Open(), Encoding.UTF8))
            {
                nuspec = reader.ReadToEnd();
            }
            string readme = ReadEntry(package, "README.md");

            string[] forbiddenRoots = ["lib/", "ref/", "runtimes/", "native/", "analyzers/", "build/", "buildTransitive/", "tools/"];
            string[] sourceEntries = package.Entries
                .Where(entry => entry.FullName.StartsWith("contentFiles/cs/any/", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.FullName)
                .ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(nuspec, Does.Contain($"<version>{candidateVersion}</version>"));
                Assert.That(nuspec, Does.Contain("Optional source-only compile-time architecture annotations"));
                Assert.That(nuspec, Does.Not.Contain("<dependency"), "The annotations package must have no runtime dependency.");
                Assert.That(package.Entries.Any(entry => forbiddenRoots.Any(root =>
                    entry.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase))), Is.False,
                    "The annotations package must not contain a runtime, binary, build, analyzer, generator, or tool asset.");
                Assert.That(sourceEntries.Length, Is.GreaterThan(0), "The annotations package must carry compile source files.");
                Assert.That(sourceEntries.Any(path => path.EndsWith("AnnotationCatalogIdentityAssemblyAttribute.cs", StringComparison.Ordinal)), Is.True);
                Assert.That(package.Entries.Any(entry => entry.FullName.Equals("README.md", StringComparison.OrdinalIgnoreCase)), Is.True);
                Assert.That(readme, Does.Contain("source-only"));
                Assert.That(readme, Does.Contain("no runtime assembly"));
                Assert.That(readme, Does.Contain("matching ArchLinterNet 0.10.x tool"));
            });
        }

        private static string[] ExpectedPackageIds(string candidateVersion)
        {
            string coreVersion = candidateVersion.Split('+')[0];
            string[] components = coreVersion.Split('.');
            bool isStable010 = components.Length == 3
                && components[0] == "0"
                && components[1] == "10"
                && !components[2].Contains('-')
                && int.TryParse(components[2], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out _);
            return isStable010 ? [.. _packageIds, AnnotationsPackageId] : _packageIds;
        }

        private static string NormalizedPackageVersion(string version) => version.Split('+')[0];

        private static void AssertSubjectIdentity(
            PackageSubjectEvidence subject,
            string packageId,
            string version,
            string kind)
        {
            string extension = kind == "package" ? ".nupkg" : ".snupkg";
            Assert.Multiple(() =>
            {
                Assert.That(subject.Kind, Is.EqualTo(kind), packageId);
                Assert.That(subject.File, Is.EqualTo($"{packageId}.{NormalizedPackageVersion(version)}{extension}"), packageId);
            });
        }
    }
}
