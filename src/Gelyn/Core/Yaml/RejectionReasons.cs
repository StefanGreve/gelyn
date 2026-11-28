using Gelyn.Core.Internals;

namespace Gelyn.Core.Yaml;

/// <summary>
///     The reasons a member rejects the value declared for it, each a clause that completes "because".
/// </summary>
internal static class RejectionReasons
{
    /// <summary>
    ///     A list is declared where text is expected.
    /// </summary>
    internal const string ListAsText = "a list is not text";

    /// <summary>
    ///     A list is declared where a date is expected.
    /// </summary>
    internal const string ListAsDate = $"a list is not a date written as {DateFormat.Machine}";

    /// <summary>
    ///     A list is declared where a boolean is expected.
    /// </summary>
    internal const string ListAsBoolean = "a list is not true or false";

    /// <summary>
    ///     Describes text that is no date.
    /// </summary>
    /// <param name="text">
    ///     The text that is declared.
    /// </param>
    /// <returns>
    ///     The reason, which quotes the text.
    /// </returns>
    internal static string DescribeDate(ReadOnlySpan<char> text) => $"'{text}' is not a date written as {DateFormat.Machine}";

    /// <summary>
    ///     Describes text that is no boolean.
    /// </summary>
    /// <param name="text">
    ///     The text that is declared.
    /// </param>
    /// <returns>
    ///     The reason, which quotes the text.
    /// </returns>
    internal static string DescribeBoolean(ReadOnlySpan<char> text) => $"'{text}' is not true or false";

    /// <summary>
    ///     Describes text that is declared where a list is expected.
    /// </summary>
    /// <param name="text">
    ///     The text that is declared.
    /// </param>
    /// <returns>
    ///     The reason, which quotes the text.
    /// </returns>
    internal static string DescribeList(ReadOnlySpan<char> text) => $"'{text}' is not a list";
}
