using System.Globalization;

using Gelyn.Core.Internals;
using Gelyn.Exceptions;
using Gelyn.Model;

namespace Gelyn.Core.Yaml;

/// <summary>
///     Reads a front matter block.
/// </summary>
internal static class FrontMatterParser
{
    // Indexed by FrontMatterKey.
    private static readonly string[] KeyNames =
    [
        string.Empty,
        "title",
        "date",
        "revised",
        "description",
        "enableMathRendering",
        "enableDiagramRendering",
        "enableSyntaxHighlighting",
        "enableComments",
        "enableFeed",
        "stylesheets",
        "scripts",
    ];

    /// <summary>
    ///     Parses a front matter block.
    /// </summary>
    /// <param name="block">
    ///     The raw text of the block, with or without its delimiters. Its first line counts as line 1.
    /// </param>
    /// <param name="unknownKeys">
    ///     When this method returns, the keys the block declares that no member reads, in the order declared.
    /// </param>
    /// <param name="invalidValues">
    ///     When this method returns, the values the block declares that the member meant for them rejects, in
    ///     the order declared. The member keeps its default.
    /// </param>
    /// <returns>
    ///     The front matter the block declares, or <see langword="null"/> when the block declares no key. An
    ///     unknown key or a rejected value does not keep the other keys from being read. A value that is quoted
    ///     but empty yields an empty string, which is distinct from the key being absent.
    /// </returns>
    /// <exception cref="FrontMatterException">
    ///     The block is malformed, uses YAML that front matter does not support, or declares a key twice that a
    ///     member reads.
    /// </exception>
    public static FrontMatter? Parse(ReadOnlySpan<char> block, out IReadOnlyList<UnknownKey> unknownKeys, out IReadOnlyList<InvalidValue> invalidValues)
    {
        FrontMatter? frontMatter = null;
        List<UnknownKey>? unknown = null;
        List<InvalidValue>? invalid = null;
        int declared = 0;
        var parser = new LeanYamlParser(block);

        while (parser.ReadKey(out ReadOnlySpan<char> name))
        {
            frontMatter ??= new FrontMatter();
            FrontMatterKey key = MatchKey(name);

            if (key is FrontMatterKey.Unknown)
            {
                (unknown ??= []).Add(new UnknownKey { Name = name.ToString(), Line = parser.KeyLine });
                continue;
            }

            int bit = 1 << (int)key;

            if ((declared & bit) != 0)
                throw new FrontMatterException(parser.KeyLine, MalformedReasons.DescribeDuplicate(KeyNames[(int)key]));

            declared |= bit;
            ReadMember(ref parser, frontMatter, key, ref invalid);
        }

        unknownKeys = (IReadOnlyList<UnknownKey>?)unknown ?? [];
        invalidValues = (IReadOnlyList<InvalidValue>?)invalid ?? [];

        return frontMatter;
    }

    #region Helpers

    private static FrontMatterKey MatchKey(ReadOnlySpan<char> name) => name switch
    {
        "title" => FrontMatterKey.Title,
        "date" => FrontMatterKey.Date,
        "revised" => FrontMatterKey.Revised,
        "description" => FrontMatterKey.Description,
        "enableMathRendering" => FrontMatterKey.EnableMathRendering,
        "enableDiagramRendering" => FrontMatterKey.EnableDiagramRendering,
        "enableSyntaxHighlighting" => FrontMatterKey.EnableSyntaxHighlighting,
        "enableComments" => FrontMatterKey.EnableComments,
        "enableFeed" => FrontMatterKey.EnableFeed,
        "stylesheets" => FrontMatterKey.Stylesheets,
        "scripts" => FrontMatterKey.Scripts,
        _ => FrontMatterKey.Unknown,
    };

    private static void RejectValue(ref List<InvalidValue>? invalid, string name, int line, string message) =>
        (invalid ??= []).Add(new InvalidValue { Name = name, Line = line, Message = message });

