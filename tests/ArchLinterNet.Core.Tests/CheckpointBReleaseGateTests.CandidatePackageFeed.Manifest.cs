using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace ArchLinterNet.Core.Tests;

public sealed partial class CheckpointBReleaseGateTests
{
    internal sealed partial class CandidatePackageFeed
    {
        private static (IReadOnlyList<PackagePairEvidence> Packages, string ManifestSha256) LoadManifest(
            string feed, string candidateVersion, bool createdLocally)
        {
            string? manifestPath = Environment.GetEnvironmentVariable("CHECKPOINT_B_PACKAGE_MANIFEST");
            if (!createdLocally && string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new InvalidOperationException("Checkpoint B requires CHECKPOINT_B_PACKAGE_MANIFEST for supplied packages.");
            }

            if (!string.IsNullOrWhiteSpace(manifestPath))
            {
                string path = Path.GetFullPath(manifestPath);
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement root = document.RootElement;
                string schema = root.GetProperty("schema").GetString()!;
                Assert.That(schema, Is.AnyOf("checkpoint-b-candidate-manifest/v2", "checkpoint-b-candidate-manifest/v3"));
                Assert.That(root.GetProperty("version").GetString(), Is.EqualTo(candidateVersion));
                string[] expectedIds = ExpectedPackageIds(candidateVersion);
                Assert.That(schema == "checkpoint-b-candidate-manifest/v3",
                    Is.EqualTo(expectedIds.Contains(AnnotationsPackageId, StringComparer.Ordinal)),
                    "The candidate manifest schema must agree with its reviewed package inventory.");
                JsonElement[] packageRecords = root.GetProperty("packages").EnumerateArray().ToArray();
                Assert.That(packageRecords, Has.Length.EqualTo(expectedIds.Length));
                PackagePairEvidence[] packages = packageRecords.Select((package, index) =>
                {
                    string packageId = expectedIds[index];
                    Assert.That(package.GetProperty("id").GetString(), Is.EqualTo(packageId));
                    Assert.That(package.GetProperty("version").GetString(), Is.EqualTo(candidateVersion));
                    PackageSubjectEvidence packageSubject = ReadSubject(package.GetProperty("package"));
                    AssertSubjectIdentity(packageSubject, packageId, candidateVersion, "package");
                    bool requiresSymbols = !string.Equals(packageId, AnnotationsPackageId, StringComparison.Ordinal);
                    bool hasSymbols = package.TryGetProperty("symbols", out JsonElement symbolsElement);
                    Assert.That(hasSymbols, Is.EqualTo(requiresSymbols), packageId);
                    PackageSubjectEvidence? symbols = hasSymbols ? ReadSubject(symbolsElement) : null;
                    if (symbols is not null)
                    {
                        AssertSubjectIdentity(symbols, packageId, candidateVersion, "symbols");
                    }

                    return new PackagePairEvidence(packageId, candidateVersion, packageSubject, symbols);
                }).ToArray();
                foreach (PackagePairEvidence package in packages)
                {
                    IEnumerable<PackageSubjectEvidence> subjects = package.Symbols is PackageSubjectEvidence symbols
                        ? [package.Package, symbols]
                        : [package.Package];
                    foreach (PackageSubjectEvidence subject in subjects)
                    {
                        string packagePath = Path.Combine(feed, subject.File);
                        Assert.That(File.Exists(packagePath), Is.True, packagePath);
                        Assert.Multiple(() =>
                        {
                            Assert.That(new FileInfo(packagePath).Length, Is.EqualTo(subject.Size), subject.File);
                            Assert.That(Sha256(packagePath), Is.EqualTo(subject.Sha256), subject.File);
                        });
                    }
                }

                return (packages, Sha256(path));
            }

            PackagePairEvidence[] localPackages = ExpectedPackageIds(candidateVersion).Select(packageId =>
            {
                string packagePath = Path.Combine(feed, $"{packageId}.{NormalizedPackageVersion(candidateVersion)}.nupkg");
                string symbolsPath = Path.Combine(feed, $"{packageId}.{NormalizedPackageVersion(candidateVersion)}.snupkg");
                bool hasSymbols = !string.Equals(packageId, AnnotationsPackageId, StringComparison.Ordinal);
                Assert.That(File.Exists(packagePath), Is.True, packagePath);
                Assert.That(File.Exists(symbolsPath), Is.EqualTo(hasSymbols), symbolsPath);
                return new PackagePairEvidence(
                    packageId,
                    candidateVersion,
                    CreateSubject("package", packagePath),
                    hasSymbols ? CreateSubject("symbols", symbolsPath) : null);
            }).ToArray();
            string localManifest = JsonSerializer.Serialize(localPackages, _evidenceSerializerOptions);
            return (localPackages, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(localManifest))));
        }

        private static PackageSubjectEvidence ReadSubject(JsonElement subject) => new(
            subject.GetProperty("kind").GetString()!,
            subject.GetProperty("file").GetString()!,
            subject.GetProperty("size").GetInt64(),
            subject.GetProperty("sha256").GetString()!);

        private static PackageSubjectEvidence CreateSubject(string kind, string path) => new(
            kind,
            Path.GetFileName(path),
            new FileInfo(path).Length,
            Sha256(path));
    }
}
