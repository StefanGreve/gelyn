using System.Buffers;

using Gelyn.Exceptions;

namespace Gelyn.Core.Yaml;

internal ref partial struct LeanYamlParser
{
    #region Collections

    private static readonly SearchValues<char> FlowStops = SearchValues.Create(",[]{}:#");

    private YamlNodeKind OpenBlockSequence(int column)
    {
        this._inBlockSequence = true;
        this._sequenceIndent = column;

        return YamlNodeKind.Sequence;
    }

    private YamlNodeKind OpenFlowSequence(int index, int parentIndent)
    {
        this._inFlowSequence = true;
        this._sequenceIndent = parentIndent;
        this._flowIndex = index + 1;
        this._flowLine = this._line;

        return YamlNodeKind.Sequence;
    }

    private bool ReadBlockEntry(int column, out YamlNodeKind kind)
    {
        kind = YamlNodeKind.Null;
        YamlLineKind line = this.SkipToContent(out int indent, out int content);

        if (line is not YamlLineKind.Content || indent < column)
            return false;

        if (indent > column)
            throw new FrontMatterException(this._line, MalformedReasons.ContinuesNothing);

        int start = this._position + indent;

        if (content != indent || !this.IsEntry(start))
            return false;

        kind = this.ReadNode(start + 1, column, false);
        return true;
    }

    private bool ReadFlowEntry(scoped ref int index, int parentIndent, int openLine, out YamlNodeKind kind)
    {
        kind = YamlNodeKind.Null;
        index = this.SkipFlowSpace(index, parentIndent, openLine);
        char c = this._block[index];

        if (c == ']')
        {
            index++;
            return false;
        }

        if (c == ',')
            throw new FrontMatterException(this._line, MalformedReasons.EmptyEntry);

        kind = this.ReadFlowItem(ref index, parentIndent, openLine);
        index = this.SkipFlowSpace(index, parentIndent, openLine);
        c = this._block[index];

        if (c == ',')
            index++;
        else if (c != ']')
            throw new FrontMatterException(this._line, MalformedReasons.NoSeparator);

        return true;
    }

    private void SkipFlowSequence(scoped ref int index, int parentIndent)
    {
        int line = this._line;
        index++;

        while (this.ReadFlowEntry(ref index, parentIndent, line, out _))
        {
            // An entry that nobody asked for is read for its syntax alone.
        }

        // The entries moved the line of the value on, which is the line the sequence opens on.
        this._valueLine = line;
    }

    private int SkipFlowSpace(int index, int parentIndent, int openLine)
    {
        int next = this.SkipBlanks(index);

        if (next < this._lineEnd && !(this._block[next] == '#' && (next > index || next == this._position || IsBlank(this._block[next - 1]))))
            return next;

        this.AdvanceLine();
        YamlLineKind kind = this.SkipToContent(out int indent, out int content);

        if (kind is not YamlLineKind.Content)
            throw new FrontMatterException(openLine, MalformedReasons.OpenBracket);

        if (indent <= parentIndent)
            throw new FrontMatterException(this._line, content > indent ? MalformedReasons.TabIndents : MalformedReasons.NotIndented);

        return this._position + content;
    }

    private YamlNodeKind ReadFlowItem(scoped ref int index, int parentIndent, int openLine)
    {
        this._valueLine = this._line;
        YamlNodeKind node = this.ReadFlowNode(ref index, parentIndent, openLine);
        int next = this.SkipBlanks(index);

        // A pair makes the entry a mapping of its own, section 7.4.1.
        if (next == this._lineEnd || this._block[next] != ':')
            return node;

        index = this.SkipFlowSpace(next + 1, parentIndent, openLine);

        if (this._block[index] is not (',' or ']'))
            _ = this.ReadFlowNode(ref index, parentIndent, openLine);

        return YamlNodeKind.Mapping;
    }

    private YamlNodeKind ReadFlowNode(scoped ref int index, int parentIndent, int openLine)
    {
        char c = this._block[index];

        while (c is '&' or '!')
        {
            if (!this._isLenient)
                throw new FrontMatterException(this._line, MalformedReasons.NoProperties);

            index = this.SkipFlowSpace(this.SkipFlowToken(index), parentIndent, openLine);
            c = this._block[index];
        }

        switch (c)
        {
            case '"' or '\'':
                this._text = this.ReadQuotedScalar(ref index, parentIndent);
                return YamlNodeKind.Scalar;

            case '[':
                this.SkipFlowCollection(ref index, parentIndent);
                return YamlNodeKind.Sequence;

            case '{':
                this.SkipFlowCollection(ref index, parentIndent);
                return YamlNodeKind.Mapping;

            case '*' when this._isLenient:
                this._text = default;
                index = this.SkipFlowToken(index);
                return YamlNodeKind.Scalar;

            case '*':
                throw new FrontMatterException(this._line, MalformedReasons.NoProperties);

            case ',' or ']':
                return YamlNodeKind.Null;

            case '}' or '%' or '@' or '`' or '|' or '>' or '#':
                throw new FrontMatterException(this._line, MalformedReasons.DescribeStart(c, false));

            case '-' or '?' or ':' when this.IsFlowEnd(index + 1):
                throw new FrontMatterException(this._line, MalformedReasons.DescribeStart(c, true));

            default:
                return this.ReadFlowPlainScalar(ref index, parentIndent, openLine);
        }
    }

    private readonly bool IsFlowEnd(int index) => this.IsEndOrBlank(index) || this._block[index] is ',' or ']' or '}';

    private readonly int SkipFlowToken(int index)
    {
        int offset = this._block[index..this._lineEnd].IndexOfAny(" \t,]}");
        return offset < 0 ? this._lineEnd : index + offset;
    }

    private YamlNodeKind ReadFlowPlainScalar(scoped ref int index, int parentIndent, int openLine)
    {
        int start = index;
        int end = this.ScanFlowLine(ref index, out bool isOpen);
        ReadOnlySpan<char> first = this._block[start..end];
        Span<char> buffer = default;
        int length = 0;

        while (isOpen)
        {
            int breaks = 0;
            YamlLineKind kind;
            int indent;
            int content;

            this.AdvanceLine();

            while ((kind = this.ClassifyLine(out indent, out content)) is YamlLineKind.Blank)
            {
                breaks++;
                this.AdvanceLine();
            }

            if (kind is YamlLineKind.Comment)
            {
                index = this._lineEnd;
                break;
            }

            if (kind is not YamlLineKind.Content)
                throw new FrontMatterException(openLine, MalformedReasons.OpenBracket);

            if (indent <= parentIndent)
                throw new FrontMatterException(this._line, content > indent ? MalformedReasons.TabIndents : MalformedReasons.NotIndented);

            index = this._position + content;

            if (FlowStops.Contains(this._block[index]) && (this._block[index] != ':' || this.IsFlowEnd(index + 1)))
                break;

            if (length == 0)
            {
                buffer = this.RentBuffer();
                first.CopyTo(buffer);
                length = first.Length;
            }

            length = FoldBreaks(buffer, length, breaks);
            start = index;
            end = this.ScanFlowLine(ref index, out isOpen);
            this._block[start..end].CopyTo(buffer[length..]);
            length += end - start;
        }

        if (length > 0)
        {
            this._text = buffer[..length];
            return YamlNodeKind.Scalar;
        }

        this._text = first;
        return IsNull(first) ? YamlNodeKind.Null : YamlNodeKind.Scalar;
    }

    private readonly int ScanFlowLine(scoped ref int index, out bool isOpen)
    {
        ReadOnlySpan<char> rest = this._block[index..this._lineEnd];
        int offset = 0;
        isOpen = false;

        while (true)
        {
            int found = rest[offset..].IndexOfAny(FlowStops);

            if (found < 0)
            {
                offset = rest.Length;
                isOpen = true;
                break;
            }

            offset += found;
            char c = rest[offset];

            if (c == ':' ? this.IsFlowEnd(index + offset + 1) : c != '#' || (offset > 0 && IsBlank(rest[offset - 1])))
                break;

            offset++;
        }

        int end = index + rest[..offset].TrimEnd(Blanks).Length;
        index += offset;

        return end;
    }

    // A quote opens a scalar only where a node starts, so that an apostrophe inside plain text stays text.
    private void SkipFlowCollection(scoped ref int index, int parentIndent)
    {
        int line = this._line;
        int depth = 0;
        bool isStart = false;

        do
        {
            index = this.SkipFlowSpace(index, parentIndent, line);
            char c = this._block[index];

            if (isStart && c is '"' or '\'')
            {
                _ = this.ReadQuotedScalar(ref index, parentIndent);
                isStart = false;
                continue;
            }

            // An anchor or a tag stands before its node, which may be a quoted scalar, section 6.9.
            if (isStart && c is '&' or '!')
            {
                index = this.SkipFlowToken(index);
                continue;
            }

            index++;
            isStart = c is '[' or '{' or ',' || (c == ':' && this.IsFlowEnd(index));

            if (c is '[' or '{')
                depth++;
            else if (c is ']' or '}')
                depth--;
        }
        while (depth > 0);
    }

    #endregion
}
