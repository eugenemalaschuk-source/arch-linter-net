namespace ArchLinterNet.Core.Contracts;

// A normalized signature changes whenever any parameter or return type changes, so a plain set
// difference reports a re-signed member as an unrelated removal plus addition. The identity key
// deliberately drops exactly the parts that a "signature change" is allowed to alter — parameter
// types and the member type — while keeping the parts that make it the *same* member: declaration
// kind, fully qualified name (including the CLR generic arity marker), and parameter count.
//
// Parameter count rather than parameter types is what keeps an added overload an addition instead
// of masquerading as a change to an existing one.
internal static class PublicApiSignatureIdentity
{
    public const int NoParameterList = -1;

    private static readonly string[] _typeLevelKinds = { "class", "interface", "struct", "enum", "delegate", "ctor" };

    public static string Compute(string signature)
    {
        (string kind, string name, int parameterCount) = Decompose(signature);
        return $"{kind} {name}/{parameterCount}";
    }

    // Best-effort declaring type for a signature that is not backed by a live reflection entry
    // (a removed member exists only as a reviewed string). Type declarations and constructors name
    // the type itself; every other member kind appends its own name to the declaring type.
    public static string DeclaringTypeName(string signature)
    {
        (string kind, string name, _) = Decompose(signature);
        if (name.Length == 0)
        {
            return signature;
        }

        if (_typeLevelKinds.Contains(kind, StringComparer.Ordinal))
        {
            return name;
        }

        int lastSeparator = name.LastIndexOf('.');
        return lastSeparator <= 0 ? name : name[..lastSeparator];
    }

    private static (string Kind, string Name, int ParameterCount) Decompose(string signature)
    {
        int kindSeparator = signature.IndexOf(' ', StringComparison.Ordinal);
        if (kindSeparator < 0)
        {
            return (signature, string.Empty, NoParameterList);
        }

        string kind = signature[..kindSeparator];
        string remainder = signature[(kindSeparator + 1)..];

        int openParen = remainder.IndexOf('(', StringComparison.Ordinal);
        if (openParen < 0)
        {
            // No parameter list: a type, field, event, or non-indexer property. Everything after
            // the ": " member type is dropped so a retyped field correlates as a change.
            return (kind, StripMemberType(remainder), NoParameterList);
        }

        int closeParen = remainder.LastIndexOf(')');
        if (closeParen < openParen)
        {
            return (kind, StripMemberType(remainder), NoParameterList);
        }

        string name = remainder[..openParen];
        string parameters = remainder[(openParen + 1)..closeParen];
        return (kind, name, CountParameters(parameters));
    }

    private static string StripMemberType(string remainder)
    {
        int typeSeparator = remainder.IndexOf(": ", StringComparison.Ordinal);
        return typeSeparator < 0 ? remainder : remainder[..typeSeparator];
    }

    // Commas inside generic argument brackets (Dictionary`2[System.String,System.Int32]) and inside
    // multi-dimensional array ranks (System.Int32[,]) belong to one parameter, so only depth-0
    // commas separate parameters.
    private static int CountParameters(string parameters)
    {
        if (parameters.Trim().Length == 0)
        {
            return 0;
        }

        int count = 1;
        int depth = 0;
        foreach (char character in parameters)
        {
            switch (character)
            {
                case '[':
                    depth++;
                    break;
                case ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    count++;
                    break;
                default:
                    break;
            }
        }

        return count;
    }
}
