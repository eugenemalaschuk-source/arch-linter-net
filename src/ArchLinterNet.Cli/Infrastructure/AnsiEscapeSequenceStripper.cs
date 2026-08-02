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

// The parser reports visible characters one at a time. Forwarding each of them straight to the
// inner writer costs one virtual write per character, which is invisible on a few console lines and
// crippling on a real report: a 45 MB `--format json` document took ~43 s to reach a redirected
// stdout, far longer than producing it (issue #419). Visible characters are therefore batched here
// and handed to the inner writer in blocks. The buffer is drained at the end of every public write,
// so each call still publishes everything it was given before returning — ordering against a
// separately written stderr is unchanged — and the parser's own state machine is untouched, so the
// characters and their order are exactly what they were.
internal sealed class AnsiStrippingTextWriter : TextWriter
{
    private const int BlockSize = 8192;

    private readonly TextWriter _inner;
    private readonly AnsiEscapeSequenceStripper.Parser _parser;
    private readonly char[] _block = new char[BlockSize];
    private int _blockLength;

    public AnsiStrippingTextWriter(TextWriter inner)
    {
        _inner = inner;
        _parser = new AnsiEscapeSequenceStripper.Parser(AppendVisibleCharacter);
    }

    public override Encoding Encoding => _inner.Encoding;

    public override IFormatProvider FormatProvider => _inner.FormatProvider;

    public override void Flush()
    {
        DrainBlock();
        _inner.Flush();
    }

    public override void Write(char value)
    {
        _parser.Write(value);
        DrainBlock();
    }

    public override void Write(char[] buffer, int index, int count)
    {
        _parser.Write(buffer.AsSpan(index, count));
        DrainBlock();
    }

    public override void Write(ReadOnlySpan<char> buffer)
    {
        _parser.Write(buffer);
        DrainBlock();
    }

    public override void Write(string? value)
    {
        if (value is not null)
        {
            _parser.Write(value);
            DrainBlock();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DrainBlock();
        }

        base.Dispose(disposing);
    }

    private void AppendVisibleCharacter(char value)
    {
        _block[_blockLength++] = value;
        if (_blockLength == _block.Length)
        {
            DrainBlock();
        }
    }

    private void DrainBlock()
    {
        if (_blockLength == 0)
        {
            return;
        }

        _inner.Write(_block.AsSpan(0, _blockLength));
        _blockLength = 0;
    }
}
