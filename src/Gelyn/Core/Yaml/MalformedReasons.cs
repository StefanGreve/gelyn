using Gelyn.Core.Internals;

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
    ///     A line that continues a quoted scalar or a flow collection starts at the first column.
    /// </summary>
    internal const string NotIndented = "it continues a value but is not indented";

    /// <summary>
    ///     An indented line follows a value that it cannot continue.
    /// </summary>
    internal const string ContinuesNothing = "it is indented but continues no value";

    /// <summary>
    ///     An anchor, an alias or a tag sits on a key, or on a value that is read.
    /// </summary>
    internal const string NoProperties = "front matter supports no anchors, aliases or tags";

    /// <summary>
    ///     A directive or a complex key sits at the first column.
    /// </summary>
    internal const string NoDirectives = "front matter supports no directives or complex keys";

    /// <summary>
    ///     A document marker carries text, or text follows the closing delimiter.
    /// </summary>
    internal const string SecondDocument = "a second document starts on it";

    /// <summary>
    ///     A colon that a space or the end of the line follows sits inside plain text.
    /// </summary>
    internal const string MappingInText = "': ' inside plain text starts a mapping, so quote the text";

    /// <summary>
    ///     A quoted scalar is never closed.
    /// </summary>
    internal const string OpenQuote = "the quote that opens on it is never closed";

    /// <summary>
    ///     A flow collection is never closed.
    /// </summary>
    internal const string OpenBracket = "the bracket that opens on it is never closed";

    /// <summary>
    ///     Text other than a comment follows a quoted scalar.
    /// </summary>
    internal const string AfterQuote = "text follows the closing quote";

    /// <summary>
    ///     Text other than a comment follows a flow collection.
    /// </summary>
    internal const string AfterBracket = "text follows the closing bracket";

    /// <summary>
    ///     Text other than a comment follows an alias.
    /// </summary>
    internal const string AfterAlias = "text follows the alias";

    /// <summary>
    ///     Text other than a comment follows the indicators of a block scalar.
    /// </summary>
    internal const string AfterIndicator = "text follows the block indicator";

    /// <summary>
    ///     Two commas of a flow sequence have no entry between them.
    /// </summary>
    internal const string EmptyEntry = "the list holds an empty entry";

    /// <summary>
    ///     Two entries of a flow sequence have no comma between them.
    /// </summary>
    internal const string NoSeparator = "an entry of the list is followed by neither ',' nor ']'";

    /// <summary>
    ///     A blank line that leads a block scalar holds more spaces than the line below it.
    /// </summary>
    internal const string BlankTooWide = "it is indented further than the text below it";

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
    ///     Describes an escape of a double-quoted scalar that YAML does not define.
    /// </summary>
    /// <param name="escape">
    ///     The character behind the backslash.
    /// </param>
    /// <returns>
    ///     The reason, which names the escape.
    /// </returns>
    internal static string DescribeEscape(char escape) => $"'\\{escape}' is not an escape";

    /// <summary>
    ///     Describes an escape that the hexadecimal digits of a character do not follow.
    /// </summary>
    /// <param name="escape">
    ///     The character behind the backslash.
    /// </param>
    /// <param name="digits">
    ///     The number of digits the escape takes.
    /// </param>
    /// <returns>
    ///     The reason, which names the escape and the number of digits.
    /// </returns>
    internal static string DescribeEscape(char escape, int digits) =>
        string.Create(Tool.Culture, $"'\\{escape}' is not followed by the {digits} hexadecimal digits of a character");

    /// <summary>
    ///     Describes a character outside the printable set of YAML.
    /// </summary>
    /// <param name="character">
    ///     The character.
    /// </param>
    /// <returns>
    ///     The reason, which names the code point of the character.
    /// </returns>
    internal static string DescribeCharacter(char character) =>
        string.Create(Tool.Culture, $"it holds the character U+{(int)character:X4}, which YAML does not allow");
}
