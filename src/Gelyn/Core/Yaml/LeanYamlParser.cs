using System.Buffers;

using Gelyn.Exceptions;

namespace Gelyn.Core.Yaml;

/// <summary>
///     Reads a flat YAML mapping one key and one value at a time, where a value is text on one line or a list of
///     such text.
/// </summary>
/// <seealso href="https://yaml.org/spec/1.2.2/"/>
internal ref struct LeanYamlParser
{
    private const string Blanks = " \t";

    private static readonly SearchValues<char> FlowStops = SearchValues.Create(",[]{}:#");

    private readonly ReadOnlySpan<char> _block;
    private ReadOnlySpan<char> _text;
    private int _position;
    private int _lineEnd;
    private int _next;
    private int _line;
    private int _keyLine;
    private int _valueLine;
    private int _valueIndex;
    private int _listColumn;
    private int _listIndex;
    private bool _isStarted;
    private bool _inBlockList;
    private bool _inFlowList;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LeanYamlParser"/> struct.
    /// </summary>
    /// <param name="block">
    ///     The raw text of the block, with or without its delimiters. Its first line counts as line 1.
    /// </param>
    public LeanYamlParser(ReadOnlySpan<char> block)
    {
        this._block = block;
        this._line = 1;
        this._valueIndex = -1;
    }

    /// <summary>
    ///     The one-based line of the key that was read last.
    /// </summary>
    public readonly int KeyLine => this._keyLine;

    /// <summary>
    ///     The one-based line that the value or the entry that was read last starts on.
    /// </summary>
    public readonly int ValueLine => this._valueLine;

    /// <summary>
    ///     The text of the scalar that was read last, which stays valid until the parser reads on.
    /// </summary>
    public readonly ReadOnlySpan<char> Text => this._text;

    /// <summary>
    ///     Reads the next key of the mapping. A value or an entry that the caller left unread is read first, so that
    ///     it is held to the same rules.
    /// </summary>
    /// <param name="key">
    ///     When this method returns, the key as text, which stays valid until the parser reads on.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when a key was read, <see langword="false"/> at the end of the block.
    /// </returns>
    /// <exception cref="FrontMatterException">
    ///     The block is malformed, or uses YAML that front matter does not support.
    /// </exception>
    public bool ReadKey(out ReadOnlySpan<char> key)
    {
        if (this._isStarted)
            this.SkipValue();
        else
            this.StartBlock();

        YamlLineKind kind = this.SkipToContent(out int indent, out int content);
        key = default;

        if (kind is YamlLineKind.End)
            return false;

        if (kind is YamlLineKind.Marker)
        {
            this.AdvanceLine();
            this.ExpectBlockEnd();
            return false;
        }

        if (indent > 0)
            throw new FrontMatterException(this._line, MalformedReasons.Indented);

        if (content > 0)
            throw new FrontMatterException(this._line, MalformedReasons.TabIndents);

        ReadOnlySpan<char> line = this._block[this._position..this._lineEnd];
        int colon = FindKeyEnd(line);

        if (line[0] is '"' or '\'')
            throw new FrontMatterException(this._line, MalformedReasons.QuotedKey);

        if (line[0] is '&' or '*' or '!')
            throw new FrontMatterException(this._line, MalformedReasons.NoProperties);

        if (colon <= 0 || line[0] is '[' or ']' or '{' or '}' or ',' || (line[0] is '-' or '?' && IsEndOrBlank(line, 1)))
            throw new FrontMatterException(this._line, MalformedReasons.NotAKey);

        key = line[..colon].TrimEnd(Blanks);
        this._keyLine = this._line;
        this._valueIndex = this._position + colon + 1;

        return true;
    }

    /// <summary>
    ///     Reads the value of the key that was read last.
    /// </summary>
    /// <returns>
    ///     <see cref="YamlNodeKind.Scalar"/> for text on the line of the key, which is then in <see cref="Text"/>,
    ///     <see cref="YamlNodeKind.Sequence"/> for a list, whose entries <see cref="ReadEntry"/> reads, and
    ///     <see cref="YamlNodeKind.Null"/> when the key has no value or its value was read before.
    /// </returns>
    /// <exception cref="FrontMatterException">
    ///     The value is malformed, or uses YAML that front matter does not support.
    /// </exception>
    public YamlNodeKind ReadValue()
    {
        int index = this._valueIndex;

        if (index < 0)
            return YamlNodeKind.Null;

        this._valueIndex = -1;
        this._valueLine = this._line;
        index = this.SkipBlanks(index);

        if (this.IsLineEnd(index))
        {
            this.AdvanceLine();
            return this.OpenBlockList();
        }

        if (this._block[index] == '[')
        {
            this._inFlowList = true;
            this._listIndex = index + 1;

            return YamlNodeKind.Sequence;
        }

        YamlNodeKind kind = this.ReadScalar(ref index, false);
        this.ExpectLineEnd(index, MalformedReasons.AfterQuote);

        return kind;
    }

    /// <summary>
    ///     Reads the next entry of the list that <see cref="ReadValue"/> found.
    /// </summary>
    /// <param name="kind">
    ///     When this method returns, <see cref="YamlNodeKind.Scalar"/> for an entry whose text is in
    ///     <see cref="Text"/>, or <see cref="YamlNodeKind.Null"/> for an entry without text.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when an entry was read, <see langword="false"/> at the end of the list.
    /// </returns>
    /// <exception cref="FrontMatterException">
    ///     The list is malformed, or uses YAML that front matter does not support.
    /// </exception>
    public bool ReadEntry(out YamlNodeKind kind)
    {
        kind = YamlNodeKind.Null;

        if (this._inBlockList)
        {
            if (this.ReadBlockEntry(out kind))
                return true;

            this._inBlockList = false;
        }
        else if (this._inFlowList)
        {
            if (this.ReadFlowEntry(out kind))
                return true;

            this._inFlowList = false;
            this.ExpectLineEnd(this._listIndex, MalformedReasons.AfterBracket);
        }

        return false;
    }

    #region Helpers

    private static bool IsBlank(char c) => c is ' ' or '\t';

    private static bool IsEndOrBlank(ReadOnlySpan<char> text, int index) => index >= text.Length || IsBlank(text[index]);

    private static bool IsMarker(ReadOnlySpan<char> line) =>
        (line.StartsWith("---") || line.StartsWith("...")) && IsEndOrBlank(line, 3);

    private static bool IsNull(ReadOnlySpan<char> text) => text is "~" or "null" or "Null" or "NULL";

    // A key ends at the first ':' that a blank or the end of the line follows, and a comment ends the search.
    private static int FindKeyEnd(ReadOnlySpan<char> line)
    {
        int index = 0;

        while (true)
        {
            int offset = line[index..].IndexOfAny(':', '#');

            if (offset < 0)
                return -1;

            index += offset;

            if (line[index] == ':')
            {
                if (IsEndOrBlank(line, index + 1))
                    return index;
            }
            else if (index > 0 && IsBlank(line[index - 1]))
            {
                return -1;
            }

            index++;
        }
    }

    private void StartBlock()
    {
        this._isStarted = true;
        this.MoveToLine(0);

        ReadOnlySpan<char> first = this._block[..this._lineEnd];

        if (first.StartsWith("---") && first[3..].IndexOfAnyExcept(' ', '\t') < 0)
            this.AdvanceLine();
    }

    private void SkipValue()
    {
        if (this._valueIndex >= 0)
            _ = this.ReadValue();

        while (this.ReadEntry(out _))
        {
            // An entry that nobody asked for is read for its syntax alone.
        }
    }

    private void MoveToLine(int start)
    {
        this._position = start;
        int offset = this._block[start..].IndexOfAny('\r', '\n');

        if (offset < 0)
        {
            this._lineEnd = this._block.Length;
            this._next = this._block.Length;
            return;
        }

        this._lineEnd = start + offset;
        this._next = this._lineEnd + 1;

        if (this._block[this._lineEnd] == '\r' && this._next < this._block.Length && this._block[this._next] == '\n')
            this._next++;
    }

    private void AdvanceLine()
    {
        this.MoveToLine(this._next);
        this._line++;
    }

    // The indent counts the leading spaces, and the content is the offset of the first character that is no blank.
    private readonly YamlLineKind ClassifyLine(out int indent, out int content)
    {
        if (this._position >= this._block.Length)
        {
            indent = 0;
            content = 0;
            return YamlLineKind.End;
        }

        ReadOnlySpan<char> line = this._block[this._position..this._lineEnd];
        content = line.IndexOfAnyExcept(' ', '\t');

        if (content < 0)
        {
            indent = line.Length;
            content = line.Length;
            return YamlLineKind.Blank;
        }

        indent = line.IndexOfAnyExcept(' ');

        if (line[content] == '#')
            return YamlLineKind.Comment;

        if (content > 0 || !IsMarker(line))
            return YamlLineKind.Content;

        if (line[3..].IndexOfAnyExcept(' ', '\t') >= 0)
            throw new FrontMatterException(this._line, MalformedReasons.SecondDocument);

        return YamlLineKind.Marker;
    }

    private YamlLineKind SkipToContent(out int indent, out int content)
    {
        YamlLineKind kind;

        while ((kind = this.ClassifyLine(out indent, out content)) is YamlLineKind.Blank or YamlLineKind.Comment)
            this.AdvanceLine();

        return kind;
    }

    private void ExpectBlockEnd()
    {
        if (this.SkipToContent(out _, out _) is not YamlLineKind.End)
            throw new FrontMatterException(this._line, MalformedReasons.SecondDocument);
    }

    private readonly int SkipBlanks(int index)
    {
        int offset = this._block[index..this._lineEnd].IndexOfAnyExcept(' ', '\t');
        return offset < 0 ? this._lineEnd : index + offset;
    }

    private readonly bool IsEndOrBlank(int index) => index >= this._lineEnd || IsBlank(this._block[index]);

    private readonly bool IsEntry(int index) => this._block[index] == '-' && this.IsEndOrBlank(index + 1);

    // A '#' starts a comment only behind a blank.
    private readonly bool IsLineEnd(int index) => index == this._lineEnd || (this._block[index] == '#' && IsBlank(this._block[index - 1]));

    // Only a comment may follow a value, and a blank separates the two.
    private void ExpectLineEnd(int index, string reason)
    {
        int next = this.SkipBlanks(index);

        if (next < this._lineEnd && (next == index || this._block[next] != '#'))
            throw new FrontMatterException(this._line, reason);

        this.AdvanceLine();
    }

    // The entries of a list sit below its key, one to a line, with their dashes at one column.
    private YamlNodeKind OpenBlockList()
    {
        if (this.SkipToContent(out int indent, out int content) is not YamlLineKind.Content || content != indent || !this.IsEntry(this._position + indent))
            return YamlNodeKind.Null;

        this._inBlockList = true;
        this._listColumn = indent;
        this._valueLine = this._line;

        return YamlNodeKind.Sequence;
    }

    private bool ReadBlockEntry(out YamlNodeKind kind)
    {
        kind = YamlNodeKind.Null;
        YamlLineKind line = this.SkipToContent(out int indent, out int content);

        if (line is not YamlLineKind.Content || indent < this._listColumn)
            return false;

        if (indent > this._listColumn)
            throw new FrontMatterException(this._line, MalformedReasons.Indented);

        if (content != indent || !this.IsEntry(this._position + indent))
            return false;

        int index = this.SkipBlanks(this._position + indent + 1);
        this._valueLine = this._line;

        if (this.IsLineEnd(index))
        {
            this.AdvanceLine();
            return true;
        }

        kind = this.ReadScalar(ref index, false);
        this.ExpectLineEnd(index, MalformedReasons.AfterQuote);

        return true;
    }

    // A list in brackets opens and closes on one line, and a comma may follow its last entry.
    private bool ReadFlowEntry(out YamlNodeKind kind)
    {
        kind = YamlNodeKind.Null;
        int index = this.SkipBlanks(this._listIndex);

        if (this.IsLineEnd(index))
            throw new FrontMatterException(this._line, MalformedReasons.OpenBracket);

        if (this._block[index] == ']')
        {
            this._listIndex = index + 1;
            return false;
        }

        if (this._block[index] == ',')
            throw new FrontMatterException(this._line, MalformedReasons.EmptyEntry);

        kind = this.ReadScalar(ref index, true);
        index = this.SkipBlanks(index);

        if (this.IsLineEnd(index))
            throw new FrontMatterException(this._line, MalformedReasons.OpenBracket);

        if (this._block[index] == ',')
            index++;
        else if (this._block[index] != ']')
            throw new FrontMatterException(this._line, MalformedReasons.NoSeparator);

        this._listIndex = index;

        return true;
    }

    // Reads the text that starts at the index, and moves the index behind it.
    private YamlNodeKind ReadScalar(ref int index, bool isFlow)
    {
        char c = this._block[index];

        switch (c)
        {
            case '"' or '\'':
                this._text = this.ReadQuotedScalar(ref index);
                return YamlNodeKind.Scalar;

            case '|' or '>' when !isFlow:
                throw new FrontMatterException(this._line, MalformedReasons.BlockText);

            case '&' or '*' or '!':
                throw new FrontMatterException(this._line, MalformedReasons.NoProperties);

            case '[' or ']' or '{' or '}' or ',' or '#' or '|' or '>' or '%' or '@' or '`':
                throw new FrontMatterException(this._line, MalformedReasons.DescribeStart(c, false));

            case '-' or '?' or ':' when this.IsEndOrBlank(index + 1):
                throw new FrontMatterException(this._line, MalformedReasons.DescribeStart(c, true));
        }

        int end = this.FindPlainEnd(index, isFlow);
        this._text = this._block[index..end];
        index = end;

        return IsNull(this._text) ? YamlNodeKind.Null : YamlNodeKind.Scalar;
    }

    // Plain text ends at a comment and at the end of its line, and inside brackets at an indicator of the list.
    private readonly int FindPlainEnd(int index, bool isFlow)
    {
        ReadOnlySpan<char> rest = this._block[index..this._lineEnd];
        int offset = 0;

        while (true)
        {
            int found = isFlow ? rest[offset..].IndexOfAny(FlowStops) : rest[offset..].IndexOfAny(':', '#');

            if (found < 0)
            {
                offset = rest.Length;
                break;
            }

            offset += found;

            if (rest[offset] == ':')
            {
                if (IsEndOrBlank(rest, offset + 1) || (isFlow && rest[offset + 1] is ',' or ']'))
                    throw new FrontMatterException(this._line, MalformedReasons.MappingInText);
            }
            else if (rest[offset] != '#' || (offset > 0 && IsBlank(rest[offset - 1])))
            {
                break;
            }

            offset++;
        }

        return index + rest[..offset].TrimEnd(Blanks).Length;
    }

    private ReadOnlySpan<char> ReadQuotedScalar(scoped ref int index)
    {
        char quote = this._block[index];
        ReadOnlySpan<char> rest = this._block[(index + 1)..this._lineEnd];
        int close = quote == '"' ? rest.IndexOfAny('"', '\\') : rest.IndexOf('\'');

        if (close < 0)
            throw new FrontMatterException(this._line, MalformedReasons.OpenQuote);

        // Without an escape, the text is a slice of the block.
        if (rest[close] == quote && (quote == '"' || close + 1 == rest.Length || rest[close + 1] != '\''))
        {
            index += close + 2;
            return rest[..close];
        }

        char[] buffer = new char[rest.Length];
        int length = 0;

        for (int offset = 0; offset < rest.Length; offset++)
        {
            char c = rest[offset];

            if (c == quote && (quote == '"' || offset + 1 == rest.Length || rest[offset + 1] != '\''))
            {
                index += offset + 2;
                return buffer.AsSpan(0, length);
            }

            // Two single quotes stand for one, and a backslash stands before a double quote or a backslash.
            if (c == quote || (c == '\\' && quote == '"'))
            {
                if (++offset == rest.Length)
                    break;

                if (c == '\\' && rest[offset] is not ('"' or '\\'))
                    throw new FrontMatterException(this._line, MalformedReasons.DescribeEscape(rest[offset]));

                c = rest[offset];
            }

            buffer[length++] = c;
        }

        throw new FrontMatterException(this._line, MalformedReasons.OpenQuote);
    }

    #endregion
}
