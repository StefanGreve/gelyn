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
            ReadOnlySpan<char> trimmed = line.Trim();

            if (trimmed.IsEmpty || trimmed.SequenceEqual(FrontMatterConstants.Delimiter))
                continue;

            int separator = trimmed.IndexOf(':');

            if (separator < 0)
                continue;

            ReadOnlySpan<char> key = trimmed[..separator].Trim();
            ReadOnlySpan<char> value = trimmed[(separator + 1)..].Trim().Trim('"').Trim('\'');

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
}
