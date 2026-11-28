namespace Gelyn.Core.Yaml;

/// <summary>
///     The reasons a front matter block is malformed, each a clause that completes "because" and names the line
///     at fault as "it".
/// </summary>
internal static class MalformedReasons
{
    /// <summary>
    ///     A tab sits where spaces have to indent the line.
    /// </summary>
    internal const string TabIndents = "a tab indents it";

    /// <summary>
    ///     A line at the first column is no key and its colon.
    /// </summary>
    internal const string NotAKey = "it is not a key followed by ': '";

    /// <summary>
    ///     A key is written in quotes.
    /// </summary>
    internal const string QuotedKey = "a key is written without quotes";

    /// <summary>
    ///     A line is indented without being an entry of a list.
    /// </summary>
    internal const string Indented = "it is indented, and a value ends with its line";

    /// <summary>
    ///     A value is written as block text, with <c>|</c> or <c>&gt;</c>.
    /// </summary>
    internal const string BlockText = "front matter holds no block text, so write the text on one line";

    /// <summary>
    ///     An anchor, an alias or a tag sits on a key or on a value.
    /// </summary>
    internal const string NoProperties = "front matter supports no anchors, aliases or tags";

    /// <summary>
    ///     A document marker carries text, or text follows the closing delimiter.
    /// </summary>
    internal const string SecondDocument = "a second document starts on it";

    /// <summary>
    ///     A colon that a blank or the end of the line follows sits inside plain text.
    /// </summary>
    internal const string MappingInText = "': ' inside plain text starts a mapping, so quote the text";

    /// <summary>
    ///     A quoted scalar is not closed on the line it opens on.
    /// </summary>
    internal const string OpenQuote = "the quote that opens on it is not closed on the same line";

    /// <summary>
    ///     A list in brackets is not closed on the line it opens on.
    /// </summary>
    internal const string OpenBracket = "the bracket that opens on it is not closed on the same line";

    /// <summary>
    ///     Text other than a comment follows a quoted scalar.
    /// </summary>
    internal const string AfterQuote = "text follows the closing quote";

    /// <summary>
    ///     Text other than a comment follows a list in brackets.
    /// </summary>
    internal const string AfterBracket = "text follows the closing bracket";

    /// <summary>
    ///     Two commas of a list in brackets have no entry between them.
    /// </summary>
    internal const string EmptyEntry = "the list holds an empty entry";

    /// <summary>
    ///     Two entries of a list in brackets have no comma between them.
    /// </summary>
    internal const string NoSeparator = "an entry of the list is followed by neither ',' nor ']'";

    /// <summary>
    ///     Describes plain text that starts with an indicator of YAML.
    /// </summary>
    /// <param name="indicator">
    ///     The indicator the text starts with.
    /// </param>
    /// <param name="withSpace">
    ///     <see langword="true"/> when the character is an indicator only because a space or the end of the line
    ///     follows it.
    /// </param>
    /// <returns>
    ///     The reason, which names the indicator.
    /// </returns>
    internal static string DescribeStart(char indicator, bool withSpace) =>
        withSpace ? $"plain text cannot start with '{indicator} ', so quote it" : $"plain text cannot start with '{indicator}', so quote it";

    /// <summary>
    ///     Describes a recognized key that a block declares a second time.
    /// </summary>
    /// <param name="key">
    ///     The key.
    /// </param>
    /// <returns>
    ///     The reason, which names the key.
    /// </returns>
    internal static string DescribeDuplicate(string key) => $"'{key}' is declared twice";

    /// <summary>
    ///     Describes a backslash in double quotes that stands before neither a double quote nor a backslash.
    /// </summary>
    /// <param name="escape">
    ///     The character behind the backslash.
    /// </param>
    /// <returns>
    ///     The reason, which names the escape and the two that front matter reads.
    /// </returns>
    internal static string DescribeEscape(char escape) => $"'\\{escape}' is not one of the two escapes, '\\\\' and '\\\"'";
}
