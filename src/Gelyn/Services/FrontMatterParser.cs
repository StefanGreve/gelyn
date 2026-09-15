using System;
using System.Globalization;

using Gelyn.Internals;
using Gelyn.Model;

namespace Gelyn.Services;

/// <summary>
///     Reads the <c>key: value</c> pairs of a front matter block.
/// </summary>
/// <remarks>
///     Only the flat subset of YAML that front matter blocks actually use is supported. A full YAML parser
///     would pull in reflection-based deserialization, which Native AOT does not tolerate without a static
///     serialization context.
/// </remarks>
public static class FrontMatterParser
{
    /// <summary>
    ///     Parses a front matter block, including its surrounding delimiters.
    /// </summary>
    /// <param name="block">
    ///     The raw text of the block.
    /// </param>
    /// <returns>
    ///     The recognized front matter, or <see cref="FrontMatter.Empty"/> when none was found.
    /// </returns>
    public static FrontMatter Parse(ReadOnlySpan<char> block)
    {
        string? title = null;
        DateOnly? date = null;

        foreach (ReadOnlySpan<char> line in block.EnumerateLines())
        {
            if (!TryReadPair(line, out ReadOnlySpan<char> key, out ReadOnlySpan<char> value))
                continue;

            switch (key)
            {
                case FrontMatterConstants.Title:
                    title = value.IsEmpty ? null : value.ToString();
                    break;

                case FrontMatterConstants.Date:
                    if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, out DateOnly parsed))
                        date = parsed;
                    break;
            }
        }

        return title is null && date is null
            ? FrontMatter.Empty
            : new FrontMatter { Title = title, Date = date };
    }

    #region Helpers

    private static bool TryReadPair(ReadOnlySpan<char> line, out ReadOnlySpan<char> key, out ReadOnlySpan<char> value)
    {
        key = default;
        value = default;

        ReadOnlySpan<char> trimmed = line.Trim();

        if (trimmed.IsEmpty || trimmed.SequenceEqual(FrontMatterConstants.Delimiter))
            return false;

        int separator = trimmed.IndexOf(FrontMatterConstants.Separator);

        if (separator < 0)
            return false;

        key = trimmed[..separator].Trim();
        value = Unquote(trimmed[(separator + 1)..].Trim());

        return !key.IsEmpty;
    }

    // Quotes are only stripped as a matched pair, so an unquoted value may end in an apostrophe.
    private static ReadOnlySpan<char> Unquote(ReadOnlySpan<char> value)
    {
        if (value.Length < 2 || value[0] is not ('"' or '\''))
            return value;

        return value[^1] == value[0] ? value[1..^1] : value;
    }

    #endregion
}
