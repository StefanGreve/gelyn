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
    ///     Parses a front matter block, including its surrounding delimiters, and reports whether every key
    ///     it declares is one a member reads.
    /// </summary>
    /// <param name="block">
    ///     The raw text of the block, which starts at the top of the source file.
    /// </param>
    /// <param name="frontMatter">
    ///     When this method returns, the front matter the block declares, or <see langword="null"/> when the
    ///     block is empty. Set whichever value is returned, because an unknown key is skipped and does not
    ///     keep the other keys from being read. A key whose value is quoted but empty yields an empty string,
    ///     which is distinct from the key being absent.
    /// </param>
    /// <param name="unknownKeys">
    ///     When this method returns <see langword="false"/>, the keys the block declares that no member reads,
    ///     in the order declared; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when no member is left without the key meant for it; <see langword="false"/>
    ///     when the block declares at least one unknown key.
    /// </returns>
    /// <exception cref="YamlException">
    ///     The block is not well-formed YAML, or declares something other than a mapping.
    /// </exception>
    public static bool TryParse(
        ReadOnlySpan<char> block,
        out FrontMatter? frontMatter,
        [NotNullWhen(false)] out IReadOnlyList<UnknownKey>? unknownKeys)
    {
        List<UnknownKey> keys = [];

        YamlSerializerOptions options = YamlContext.Default.Options with
        {
            // Zero-based, and counted from the top of the file, because stripping
            // keeps the delimiter's line break.
            UnmappedMemberCallback = member => keys.Add(new UnknownKey
            {
                Name = member.Name,
                Line = member.KeyStart.Line + 1
            }),
        };

        frontMatter = YamlSerializer.Deserialize<FrontMatter>(StripDelimiters(block), options);
        unknownKeys = keys.Count > 0 ? keys : null;

        return unknownKeys is null;
    }

    #region Helpers

    // A delimiter would otherwise open a second YAML document, which Deserialize rejects outright. Markdig
    // closes the block at its second one, so the two it hands over are the first and the last line.
    private static string StripDelimiters(ReadOnlySpan<char> block) =>
        StringUtils.TrimDelimiters(block, Delimiter).ToString();

    #endregion
}
