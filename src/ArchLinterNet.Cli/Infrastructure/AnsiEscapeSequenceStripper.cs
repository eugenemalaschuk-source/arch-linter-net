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
                    WriteText(value);
                    break;
                case ParserState.Escape:
                    WriteEscape(value);
                    break;
                case ParserState.EscapeIntermediate:
                    WriteEscapeIntermediate(value);
                    break;
                case ParserState.ControlSequence:
                    WriteControlSequence(value);
                    break;
                case ParserState.OperatingSystemCommand:
                    WriteOperatingSystemCommand(value);
                    break;
                case ParserState.StringCommand:
                    WriteStringCommand(value);
                    break;
                case ParserState.OperatingSystemCommandTerminator:
                    WriteOperatingSystemCommandTerminator(value);
                    break;
                case ParserState.StringCommandTerminator:
                    WriteStringCommandTerminator(value);
                    break;
            }
        }

        private void WriteText(char value)
        {
            _state = value switch
            {
                '\u001b' => ParserState.Escape,
                '\u0090' or '\u0098' or '\u009e' or '\u009f' => ParserState.StringCommand,
                '\u009b' => ParserState.ControlSequence,
                '\u009d' => ParserState.OperatingSystemCommand,
                _ => ParserState.Text,
            };

            if (_state == ParserState.Text)
            {
                writeVisibleCharacter(value);
            }
        }

        private void WriteEscape(char value)
        {
            _state = value switch
            {
                '[' => ParserState.ControlSequence,
                ']' => ParserState.OperatingSystemCommand,
                'P' or 'X' or '^' or '_' => ParserState.StringCommand,
                >= '\u0020' and <= '\u002f' => ParserState.EscapeIntermediate,
                '\u001b' => ParserState.Escape,
                >= '\u0030' and <= '\u007e' => ParserState.Text,
                _ => ParserState.Text,
            };

            if (_state == ParserState.Text && value > '\u007e')
            {
                writeVisibleCharacter(value);
            }
        }

        private void WriteEscapeIntermediate(char value)
        {
            if (value is >= '\u0030' and <= '\u007e')
            {
                _state = ParserState.Text;
            }
            else if (value == '\u001b')
            {
                _state = ParserState.Escape;
            }
        }

        private void WriteControlSequence(char value)
        {
            if (value is >= '\u0040' and <= '\u007e')
            {
                _state = ParserState.Text;
            }
            else if (value is not (>= '\u0020' and <= '\u003f'))
            {
                _state = ParserState.Text;
                writeVisibleCharacter(value);
            }
        }

        private void WriteOperatingSystemCommand(char value)
        {
            if (value is '\u0007' or '\u009c')
            {
                _state = ParserState.Text;
            }
            else if (value == '\u001b')
            {
                _state = ParserState.OperatingSystemCommandTerminator;
            }
        }

        private void WriteStringCommand(char value)
        {
            if (value == '\u009c')
            {
                _state = ParserState.Text;
            }
            else if (value == '\u001b')
            {
                _state = ParserState.StringCommandTerminator;
            }
        }

        private void WriteOperatingSystemCommandTerminator(char value)
        {
            if (value == '\\')
            {
                _state = ParserState.Text;
            }
            else
            {
                _state = value == '\u001b'
                    ? ParserState.OperatingSystemCommandTerminator
                    : ParserState.OperatingSystemCommand;
            }
        }

        private void WriteStringCommandTerminator(char value)
        {
            if (value == '\\')
            {
                _state = ParserState.Text;
            }
            else
            {
                _state = value == '\u001b'
                    ? ParserState.StringCommandTerminator
                    : ParserState.StringCommand;
            }
        }

        private enum ParserState
        {
            Text,
            Escape,
            EscapeIntermediate,
            ControlSequence,
            OperatingSystemCommand,
            StringCommand,
            OperatingSystemCommandTerminator,
            StringCommandTerminator,
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
