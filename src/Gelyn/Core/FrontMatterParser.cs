using Gelyn.Core.Internals;
using Gelyn.Model;

using SharpYaml;

namespace Gelyn.Core;

/// <summary>
///     Reads a front matter block.
/// </summary>
internal static class FrontMatterParser
{
    private const string Delimiter = "---";

    /// <summary>
    ///     Parses a front matter block, including its surrounding delimiters.
    /// </summary>
    /// <param name="block">
    ///     The raw text of the block.
    /// </param>
    /// <returns>
    ///     The front matter the block declares, or <see langword="null"/> when the block is empty. A key whose
    ///     value is quoted but empty yields an empty string, which is distinct from the key being absent.
    /// </returns>
    /// <exception cref="YamlException">
    ///     The block is not well-formed YAML, or declares something other than a mapping.
    /// </exception>
    public static FrontMatter? Parse(ReadOnlySpan<char> block) =>
        YamlSerializer.Deserialize(StripDelimiters(block), YamlContext.Default.FrontMatter);

    #region Helpers

    // A delimiter would otherwise open a second YAML document, which Deserialize rejects outright. Markdig
    // closes the block at its second one, so the two it hands over are the first and the last line.
    private static string StripDelimiters(ReadOnlySpan<char> block) =>
        StringUtils.TrimDelimiters(block, Delimiter).ToString();

    #endregion
}