    private static void ReadMember(ref LeanYamlParser parser, FrontMatter frontMatter, FrontMatterKey key, ref List<InvalidValue>? invalid)
    {
        string name = KeyNames[(int)key];

        switch (key)
        {
            case FrontMatterKey.Title:
                frontMatter.Title = ReadText(ref parser, name, ref invalid);
                break;
            case FrontMatterKey.Date:
                frontMatter.Date = ReadDate(ref parser, name, ref invalid);
                break;
            case FrontMatterKey.Revised:
                frontMatter.Revised = ReadDate(ref parser, name, ref invalid);
                break;
            case FrontMatterKey.Description:
                frontMatter.Description = ReadText(ref parser, name, ref invalid);
                break;
            case FrontMatterKey.EnableMathRendering:
                frontMatter.EnableMathRendering = ReadBoolean(ref parser, name, ref invalid);
                break;
            case FrontMatterKey.EnableDiagramRendering:
                frontMatter.EnableDiagramRendering = ReadBoolean(ref parser, name, ref invalid);
                break;
            case FrontMatterKey.EnableSyntaxHighlighting:
                frontMatter.EnableSyntaxHighlighting = ReadBoolean(ref parser, name, ref invalid);
                break;
            case FrontMatterKey.EnableComments:
                frontMatter.EnableComments = ReadBoolean(ref parser, name, ref invalid);
                break;
            case FrontMatterKey.EnableFeed:
                frontMatter.EnableFeed = ReadBoolean(ref parser, name, ref invalid);
                break;
            case FrontMatterKey.Stylesheets:
                frontMatter.Stylesheets = ReadList(ref parser, name, ref invalid);
                break;
            default:
                frontMatter.Scripts = ReadList(ref parser, name, ref invalid);
                break;
        }
    }

    private static string? ReadText(ref LeanYamlParser parser, string name, ref List<InvalidValue>? invalid)
    {
        switch (parser.ReadValue())
        {
            case YamlNodeKind.Scalar:
                return parser.Text.ToString();
            case YamlNodeKind.Sequence:
                RejectValue(ref invalid, name, parser.ValueLine, RejectionReasons.ListAsText);
                return null;
            default:
                return null;
        }
    }

    private static DateOnly? ReadDate(ref LeanYamlParser parser, string name, ref List<InvalidValue>? invalid)
    {
        switch (parser.ReadValue())
        {
            case YamlNodeKind.Scalar:
                if (DateOnly.TryParseExact(parser.Text, DateFormat.Machine, Tool.Culture, DateTimeStyles.None, out DateOnly date))
                    return date;

                RejectValue(ref invalid, name, parser.ValueLine, RejectionReasons.DescribeDate(parser.Text));
                return null;
            case YamlNodeKind.Sequence:
                RejectValue(ref invalid, name, parser.ValueLine, RejectionReasons.ListAsDate);
                return null;
            default:
                return null;
        }
    }

    // The spellings of the core schema, section 10.3.2 of YAML 1.2.2.
    private static bool? ReadBoolean(ref LeanYamlParser parser, string name, ref List<InvalidValue>? invalid)
    {
        switch (parser.ReadValue())
        {
            case YamlNodeKind.Scalar when parser.Text is "true" or "True" or "TRUE":
                return true;
            case YamlNodeKind.Scalar when parser.Text is "false" or "False" or "FALSE":
                return false;
            case YamlNodeKind.Scalar:
                RejectValue(ref invalid, name, parser.ValueLine, RejectionReasons.DescribeBoolean(parser.Text));
                return null;
            case YamlNodeKind.Sequence:
                RejectValue(ref invalid, name, parser.ValueLine, RejectionReasons.ListAsBoolean);
                return null;
            default:
                return null;
        }
    }

    private static List<string?>? ReadList(ref LeanYamlParser parser, string name, ref List<InvalidValue>? invalid)
    {
        switch (parser.ReadValue())
        {
            case YamlNodeKind.Sequence:
                break;
            case YamlNodeKind.Scalar:
                RejectValue(ref invalid, name, parser.ValueLine, RejectionReasons.DescribeList(parser.Text));
                return null;
            default:
                return null;
        }

        List<string?> entries = [];

        while (parser.ReadEntry(out YamlNodeKind kind))
            entries.Add(kind is YamlNodeKind.Scalar ? parser.Text.ToString() : null);

        return entries;
    }

    #endregion
}
