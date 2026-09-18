using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArchLinterNet.Cli.Abstractions;
using ArchLinterNet.Cli.Commands;
using static ArchLinterNet.Cli.Commands.Badge.Application.Setup.BadgeSetupOutputWriterPersistence;

namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal sealed record BadgeSetupHandoffCommandOptions(
    string? InputPath,
    string? PayloadDirectory,
    string? OutputDirectory,
    string? ExpectedBaseSha,
    string? ExpectedBaseTreeSha,
    string? ExpectedRepository,
    long? ExpectedRepositoryId,
    long? ExpectedRepositoryOwnerId,
    bool ShowHelp);

internal sealed class BadgeSetupHandoffCommandHandler(ICliConsole console)
{
    private const string Help = "arch-linter-net badge architecture-health apply-handoff --input <bootstrap-handoff.json> [--payload <directory>] --output <repository-directory> --expected-base-sha <sha> --expected-base-tree-sha <tree-sha> --repository <owner/name> --repository-id <id> --repository-owner-id <id>";

    internal int Execute(BadgeSetupHandoffCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.ShowHelp)
        {
            console.Out.WriteLine(Help);
            return CliExitCodes.Success;
        }

        try
        {
            BadgeSetupHandoff.Apply(options);
            console.Out.WriteLine("Verified bootstrap handoff applied. Review the generated diff in a normal pull request.");
            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException)
        {
            console.Error.WriteLine($"Badge setup handoff failed: {exception.Message}");
            return CliExitCodes.InvalidArgumentsOrRuntimeError;
        }
    }
}

internal static class BadgeSetupHandoff
{
    private const string Schema = "architecture-health-badge-bootstrap-handoff/v1";
    private static readonly UTF8Encoding _utf8 = new(false, true);
    private static readonly HashSet<string> _fixedManagedPaths = new(StringComparer.Ordinal)
    {
        "badge-relay-config.json",
        "badge-relay-manifest.json",
        "README.md",
        ".github/badge-promotion/registry.json",
        "schema/0.8.0/badge-relay-config.schema.json",
        ".github/workflows/architecture-health-badge-publisher.yml",
        ".github/workflows/architecture-health-badge-renewal.yml",
        "relay/package.json",
        "relay/package-lock.json",
        "relay/tsconfig.json",
        "relay/THIRD-PARTY-NOTICES.txt",
        "relay/wrangler.jsonc",
        "relay/bundle-manifest.json",
    };

    internal static void Apply(BadgeSetupHandoffCommandOptions options)
    {
        ValidateOptions(options);
        string inputPath = Path.GetFullPath(options.InputPath!);
        if (!File.Exists(inputPath) || File.GetAttributes(inputPath).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException("The bootstrap handoff manifest must be a regular file.");
        }
        string payloadDirectory = options.PayloadDirectory is null
            ? Path.Combine(Path.GetDirectoryName(inputPath)!, "payload")
            : Path.GetFullPath(options.PayloadDirectory);
        BootstrapHandoff handoff = Parse(File.ReadAllBytes(inputPath));
        ValidateExpected(handoff, options);
        BadgeSetupHandoffGit.ValidateLocalReviewBranch(
            options.OutputDirectory!,
            handoff.Base.Sha,
            handoff.Base.TreeSha);
        IReadOnlyList<BadgeSetupOutputWriter.GeneratedFile> files = ReadPayload(payloadDirectory, handoff.Files);
        ValidatePayloadConfiguration(files, handoff);
        BadgeSetupOutputWriter.ApplyVerifiedHandoff(options.OutputDirectory!, files);
    }

