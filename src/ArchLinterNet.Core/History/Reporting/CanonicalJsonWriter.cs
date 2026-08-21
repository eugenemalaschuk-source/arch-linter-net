using System.Globalization;
using System.Text;

namespace ArchLinterNet.Core.History.Reporting;

// Deterministic JSON emission: LF line endings, two-space indentation, no trailing whitespace, and
// the fixed escaping profile. Non-ASCII scalars are emitted directly as UTF-8 rather than as
// optional `\uXXXX` escapes, so canonical bytes are a property of the writer, not of a serializer's
// default settings.
internal sealed class CanonicalJsonWriter
{
    private readonly StringBuilder _builder = new();
    private int _depth;
    private bool _pendingSeparator;

    public string ToCanonicalText() => _builder.ToString();

    public void BeginObject(string? name = null) => Begin(name, '{');

    public void EndObject() => End('}');

    public void BeginArray(string? name = null) => Begin(name, '[');

    public void EndArray() => End(']');

    public void WriteString(string name, string? value)
    {
        WriteMemberPrefix(name);
        _builder.Append(value is null ? "null" : Quote(value));
    }

    public void WriteStringElement(string value)
    {
        WriteElementPrefix();
        _builder.Append(Quote(value));
    }

    public void WriteNumber(string name, long value)
    {
        WriteMemberPrefix(name);
        _builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    public void WriteNumberElement(long value)
    {
        WriteElementPrefix();
        _builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    public void WriteOptionalNumber(string name, int? value)
    {
        WriteMemberPrefix(name);
        _builder.Append(value?.ToString(CultureInfo.InvariantCulture) ?? "null");
    }

    // Counts, TaskKey identifiers, and epoch seconds are exact non-exponent integers at arbitrary
    // precision, so they are written from their canonical decimal text rather than a host numeric type.
    public void WriteIntegerText(string name, string decimalText)
    {
        WriteMemberPrefix(name);
        _builder.Append(decimalText);
    }

    public void WriteCanonicalDecimal(string name, decimal value)
    {
        WriteMemberPrefix(name);
        _builder.Append(value.ToString("F9", CultureInfo.InvariantCulture));
    }

    public void WriteOptionalCanonicalDecimal(string name, decimal? value)
    {
        WriteMemberPrefix(name);
        _builder.Append(value is null ? "null" : value.Value.ToString("F9", CultureInfo.InvariantCulture));
    }

    public void WriteBoolean(string name, bool value)
    {
        WriteMemberPrefix(name);
        _builder.Append(value ? "true" : "false");
    }

    public static string Quote(string value)
    {
        StringBuilder quoted = new("\"");
        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    quoted.Append("\\\"");
                    break;
                case '\\':
                    quoted.Append("\\\\");
                    break;
                case '\b':
                    quoted.Append("\\b");
                    break;
                case '\t':
                    quoted.Append("\\t");
                    break;
                case '\n':
                    quoted.Append("\\n");
                    break;
                case '\f':
                    quoted.Append("\\f");
                    break;
                case '\r':
                    quoted.Append("\\r");
                    break;
                default:
                    if (character < 0x20)
                    {
                        quoted.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:X4}");
                    }
                    else
                    {
                        quoted.Append(character);
                    }

                    break;
            }
        }

        return quoted.Append('"').ToString();
    }

    private void Begin(string? name, char opening)
    {
        if (name is null)
        {
            WriteElementPrefix();
        }
        else
        {
            WriteMemberPrefix(name);
        }

        _builder.Append(opening);
        _depth++;
        _pendingSeparator = false;
    }

    private void End(char closing)
    {
        _depth--;
        if (_pendingSeparator)
        {
            _builder.Append('\n').Append(' ', _depth * 2);
        }

        _builder.Append(closing);
        _pendingSeparator = true;
    }

    private void WriteMemberPrefix(string name)
    {
        WriteElementPrefix();
        _builder.Append(Quote(name)).Append(": ");
    }

    private void WriteElementPrefix()
    {
        if (_pendingSeparator)
        {
            _builder.Append(',');
        }

        if (_depth > 0)
        {
            _builder.Append('\n').Append(' ', _depth * 2);
        }

        _pendingSeparator = true;
    }
}
