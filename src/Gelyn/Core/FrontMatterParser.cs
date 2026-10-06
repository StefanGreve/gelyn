using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Gelyn.Internals;
using Gelyn.Model;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Gelyn.Core;

/// <summary>
///     Reads a front matter block.
/// </summary>
public static class FrontMatterParser
{
    private const string Delimiter = "---";

    private static readonly IDeserializer YamlDeserializer = new StaticDeserializerBuilder(new YamlContext())
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new DateOnlyConverter())
        .IgnoreUnmatchedProperties()
        .Build();

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
    [SuppressMessage("Maintainability", "CA2263:Prefer generic overload", Justification = Justifications.ByDesign)]
    public static FrontMatter? Parse(ReadOnlySpan<char> block)
    {
        return YamlDeserializer.Deserialize(StripDelimiters(block), typeof(FrontMatter)) switch
        {
            null => null,
            FrontMatter frontMatter => frontMatter,
            _ => throw new YamlException("Front matter must be a mapping of key-value pairs."),
        };
    }

    #region Helpers

    // A delimiter line would otherwise open a second YAML document, which Deserialize rejects outright.
    private static string StripDelimiters(ReadOnlySpan<char> block)
    {
        StringBuilder body = new(block.Length);

        foreach (ReadOnlySpan<char> line in block.EnumerateLines())
        {
            if (line.Trim().SequenceEqual(Delimiter)) continue;

            body.Append(line).AppendLine();
        }

        return body.ToString();
    }

    #endregion
}
