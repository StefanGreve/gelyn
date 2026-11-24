using Gelyn.Model;

using SharpYaml;

namespace Gelyn.Core;

/// <summary>
///     Reads a front matter block.
/// </summary>
internal static class FrontMatterParser
{
    /// <summary>
    ///     Parses a front matter block, including its surrounding delimiters, and reports whether every key
    ///     it declares is one a member reads and every value is one its member accepts.
    /// </summary>
    /// <param name="block">
    ///     The raw text of the block, which starts at the top of the source file.
    /// </param>
    /// <param name="frontMatter">
    ///     When this method returns, the front matter the block declares, or <see langword="null"/> when the
    ///     block is empty. Set whichever value is returned, because an unknown key or a rejected value is
    ///     skipped and does not keep the other keys from being read. A key whose value is quoted but empty
    ///     yields an empty string, which is distinct from the key being absent.
    /// </param>
    /// <param name="unknownKeys">
    ///     When this method returns, the keys the block declares that no member reads, in the order declared.
    /// </param>
    /// <param name="invalidValues">
    ///     When this method returns, the values the block declares that the member meant for them rejects, in
    ///     the order declared. The member keeps its default.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when no member is left without the key meant for it and every value was
    ///     accepted; <see langword="false"/> when the block declares at least one unknown key or rejected value.
    /// </returns>
    /// <exception cref="YamlException">
    ///     The block is not well-formed YAML, or declares something other than a mapping.
    /// </exception>
    public static bool TryParse(
        ReadOnlySpan<char> block,
        out FrontMatter? frontMatter,
        out IReadOnlyList<UnknownKey> unknownKeys,
        out IReadOnlyList<InvalidValue> invalidValues)
    {
        List<UnknownKey> keys = [];
        List<InvalidValue> values = [];

        YamlSerializerOptions options = YamlContext.Default.Options with
        {
            // Zero-based, and counted from the top of the file, where the block starts.
            UnmappedMemberCallback = member => keys.Add(new UnknownKey
            {
                Name = member.Name,
                Line = member.KeyStart.Line + 1
            }),
            InvalidValueHandling = YamlInvalidValueHandling.Skip,
            InvalidValueCallback = value => values.Add(new InvalidValue
            {
                Name = value.Name,
                Line = value.ValueStart.Line + 1,
                Message = value.Message
            }),
        };

        frontMatter = YamlSerializer.Deserialize<FrontMatter>(block.ToString(), options);
        unknownKeys = keys;
        invalidValues = values;

        return keys.Count == 0 && values.Count == 0;
    }
}
