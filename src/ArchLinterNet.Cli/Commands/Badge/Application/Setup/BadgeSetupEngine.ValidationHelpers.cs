namespace ArchLinterNet.Cli.Commands.Badge.Application.Setup;

internal static class BadgeSetupValidationHelpers
{
    internal static bool IsSupportedPlan(string? plan) => plan is "free" or "pro" or "team" or "enterprise";

    internal static bool IsPositive(long? value) => value is > 0;

    internal static bool IsSafePositiveId(long? value) => value is > 0 and <= 9_007_199_254_740_991;

    internal static bool IsCloudflareAccountId(string? value) =>
        value is not null
        && value.Length == 32
        && value.All(static character => char.IsAsciiHexDigit(character));

    internal static bool IsHttpsOrigin(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps
            && !string.IsNullOrEmpty(uri.Host)
            && string.IsNullOrEmpty(uri.UserInfo)
            && (uri.AbsolutePath is "" or "/")
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);
    }

    internal static bool IsSha(string? value, int length) =>
        value is not null
        && value.Length == length
        && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static bool IsReusableWorkflowReference(string value) =>
        IsSafeRepositoryPath(value)
        && value.Split('/', StringSplitOptions.None) is [var owner, var repository, ".github", "workflows", var workflow]
        && IsIdentity(owner)
        && IsIdentity(repository)
        && IsSafeWorkflowFileName(workflow);

    internal static bool IsPinnedActionReference(string value)
    {
        int separator = value.LastIndexOf('@');
        return separator > 0
            && IsSha(value[(separator + 1)..], 40)
            && IsSafeRepositoryPath(value[..separator])
            && value[..separator].Contains('/');
    }

    internal static bool IsSafeRepositoryPath(string? value) =>
        value is { Length: > 0 and <= 256 } path
        && IsSafePath(path)
        && !path.StartsWith('/')
        && !path.Contains("..", StringComparison.Ordinal)
        && !path.Contains("//", StringComparison.Ordinal)
        && !path.EndsWith('/')
        && path.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' or '/');

    internal static bool IsSafePath(string? value) => !string.IsNullOrWhiteSpace(value);

    internal static bool IsSafeRef(string? value) =>
        value is { Length: > 0 and <= 128 } reference
        && IsSafeRepositoryPath(reference)
        && char.IsAsciiLetterOrDigit(reference[0])
        && !reference.Contains("/.", StringComparison.Ordinal)
        && !reference.EndsWith('.');

    internal static bool IsWorkflowPath(string? value) =>
        value is not null
        && IsSafeRepositoryPath(value)
        && value.Split('/', StringSplitOptions.None) is [".github", "workflows", var workflow]
        && IsSafeWorkflowFileName(workflow);

    internal static bool IsSafeWorkflowFileName(string value) =>
        (value.EndsWith(".yml", StringComparison.Ordinal) || value.EndsWith(".yaml", StringComparison.Ordinal))
        && value[..value.LastIndexOf('.')].Length > 0
        && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');

    internal static bool IsSafeName(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is ' ' or '_' or '-' or '.');

    internal static bool IsSafeCheckApp(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 2 and <= 64
        && (value[0] is >= 'a' and <= 'z' || value[0] is >= '0' and <= '9')
        && value.All(static character => character is >= 'a' and <= 'z' || character is >= '0' and <= '9' || character == '-');

    internal static bool IsSafeAudience(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 256
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' or ':' or '/');

    internal static bool IsIdentity(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 100
        && value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');

    internal static bool IsOpaqueAlias(string? value) =>
        value is not null
        && value.Length == 8
        && value[0] == 'a'
        && value[1..].All(static character => char.IsLower(character) || char.IsDigit(character));
}
