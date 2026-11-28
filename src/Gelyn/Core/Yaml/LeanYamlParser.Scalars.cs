using System.Buffers;
using System.Globalization;
using System.Text;

using Gelyn.Core.Internals;
using Gelyn.Exceptions;

namespace Gelyn.Core.Yaml;

internal ref partial struct LeanYamlParser
{
    #region Scalars

    private static bool IsNull(ReadOnlySpan<char> text) => text is "~" or "null" or "Null" or "NULL";

    // A line break between two lines of text folds into a space, and every blank line between them into a
    // line break, section 6.5.
    private static int FoldBreaks(Span<char> buffer, int length, int breaks)
    {
        if (breaks == 0)
        {
            buffer[length] = ' ';
            return length + 1;
        }

        buffer.Slice(length, breaks).Fill('\n');
        return length + breaks;
    }

    private static char DecodeEscape(char c) => c switch
    {
        '0' => '\0',
        'a' => '\a',
        'b' => '\b',
        't' or '\t' => '\t',
        'n' => '\n',
        'v' => '\v',
        'f' => '\f',
        'r' => '\r',
        'e' => Escape,
        'N' => NextLine,
        '_' => NoBreakSpace,
        'L' => LineSeparator,
        'P' => ParagraphSeparator,
        ' ' or '"' or '/' or '\\' => c,
        _ => char.MaxValue,
    };

    // Folding, escapes and chomping never make a text longer than its extent in the block, so one buffer of the
    // length of the block serves every scalar and never grows.
    private Span<char> RentBuffer() => this._buffer ??= ArrayPool<char>.Shared.Rent(this._block.Length);

    // A plain scalar folds over the lines that are indented further than its parent, section 7.3.3.
    private YamlNodeKind ReadPlainScalar(int index, int parentIndent)
    {
        int end = this.ScanPlainLine(index, out bool hasComment);
        ReadOnlySpan<char> first = this._block[index..end];
        Span<char> buffer = default;
        int length = 0;

        this.AdvanceLine();

        while (!hasComment)
        {
            int breaks = 0;
            YamlLineKind kind;
            int indent;
            int content;

            while ((kind = this.ClassifyLine(out indent, out content)) is YamlLineKind.Blank)
            {
                breaks++;
                this.AdvanceLine();
            }

            if (kind is not YamlLineKind.Content || indent <= parentIndent)
                break;

            if (length == 0)
            {
                buffer = this.RentBuffer();
                first.CopyTo(buffer);
                length = first.Length;
            }

            length = FoldBreaks(buffer, length, breaks);

            int start = this._position + content;
            end = this.ScanPlainLine(start, out hasComment);
            this._block[start..end].CopyTo(buffer[length..]);
            length += end - start;

            this.AdvanceLine();
        }

        if (length > 0)
        {
            this._text = buffer[..length];
            return YamlNodeKind.Scalar;
        }

        this._text = first;
        return IsNull(first) ? YamlNodeKind.Null : YamlNodeKind.Scalar;
    }

    // Returns the end of the text that a line of a plain scalar holds from the index on.
    private readonly int ScanPlainLine(int index, out bool hasComment)
    {
        ReadOnlySpan<char> rest = this._block[index..this._lineEnd];
        int end = rest.Length;
        int offset = 0;
        hasComment = false;

        while (true)
        {
            int found = rest[offset..].IndexOfAny(':', '#');

            if (found < 0)
                break;

            offset += found;

            if (rest[offset] == ':')
            {
                if (IsEndOrBlank(rest, offset + 1))
                    throw new FrontMatterException(this._line, MalformedReasons.MappingInText);
            }
            else if (offset > 0 && IsBlank(rest[offset - 1]))
            {
                hasComment = true;
                end = offset;
                break;
            }

            offset++;
        }

        return index + rest[..end].TrimEnd(Blanks).Length;
    }

    // Reads the quoted scalar that opens at the index, and moves the index past its closing quote.
    private ReadOnlySpan<char> ReadQuotedScalar(scoped ref int index, int parentIndent)
    {
        char quote = this._block[index];
        int line = this._line;
        ReadOnlySpan<char> rest = this._block[(index + 1)..this._lineEnd];
        int close = quote == '"' ? rest.IndexOfAny('"', '\\') : rest.IndexOf('\'');

        // On one line and without an escape, the text is a slice of the block.
        if (close >= 0 && rest[close] == quote && (quote == '"' || close + 1 == rest.Length || rest[close + 1] != '\''))
        {
            index += close + 2;
            return rest[..close];
        }

        Span<char> buffer = this.RentBuffer();
        int length = 0;
        int kept = 0;

        index++;

        while (true)
        {
            // A line break drops the blanks before it, which is why kept trails length, section 7.3.1.
            if (index == this._lineEnd)
            {
                length = this.FoldQuotedBreak(buffer, kept, parentIndent, line, false, out index);
                kept = length;
                continue;
            }

            char c = this._block[index++];

            if (c == quote)
            {
                if (quote == '"' || index == this._lineEnd || this._block[index] != '\'')
                    return buffer[..length];

                index++;
            }
            else if (c == '\\' && quote == '"')
            {
                length = index == this._lineEnd
                    ? this.FoldQuotedBreak(buffer, length, parentIndent, line, true, out index)
                    : this.DecodeEscape(buffer, length, ref index);

                kept = length;
                continue;
            }

            buffer[length++] = c;

            if (!IsBlank(c))
                kept = length;
        }
    }

    // Moves to the line that continues a quoted scalar. An escaped line break joins the two lines as they are.
    private int FoldQuotedBreak(Span<char> buffer, int length, int parentIndent, int openLine, bool isEscaped, out int index)
    {
        int breaks = 0;

        while (true)
        {
            this.AdvanceLine();

            if (this._position >= this._block.Length)
                throw new FrontMatterException(openLine, MalformedReasons.OpenQuote);

            ReadOnlySpan<char> line = this._block[this._position..this._lineEnd];
            int content = line.IndexOfAnyExcept(' ', '\t');

            if (content < 0)
            {
                breaks++;
                continue;
            }

            int indent = line.IndexOfAnyExcept(' ');

            if (content == 0 && IsMarker(line))
                throw new FrontMatterException(openLine, MalformedReasons.OpenQuote);

            if (indent <= parentIndent)
                throw new FrontMatterException(this._line, content > indent ? MalformedReasons.TabIndents : MalformedReasons.NotIndented);

            index = this._position + content;
            break;
        }

        return breaks > 0 || !isEscaped ? FoldBreaks(buffer, length, breaks) : length;
    }

    // Decodes the escape whose character sits at the index, section 5.7.
    private readonly int DecodeEscape(Span<char> buffer, int length, scoped ref int index)
    {
        char c = this._block[index++];
        int digits = c switch { 'x' => 2, 'u' => 4, 'U' => 8, _ => 0 };

        if (digits == 0)
        {
            char decoded = DecodeEscape(c);

            if (decoded == char.MaxValue)
                throw new FrontMatterException(this._line, MalformedReasons.DescribeEscape(c));

            buffer[length] = decoded;
            return length + 1;
        }

        ReadOnlySpan<char> code = this._block[index..this._lineEnd];

        if (code.Length < digits || !uint.TryParse(code[..digits], NumberStyles.AllowHexSpecifier, Tool.Culture, out uint value) || !Rune.TryCreate(value, out Rune rune))
            throw new FrontMatterException(this._line, MalformedReasons.DescribeEscape(c, digits));

        index += digits;
        return length + rune.EncodeToUtf16(buffer[length..]);
    }

    // Reads the block scalar whose indicator sits at the index, sections 8.1.2 and 8.1.3.
    private ReadOnlySpan<char> ReadBlockScalar(int index, int parentIndent)
    {
        bool isLiteral = this._block[index] == '|';
        YamlChomping chomping = this.ReadBlockHeader(index + 1, out int indicator);
        this.AdvanceLine();

        Span<char> buffer = this.RentBuffer();
        int length = 0;
        int indent = indicator > 0 ? parentIndent + indicator : 0;
        int breaks = this.SkipBlockBreaks(ref indent, parentIndent);
        bool hasBreak = false;
        bool wasIndented = false;

        while (this.IsBlockText(indent))
        {
            ReadOnlySpan<char> text = this._block[(this._position + indent)..this._lineEnd];
            bool isIndented = IsBlank(text[0]);

            // A folded scalar keeps the line breaks around a line that is indented further, section 8.1.3.
            if (hasBreak && (isLiteral || wasIndented || isIndented))
                buffer[length++] = '\n';
            else if (hasBreak && breaks == 0)
                buffer[length++] = ' ';

            buffer.Slice(length, breaks).Fill('\n');
            length += breaks;
            text.CopyTo(buffer[length..]);
            length += text.Length;

            wasIndented = isIndented;
            hasBreak = this._lineEnd < this._block.Length;
            this.AdvanceLine();
            breaks = this.SkipBlockBreaks(ref indent, parentIndent);
        }

        if (chomping is not YamlChomping.Strip && hasBreak)
            buffer[length++] = '\n';

        if (chomping is YamlChomping.Keep)
        {
            buffer.Slice(length, breaks).Fill('\n');
            length += breaks;
        }

        return buffer[..length];
    }

    // The indicators may come in either order, and only a comment may follow them, section 8.1.1.
    private readonly YamlChomping ReadBlockHeader(int index, out int indicator)
    {
        ReadOnlySpan<char> rest = this._block[index..this._lineEnd];
        YamlChomping chomping = YamlChomping.Clip;
        int offset = 0;
        indicator = 0;

        for (; offset < rest.Length && offset < 2; offset++)
        {
            char c = rest[offset];

            if (c is '+' or '-' && chomping is YamlChomping.Clip)
                chomping = c == '+' ? YamlChomping.Keep : YamlChomping.Strip;
            else if (c is >= '1' and <= '9' && indicator == 0)
                indicator = c - '0';
            else
                break;
        }

        ReadOnlySpan<char> tail = rest[offset..];
        int content = tail.IndexOfAnyExcept(' ', '\t');

        if (content >= 0 && (content == 0 || tail[content] != '#'))
            throw new FrontMatterException(this._line, MalformedReasons.AfterIndicator);

        return chomping;
    }

    // Counts the blank lines ahead, and takes the indentation of the scalar from its first line of text when no
    // indicator gave it, section 8.1.1.1.
    private int SkipBlockBreaks(scoped ref int indent, int parentIndent)
    {
        int breaks = 0;
        int widest = 0;
        int widestLine = 0;

        while (this._position < this._block.Length)
        {
            ReadOnlySpan<char> line = this._block[this._position..this._lineEnd];
            int spaces = line.IndexOfAnyExcept(' ');

            // A line of spaces alone is blank, unless it reaches past the indentation and so holds text.
            if (spaces < 0 && (indent == 0 || line.Length <= indent))
            {
                if (indent == 0 && line.Length > widest)
                {
                    widest = line.Length;
                    widestLine = this._line;
                }

                breaks++;
                this.AdvanceLine();
                continue;
            }

            if (spaces < 0)
                break;

            if (line[spaces] == '\t' && spaces < (indent == 0 ? parentIndent + 1 : indent))
                throw new FrontMatterException(this._line, MalformedReasons.TabIndents);

            if (indent == 0 && widest > Math.Max(spaces, parentIndent + 1))
                throw new FrontMatterException(widestLine, MalformedReasons.BlankTooWide);

            if (indent == 0 && spaces > parentIndent)
                indent = spaces;

            break;
        }

        return breaks;
    }

    private readonly bool IsBlockText(int indent)
    {
        if (indent == 0 || this._position >= this._block.Length)
            return false;

        ReadOnlySpan<char> line = this._block[this._position..this._lineEnd];
        return line.Length > indent && line[..indent].IndexOfAnyExcept(' ') < 0;
    }

    #endregion
}