    private static void ValidateOptions(BadgeSetupHandoffCommandOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputPath)
            || string.IsNullOrWhiteSpace(options.OutputDirectory)
            || string.IsNullOrWhiteSpace(options.ExpectedBaseSha)
            || string.IsNullOrWhiteSpace(options.ExpectedBaseTreeSha)
            || string.IsNullOrWhiteSpace(options.ExpectedRepository)
            || options.ExpectedRepositoryId is not > 0
            || options.ExpectedRepositoryOwnerId is not > 0)
        {
            throw new ArgumentException("Apply-handoff requires input, output, expected base commit/tree, and immutable repository identity.");
        }
    }

    private static BootstrapHandoff Parse(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        EnsureObject(root, "schema", "base", "repository", "publisher", "configuration", "files");
        if (!string.Equals(ReadString(root, "schema"), Schema, StringComparison.Ordinal))
        {
            throw new JsonException("The bootstrap handoff schema is unsupported.");
        }

        JsonElement baseElement = ReadObject(root, "base", "ref", "sha", "tree_sha");
        string baseRef = ReadString(baseElement, "ref");
        if (!BadgeSetupValidationHelpers.IsSafeRef(baseRef))
        {
            throw new JsonException("The bootstrap handoff contains an invalid base ref.");
        }

        BootstrapBase bootstrapBase = new(
            baseRef,
            ReadSha(baseElement, "sha", 40),
            ReadSha(baseElement, "tree_sha", 40));

        JsonElement repositoryElement = ReadObject(root, "repository", "owner", "name", "visibility", "repository_id", "repository_owner_id");
        BootstrapRepository repository = new(
            ReadString(repositoryElement, "owner"),
            ReadString(repositoryElement, "name"),
            ReadPositiveInteger(repositoryElement, "repository_id"),
            ReadPositiveInteger(repositoryElement, "repository_owner_id"));
        if (!BadgeSetupValidationHelpers.IsIdentity(repository.Owner)
            || !BadgeSetupValidationHelpers.IsIdentity(repository.Name))
        {
            throw new JsonException("The bootstrap handoff contains an invalid repository identity.");
        }

        JsonElement publisherElement = ReadObject(root, "publisher", "workflow_ref", "workflow_sha", "action_ref", "bundle_digest");
        BootstrapPublisher publisher = new(
            ReadString(publisherElement, "workflow_ref"),
            ReadSha(publisherElement, "workflow_sha", 40),
            ReadString(publisherElement, "action_ref"),
            ReadNullableSha(publisherElement, "bundle_digest", 64));
        ValidatePublisher(publisher);

        JsonElement configurationElement = ReadObject(root, "configuration", "schema_id", "contract_version", "mode", "disclosure_profile");
        BootstrapConfiguration configuration = ReadConfiguration(configurationElement);

        JsonElement filesElement = root.GetProperty("files");
        if (filesElement.ValueKind != JsonValueKind.Array || filesElement.GetArrayLength() == 0)
        {
            throw new JsonException("The bootstrap handoff must declare at least one managed file.");
        }

        HashSet<string> paths = new(StringComparer.Ordinal);
        List<BootstrapFile> files = [];
        foreach (JsonElement element in filesElement.EnumerateArray())
        {
            EnsureObject(element, "path", "bytes", "sha256");
            string path = ReadString(element, "path");
            if (!IsSafeManagedPath(path) || !paths.Add(path))
            {
                throw new JsonException("The bootstrap handoff contains an unsafe or duplicate managed path.");
            }

            long length = ReadNonNegativeInteger(element, "bytes");
            files.Add(new BootstrapFile(path, length, ReadSha(element, "sha256", 64)));
        }

        return new(bootstrapBase, repository, publisher, configuration, files);
    }

    private static void ValidateExpected(BootstrapHandoff handoff, BadgeSetupHandoffCommandOptions options)
    {
        string expectedRepository = options.ExpectedRepository!;
        string actualRepository = handoff.Repository.Owner + "/" + handoff.Repository.Name;
        string expectedBaseSha = ReadExpectedSha(options.ExpectedBaseSha, "base commit");
        string expectedBaseTreeSha = ReadExpectedSha(options.ExpectedBaseTreeSha, "base tree");
        if (!string.Equals(handoff.Base.Sha, expectedBaseSha, StringComparison.Ordinal)
            || !string.Equals(handoff.Base.TreeSha, expectedBaseTreeSha, StringComparison.Ordinal)
            || !string.Equals(actualRepository, expectedRepository, StringComparison.Ordinal)
            || handoff.Repository.Id != options.ExpectedRepositoryId
            || handoff.Repository.OwnerId != options.ExpectedRepositoryOwnerId)
        {
            throw new InvalidOperationException("The bootstrap handoff does not match the expected base tree and repository identity.");
        }
    }

    private static List<BadgeSetupOutputWriter.GeneratedFile> ReadPayload(
        string payloadDirectory,
        IReadOnlyList<BootstrapFile> manifestFiles)
    {
        if (!Directory.Exists(payloadDirectory))
        {
            throw new IOException("The bootstrap handoff payload directory is missing.");
        }
        if (File.GetAttributes(payloadDirectory).HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException("The bootstrap handoff payload directory cannot be a symlink, junction, or reparse point.");
        }

        HashSet<string> expected = manifestFiles.Select(static file => file.Path).ToHashSet(StringComparer.Ordinal);
        HashSet<string> actual = EnumeratePayloadFiles(payloadDirectory);
        if (!actual.SetEquals(expected))
        {
            throw new IOException("The bootstrap handoff payload does not match its closed file manifest.");
        }

        List<BadgeSetupOutputWriter.GeneratedFile> files = [];
        foreach (BootstrapFile file in manifestFiles.OrderBy(static file => file.Path, StringComparer.Ordinal))
        {
            string path = SafePath(payloadDirectory, file.Path);
            byte[] contents = File.ReadAllBytes(path);
            if (contents.LongLength != file.Bytes
                || !string.Equals(Convert.ToHexString(SHA256.HashData(contents)).ToLowerInvariant(), file.Sha256, StringComparison.Ordinal))
            {
                throw new IOException("The bootstrap handoff payload digest does not match its manifest.");
            }

            files.Add(new(file.Path, _utf8.GetString(contents)));
        }

        return files;
    }

    private static HashSet<string> EnumeratePayloadFiles(string payloadDirectory)
    {
        HashSet<string> files = new(StringComparer.Ordinal);
        Stack<string> directories = new();
        directories.Push(Path.GetFullPath(payloadDirectory));
        while (directories.TryPop(out string? directory))
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("The bootstrap handoff payload contains a symlink, junction, or reparse point.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    directories.Push(entry);
                    continue;
                }

                string relative = Path.GetRelativePath(payloadDirectory, entry).Replace('\\', '/');
                if (!IsSafeManagedPath(relative) || !files.Add(relative))
                {
                    throw new IOException("The bootstrap handoff payload contains an unsafe file path.");
                }
            }
        }

        return files;
    }

    private static void ValidatePayloadConfiguration(
        IReadOnlyList<BadgeSetupOutputWriter.GeneratedFile> files,
        BootstrapHandoff handoff)
    {
        BadgeSetupOutputWriter.GeneratedFile configurationFile = files.SingleOrDefault(
            static file => string.Equals(file.Path, "badge-relay-config.json", StringComparison.Ordinal))
            ?? throw new JsonException("The bootstrap handoff payload is missing its generated configuration.");
        BadgeSetupConfigurationParseResult parsed = BadgeSetupConfigurationParser.Parse(configurationFile.Contents);
        if (!parsed.IsValid || parsed.Configuration is not { } configuration
            || !string.Equals(configuration.Repository.Owner, handoff.Repository.Owner, StringComparison.Ordinal)
            || !string.Equals(configuration.Repository.Name, handoff.Repository.Name, StringComparison.Ordinal)
            || configuration.Repository.RepositoryId != handoff.Repository.Id
            || configuration.Repository.RepositoryOwnerId != handoff.Repository.OwnerId
            || configuration.Pins is not { } pins
            || configuration.ManagedFiles is not { } managedFiles
            || !managedFiles.Append("badge-relay-manifest.json").OrderBy(static path => path, StringComparer.Ordinal)
                .SequenceEqual(files.Select(static file => file.Path).OrderBy(static path => path, StringComparer.Ordinal), StringComparer.Ordinal)
            || !string.Equals(configuration.SchemaId, handoff.Configuration.SchemaId, StringComparison.Ordinal)
            || !string.Equals(configuration.ContractVersion, handoff.Configuration.ContractVersion, StringComparison.Ordinal)
            || !string.Equals(configuration.Mode, handoff.Configuration.Mode, StringComparison.Ordinal)
            || !string.Equals(configuration.DisclosureProfile, handoff.Configuration.DisclosureProfile, StringComparison.Ordinal)
            || !string.Equals(pins.BundleDigest, handoff.Publisher.BundleDigest, StringComparison.Ordinal))
        {
            throw new JsonException("The bootstrap handoff configuration does not match its trusted manifest binding.");
        }

        ValidatePublisher(new(pins.WorkflowRef ?? string.Empty, pins.WorkflowSha ?? string.Empty, pins.ActionRef ?? string.Empty, pins.BundleDigest));
    }

    private static void ValidatePublisher(BootstrapPublisher publisher)
    {
        if (!string.Equals(publisher.WorkflowRef, BadgeSetupContract.DefaultPublisherWorkflowRef, StringComparison.Ordinal)
            || !string.Equals(publisher.WorkflowSha, BadgeSetupContract.DefaultPublisherWorkflowSha, StringComparison.Ordinal)
            || !string.Equals(publisher.ActionRef, BadgeSetupContract.DefaultActionRef, StringComparison.Ordinal))
        {
            throw new JsonException("The bootstrap handoff publisher pins do not match the shipped trusted contract.");
        }
    }

    private static BootstrapConfiguration ReadConfiguration(JsonElement configuration)
    {
        BootstrapConfiguration result = new(
            ReadString(configuration, "schema_id"),
            ReadString(configuration, "contract_version"),
            ReadString(configuration, "mode"),
            ReadString(configuration, "disclosure_profile"));
        if (!string.Equals(result.SchemaId, BadgeSetupContract.SchemaId, StringComparison.Ordinal)
            || !string.Equals(result.ContractVersion, BadgeSetupContract.ContractVersion, StringComparison.Ordinal)
            || !BadgeSetupContract.TryParseMode(result.Mode, out _)
            || !BadgeSetupContract.TryParseProfile(result.DisclosureProfile, out _))
        {
            throw new JsonException("The bootstrap handoff configuration binding is invalid.");
        }

        return result;
    }

    private static bool IsSafeManagedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Contains('\0'))
        {
            return false;
        }

        string[] segments = path.Split('/', StringSplitOptions.None);
        if (segments.Length == 0 || !segments.All(static segment => segment.Length > 0 && segment is not "." and not ".."))
        {
            return false;
        }

        return _fixedManagedPaths.Contains(path)
            || path.StartsWith(".github/workflows/architecture-health-badge-", StringComparison.Ordinal)
                && (path.EndsWith(".yml", StringComparison.Ordinal) || path.EndsWith(".yaml", StringComparison.Ordinal))
            || path.StartsWith("relay/src/", StringComparison.Ordinal)
                && BadgeRelayBundleIntegrityValidator.SourceNames.Contains(path["relay/src/".Length..], StringComparer.Ordinal);
    }

    private static JsonElement ReadObject(JsonElement parent, string name, params string[] allowed)
    {
        JsonElement value = parent.GetProperty(name);
        EnsureObject(value, allowed);
        return value;
    }

    private static void EnsureObject(JsonElement element, params string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The bootstrap handoff contains a non-object value where an object is required.");
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new JsonException("The bootstrap handoff contains duplicate or unknown object properties.");
            }

            EnsureNoDuplicateProperties(property.Value);
        }

        if (allowed.Any(name => !seen.Contains(name)))
        {
            throw new JsonException("The bootstrap handoff is missing a required property.");
        }
    }

    private static void EnsureNoDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                {
                    throw new JsonException("The bootstrap handoff contains duplicate JSON properties.");
                }

                EnsureNoDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                EnsureNoDuplicateProperties(child);
            }
        }
    }

    private static string ReadString(JsonElement element, string name)
    {
        JsonElement value = element.GetProperty(name);
        return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new JsonException("The bootstrap handoff contains an invalid string property.");
    }

    private static string ReadSha(JsonElement element, string name, int length)
    {
        string value = ReadString(element, name);
        return value.Length == length && value.All(static character => char.IsAsciiHexDigit(character))
            ? value.ToLowerInvariant()
            : throw new JsonException("The bootstrap handoff contains an invalid digest.");
    }

    private static string? ReadNullableSha(JsonElement element, string name, int length)
    {
        JsonElement value = element.GetProperty(name);
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return ReadSha(element, name, length);
    }

    private static string ReadExpectedSha(string? value, string name)
    {
        if (value is null || value.Length != 40 || !value.All(static character => char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException($"The expected {name} must be a 40-character hexadecimal SHA.");
        }

        return value.ToLowerInvariant();
    }

    private static long ReadPositiveInteger(JsonElement element, string name)
    {
        long value = ReadNonNegativeInteger(element, name);
        return value > 0 ? value : throw new JsonException("The bootstrap handoff contains a non-positive immutable identifier.");
    }

    private static long ReadNonNegativeInteger(JsonElement element, string name) => element.GetProperty(name).TryGetInt64(out long value) && value >= 0
        ? value
        : throw new JsonException("The bootstrap handoff contains an invalid numeric property.");

    private sealed record BootstrapHandoff(
        BootstrapBase Base,
        BootstrapRepository Repository,
        BootstrapPublisher Publisher,
        BootstrapConfiguration Configuration,
        IReadOnlyList<BootstrapFile> Files);

    private sealed record BootstrapBase(string Ref, string Sha, string TreeSha);

    private sealed record BootstrapRepository(string Owner, string Name, long Id, long OwnerId);

    private sealed record BootstrapPublisher(string WorkflowRef, string WorkflowSha, string ActionRef, string? BundleDigest);

    private sealed record BootstrapConfiguration(string SchemaId, string ContractVersion, string Mode, string DisclosureProfile);

    private sealed record BootstrapFile(string Path, long Bytes, string Sha256);
}
