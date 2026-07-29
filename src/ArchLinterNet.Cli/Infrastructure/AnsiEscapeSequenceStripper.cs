using System.Text;

namespace ArchLinterNet.Cli.Infrastructure;

internal static class AnsiEscapeSequenceStripper
{
    public static string Strip(string content)
    {
        var output = new StringBuilder(content.Length);
        var parser = new Parser(value => output.Append(value));
        parser.Write(content);
        return output.ToString();
    }

    internal sealed class Parser(Action<char> writeVisibleCharacter)
    {
        private ParserState _state;

        public void Write(ReadOnlySpan<char> content)
        {
            foreach (char value in content)
            {
                Write(value);
            }
        }

        public void Write(char value)
        {
            switch (_state)
            {
                case ParserState.Text:
                    if (value == '\u001b')
                    {
                        _state = ParserState.Escape;
                    }
                    else if (value == '\u009b')
                    {
                        _state = ParserState.ControlSequence;
                    }
                    else if (value == '\u009d')
                    {
                        _state = ParserState.OperatingSystemCommand;
                    }
                    else
                    {
                        writeVisibleCharacter(value);
                    }
                    break;
                case ParserState.Escape:
                    _state = value switch
                    {
                        '[' => ParserState.ControlSequence,
                        ']' => ParserState.OperatingSystemCommand,
                        '\u001b' => ParserState.Escape,
                        >= '\u0020' and <= '\u002f' => ParserState.Escape,
                        _ => ParserState.Text,
                    };
                    if (_state == ParserState.Text && value > '\u007e')
                    {
                        writeVisibleCharacter(value);
                    }
                    break;
                case ParserState.ControlSequence:
                    if (value is >= '\u0040' and <= '\u007e')
                    {
                        _state = ParserState.Text;
                    }
                    else if (value is not (>= '\u0020' and <= '\u003f'))
                    {
                        _state = ParserState.Text;
                        writeVisibleCharacter(value);
                    }
                    break;
                case ParserState.OperatingSystemCommand:
                    if (value is '\u0007' or '\u009c')
                    {
                        _state = ParserState.Text;
                    }
                    else if (value == '\u001b')
                    {
                        _state = ParserState.OperatingSystemCommandTerminator;
                    }
                    break;
                case ParserState.OperatingSystemCommandTerminator:
                    if (value == '\\')
                    {
                        _state = ParserState.Text;
                    }
                    else
                    {
                        _state = ParserState.OperatingSystemCommand;
                        Write(value);
                    }
                    break;
            }
        }

        private enum ParserState
        {
            Text,
            Escape,
            ControlSequence,
            OperatingSystemCommand,
            OperatingSystemCommandTerminator,
        }
    }
}

internal sealed class AnsiStrippingTextWriter(TextWriter inner) : TextWriter
{
    private readonly AnsiEscapeSequenceStripper.Parser _parser = new(inner.Write);

    public override Encoding Encoding => inner.Encoding;

    public override IFormatProvider FormatProvider => inner.FormatProvider;

    public override void Flush()
    {
        inner.Flush();
    }

    public override void Write(char value)
    {
        _parser.Write(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        _parser.Write(buffer.AsSpan(index, count));
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        _parser.Write(buffer);
    }

    public override void Write(string? value)
    {
        if (value is not null)
        {
            _parser.Write(value);
        }
    }
}
