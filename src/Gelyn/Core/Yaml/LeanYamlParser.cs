using System.Buffers;

using Gelyn.Exceptions;

namespace Gelyn.Core.Yaml;

/// <summary>
///     Reads a flat YAML mapping one key and one value at a time, in the subset of YAML 1.2 that front matter is
///     written in.
/// </summary>
/// <seealso href="https://yaml.org/spec/1.2.2/"/>
internal ref partial struct LeanYamlParser
{
    private const string Blanks = " \t";

    private const char Escape = (char)0x1B;
    private const char NextLine = (char)0x85;
    private const char NoBreakSpace = (char)0xA0;
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;
    private const char BeforeSurrogates = (char)0xD7FF;
    private const char AfterSurrogates = (char)0xE000;
    private const char LastPrintable = (char)0xFFFD;

    private readonly ReadOnlySpan<char> _block;
    private ReadOnlySpan<char> _text;
    private char[]? _buffer;
    private int _position;
    private int _lineEnd;
    private int _next;
    private int _line;
    private int _keyLine;
    private int _valueLine;
    private int _valueIndex;
    private int _sequenceIndent;
    private int _flowIndex;
    private int _flowLine;
    private bool _isStarted;
    private bool _isLenient;
    private bool _inBlockSequence;
    private bool _inFlowSequence;

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
    ///     Reads the next key of the mapping, and skips whatever the caller left unread of the value before it.
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
            throw new FrontMatterException(this._line, MalformedReasons.ContinuesNothing);

        if (content > 0)
            throw new FrontMatterException(this._line, MalformedReasons.TabIndents);

        ReadOnlySpan<char> line = this._block[this._position..this._lineEnd];
        int colon;

        if (line[0] is '"' or '\'')
        {
            int start = this._line;
            int end = this._position;
            key = this.ReadQuotedScalar(ref end, 0);
            colon = this.SkipBlanks(end) - this._position;

            // An implicit key sits on one line, section 7.4.2.
            if (this._line != start || colon >= line.Length || line[colon] != ':' || !IsEndOrBlank(line, colon + 1))
                throw new FrontMatterException(start, MalformedReasons.NotAKey);
        }
        else
        {
            this.CheckKeyStart(line);
            colon = FindKeyEnd(line);

            if (colon <= 0)
                throw new FrontMatterException(this._line, MalformedReasons.NotAKey);

            key = line[..colon].TrimEnd(Blanks);
        }

        this._keyLine = this._line;
        this._valueIndex = this._position + colon + 1;

        return true;
    }

    /// <summary>
    ///     Reads the value of the key that was read last.
    /// </summary>
    /// <returns>
    ///     The kind of the value, or <see cref="YamlNodeKind.Null"/> when no value is left to read. The text of a
    ///     scalar is in <see cref="Text"/>, and the entries of a sequence are read by <see cref="ReadEntry"/>. A
    ///     mapping is skipped.
    /// </returns>
    /// <exception cref="FrontMatterException">
    ///     The value is malformed, or carries an anchor, a tag or an alias, which only a value that is left unread
    ///     may carry.
    /// </exception>
    public YamlNodeKind ReadValue()
    {
        int index = this._valueIndex;

        if (index < 0)
            return YamlNodeKind.Null;

        this._valueIndex = -1;
        this._isLenient = false;

        return this.ReadNode(index, 0, true);
    }

    /// <summary>
    ///     Reads the next entry of the sequence that <see cref="ReadValue"/> found.
    /// </summary>
    /// <param name="kind">
    ///     When this method returns, the kind of the entry. The text of a scalar is in <see cref="Text"/>. A
    ///     sequence or a mapping inside the sequence is skipped.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when an entry was read, <see langword="false"/> at the end of the sequence.
    /// </returns>
    /// <exception cref="FrontMatterException">
    ///     The sequence is malformed, or uses YAML that front matter does not support.
    /// </exception>
    public bool ReadEntry(out YamlNodeKind kind)
    {
        kind = YamlNodeKind.Null;

        if (this._inBlockSequence)
        {
            if (this.ReadBlockEntry(this._sequenceIndent, out kind))
                return true;

            this._inBlockSequence = false;
        }
        else if (this._inFlowSequence)
        {
            if (this.ReadFlowEntry(ref this._flowIndex, this._sequenceIndent, this._flowLine, out kind))
                return true;

            this._inFlowSequence = false;
            this.ExpectLineEnd(this._flowIndex, MalformedReasons.AfterBracket);
        }

        return false;
    }

    /// <summary>
    ///     Returns the buffer that the parser rented for a scalar it had to decode.
    /// </summary>
    public void Dispose()
    {
        if (this._buffer is char[] buffer)
        {
            ArrayPool<char>.Shared.Return(buffer);
            this._buffer = null;
        }
    }

    #region Helpers

    private static bool IsBlank(char c) => c is ' ' or '\t';

    private static bool IsEndOrBlank(ReadOnlySpan<char> text, int index) => index >= text.Length || IsBlank(text[index]);

    // A document marker, which YAML reads at column 0 when a space, a tab or the end of the line follows it.
    private static bool IsMarker(ReadOnlySpan<char> line) =>
        (line.StartsWith("---") || line.StartsWith("...")) && IsEndOrBlank(line, 3);

    // The printable set of YAML 1.2.2, section 5.1, without the surrogates that pair up.
    private static bool IsPrintable(char c) =>
        c is '\t' or '\n' or '\r' or NextLine or (>= NoBreakSpace and <= BeforeSurrogates) or (>= AfterSurrogates and <= LastPrintable);

    private static int CountLineBreaks(ReadOnlySpan<char> text)
    {
        int count = 0;

        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n' || (text[index] == '\r' && (index + 1 == text.Length || text[index + 1] != '\n')))
                count++;
        }

        return count;
    }

    // The ':' that ends an implicit key, which a space, a tab or the end of the line follows, or -1.
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

    // The closing quote of a quoted scalar that opens at the start of the text, or -1 when the line does not hold it.
    private static int FindQuoteEnd(ReadOnlySpan<char> text)
    {
        char quote = text[0];

        for (int index = 1; index < text.Length; index++)
        {
            char c = text[index];

            if (c == '\\' && quote == '"')
                index++;
            else if (c == quote && quote == '\'' && index + 1 < text.Length && text[index + 1] == '\'')
                index++;
            else if (c == quote)
                return index;
        }

        return -1;
    }

    private void StartBlock()
    {
        this._isStarted = true;
        this.CheckCharacters();
        this.MoveToLine(0);

        ReadOnlySpan<char> first = this._block[..this._lineEnd];

        if (first.StartsWith("---") && first[3..].IndexOfAnyExcept(' ', '\t') < 0)
            this.AdvanceLine();
    }

    // Reads the value of the last key to its end, whatever the caller left of it. Only a value that the caller
    // never asked for may carry an anchor, a tag or an alias.
    private void SkipValue()
    {
        int index = this._valueIndex;

        if (index >= 0)
        {
            this._valueIndex = -1;
            this._isLenient = true;
            _ = this.ReadNode(index, 0, true);
        }

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

    private readonly void CheckCharacters()
    {
        ReadOnlySpan<char> block = this._block;
        int index = 0;

        while (true)
        {
            int offset = block[index..].IndexOfAnyExceptInRange(' ', '~');

            if (offset < 0)
                return;

            index += offset;
            char c = block[index];

            if (char.IsHighSurrogate(c) && index + 1 < block.Length && char.IsLowSurrogate(block[index + 1]))
            {
                index += 2;
                continue;
            }

            if (!IsPrintable(c))
                throw new FrontMatterException(CountLineBreaks(block[..index]) + 1, MalformedReasons.DescribeCharacter(c));

            index++;
        }
    }

    // Measures the current line. The indent counts its leading spaces, and the content is the offset of its first
    // character that is neither a space nor a tab.
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

    private void SkipIndentedLines(int parentIndent)
    {
        while (this.SkipToContent(out int indent, out _) is YamlLineKind.Content && indent > parentIndent)
            this.AdvanceLine();
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

    private readonly int SkipToken(int index)
    {
        int offset = this._block[index..this._lineEnd].IndexOfAny(' ', '\t');
        return offset < 0 ? this._lineEnd : index + offset;
    }

    private readonly bool IsEndOrBlank(int index) => index >= this._lineEnd || IsBlank(this._block[index]);

    private readonly bool IsEntry(int index) => this._block[index] == '-' && this.IsEndOrBlank(index + 1);

    // Only a comment may follow a value that ends within its line, and a blank separates the two.
    private void ExpectLineEnd(int index, string reason)
    {
        int next = this.SkipBlanks(index);

        if (next < this._lineEnd && (next == index || this._block[next] != '#'))
            throw new FrontMatterException(this._line, reason);

        this.AdvanceLine();
    }

    private readonly void CheckKeyStart(ReadOnlySpan<char> line)
    {
        char c = line[0];
        bool isAlone = IsEndOrBlank(line, 1);

        switch (c)
        {
            case '%':
            case '?' when isAlone:
                throw new FrontMatterException(this._line, MalformedReasons.NoDirectives);
            case '&' or '*' or '!':
                throw new FrontMatterException(this._line, MalformedReasons.NoProperties);
            case '[' or '{' or '|' or '>':
            case '-' or ':' when isAlone:
                throw new FrontMatterException(this._line, MalformedReasons.NotAKey);
            case ',' or ']' or '}' or '@' or '`':
                throw new FrontMatterException(this._line, MalformedReasons.DescribeStart(c, false));
        }
    }

    // Reads the node behind the ':' of a key or the '-' of an entry. It starts at the index, on the current
    // line, or on the lines below it.
    private YamlNodeKind ReadNode(int index, int parentIndent, bool isKeyValue)
    {
        int start = this.SkipBlanks(index);

        if (start == this._lineEnd || this._block[start] == '#')
        {
            this.AdvanceLine();
            return this.ReadNodeBelow(parentIndent, isKeyValue);
        }

        this._valueLine = this._line;

        if (!isKeyValue && (this.IsEntry(start) || this.StartsMapping(start)))
        {
            YamlNodeKind kind = this.IsEntry(start) ? YamlNodeKind.Sequence : YamlNodeKind.Mapping;
            this.AdvanceLine();
            this.SkipIndentedLines(parentIndent);

            return kind;
        }

        return this.ReadInlineNode(start, parentIndent, isKeyValue);
    }

    private YamlNodeKind ReadNodeBelow(int parentIndent, bool isKeyValue)
    {
        YamlLineKind kind = this.SkipToContent(out int indent, out int content);
        this._valueLine = this._line;

        if (kind is not YamlLineKind.Content)
            return YamlNodeKind.Null;

        int start = this._position + content;
        bool isEntry = this.IsEntry(start);

        if (indent > parentIndent)
        {
            bool isCollection = isEntry || this.StartsMapping(start);

            // A tab may separate the indentation from text, yet never from the '-' or the key of a collection.
            if (isCollection && content > indent)
                throw new FrontMatterException(this._line, MalformedReasons.TabIndents);

            if (isEntry && isKeyValue)
                return this.OpenBlockSequence(indent);

            if (isCollection)
            {
                this.SkipIndentedLines(parentIndent);
                return isEntry ? YamlNodeKind.Sequence : YamlNodeKind.Mapping;
            }

            return this.ReadInlineNode(start, parentIndent, isKeyValue);
        }

        // The entries of a sequence may sit at the column of the key that holds it, section 8.2.1.
        if (isEntry && isKeyValue && content == indent && indent == parentIndent)
            return this.OpenBlockSequence(indent);

        return YamlNodeKind.Null;
    }

    private YamlNodeKind ReadInlineNode(int index, int parentIndent, bool isKeyValue)
    {
        char c = this._block[index];

        // Only a value that the caller leaves unread may carry an anchor, a tag or an alias, which are skipped.
        while (c is '&' or '!')
        {
            if (!this._isLenient)
                throw new FrontMatterException(this._line, MalformedReasons.NoProperties);

            index = this.SkipBlanks(this.SkipToken(index));

            if (index == this._lineEnd || this._block[index] == '#')
            {
                this.AdvanceLine();
                return this.ReadNodeBelow(parentIndent, isKeyValue);
            }

            c = this._block[index];
        }

        switch (c)
        {
            case '"' or '\'':
                this._text = this.ReadQuotedScalar(ref index, parentIndent);
                this.ExpectLineEnd(index, MalformedReasons.AfterQuote);
                return YamlNodeKind.Scalar;

            case '|' or '>':
                this._text = this.ReadBlockScalar(index, parentIndent);
                return YamlNodeKind.Scalar;

            case '[' when isKeyValue:
                return this.OpenFlowSequence(index, parentIndent);

            case '[':
                this.SkipFlowSequence(ref index, parentIndent);
                this.ExpectLineEnd(index, MalformedReasons.AfterBracket);
                return YamlNodeKind.Sequence;

            case '{':
                this.SkipFlowCollection(ref index, parentIndent);
                this.ExpectLineEnd(index, MalformedReasons.AfterBracket);
                return YamlNodeKind.Mapping;

            case '*' when this._isLenient:
                this._text = default;
                this.ExpectLineEnd(this.SkipToken(index), MalformedReasons.AfterAlias);
                return YamlNodeKind.Scalar;

            case '*':
                throw new FrontMatterException(this._line, MalformedReasons.NoProperties);

            case ',' or ']' or '}' or '%' or '@' or '`':
                throw new FrontMatterException(this._line, MalformedReasons.DescribeStart(c, false));

            case '-' or '?' or ':' when this.IsEndOrBlank(index + 1):
                throw new FrontMatterException(this._line, MalformedReasons.DescribeStart(c, true));

            default:
                return this.ReadPlainScalar(index, parentIndent);
        }
    }

    // Tells whether the node at the index is a mapping, which is a key and its ':' on one line.
    private readonly bool StartsMapping(int index)
    {
        ReadOnlySpan<char> rest = this._block[index..this._lineEnd];
        char c = rest[0];

        // None of these starts a key, so the line is read as the value it tries to be.
        if (c is '[' or '{' or '|' or '>' or ',' or ']' or '}' or '%' or '@' or '`')
            return false;

        if (c == '?' && IsEndOrBlank(rest, 1))
            return true;

        if (c is not ('"' or '\''))
            return FindKeyEnd(rest) >= 0;

        int close = FindQuoteEnd(rest);

        if (close < 0)
            return false;

        ReadOnlySpan<char> tail = rest[(close + 1)..].TrimStart(Blanks);
        return tail.Length > 0 && tail[0] == ':' && IsEndOrBlank(tail, 1);
    }

    #endregion
}
