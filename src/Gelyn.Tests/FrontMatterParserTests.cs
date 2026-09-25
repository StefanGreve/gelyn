using System;
using System.Globalization;
using System.Threading.Tasks;

using Gelyn.Model;
using Gelyn.Services;

namespace Gelyn.Tests;

/// <summary>
///     Unit tests for the <see cref="FrontMatterParser"/> class.
/// </summary>
public partial class FrontMatterParserTests
{
    #region Parse Tests

    /// <summary>
    ///     Verifies that an empty block yields no front matter.
    /// </summary>
    [Test]
    public async Task Parse_Test_EmptyBlock_ReturnsEmpty()
    {
        // Arrange
        const string block = "";

        // Act
        FrontMatter result = FrontMatterParser.Parse(block);

        // Assert
        await Assert.That(result).IsSameReferenceAs(FrontMatter.Empty);
    }

    /// <summary>
    ///     Verifies that a block containing nothing but its delimiters yields no front matter.
    /// </summary>
    [Test]
    public async Task Parse_Test_DelimitersOnly_ReturnsEmpty()
    {
        // Arrange
        const string block = """
            ---
            ---
        """;

        // Act
        FrontMatter result = FrontMatterParser.Parse(block);

        // Assert
        await Assert.That(result).IsSameReferenceAs(FrontMatter.Empty);
    }

    /// <summary>
    ///     Verifies that keys outside the recognized vocabulary are ignored.
    /// </summary>
    [Test]
    public async Task Parse_Test_UnrecognizedKeys_ReturnsEmpty()
    {
        // Arrange
        const string block = """
            ---
            author: Stefan
            draft: true
            ---
        """;

        // Act
        FrontMatter result = FrontMatterParser.Parse(block);

        // Assert
        await Assert.That(result).IsSameReferenceAs(FrontMatter.Empty);
    }

    /// <summary>
    ///     Verifies that both recognized keys are read from a single block.
    /// </summary>
    [Test]
    public async Task Parse_Test_TitleAndDate_ReturnsBothValues()
    {
        // Arrange
        const string block = """
            ---
            title: Home
            date: 2026-09-13
            ---
        """;

        // Act
        FrontMatter result = FrontMatterParser.Parse(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Title).IsEqualTo("Home");
            await Assert.That(result.Date).IsEqualTo(new DateOnly(2026, 9, 13));
        }
    }

    /// <summary>
    ///     Verifies that a block supplying only one key leaves the other unset.
    /// </summary>
    [Test]
    public async Task Parse_Test_TitleOnly_LeavesDateUnset()
    {
        // Arrange
        const string block = """
            ---
            title: Home
            ---
        """;

        // Act
        FrontMatter result = FrontMatterParser.Parse(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Title).IsEqualTo("Home");
            await Assert.That(result.Date).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that surrounding whitespace is stripped from both the key and the value.
    /// </summary>
    /// <param name="line">
    ///     A front matter line whose key and value are padded with whitespace.
    /// </param>
    [Test]
    [Arguments("title: Home")]
    [Arguments("title:Home")]
    [Arguments("   title   :   Home   ")]
    [Arguments("\ttitle:\tHome\t")]
    public async Task Parse_Test_SurroundingWhitespace_IsTrimmed(string line)
    {
        // Arrange

        // Act
        FrontMatter result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Home");
    }

    /// <summary>
    ///     Verifies that quotes delimiting a value are stripped when they form a matched pair.
    /// </summary>
    /// <param name="value">
    ///     The raw value as it appears after the separator.
    /// </param>
    /// <param name="expected">
    ///     The expected title once the delimiters have been removed.
    /// </param>
    [Test]
    [Arguments("\"Quoted Title\"", "Quoted Title")]
    [Arguments("'Single Quoted'", "Single Quoted")]
    public async Task Parse_Test_MatchedQuotes_AreStripped(string value, string expected)
    {
        // Arrange
        string line = $"title: {value}";

        // Act
        FrontMatter result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result.Title).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that quote characters which do not form a matched pair are part of the value.
    /// </summary>
    /// <remarks>
    ///     Only a leading quote closed by the same character at the end is treated as a delimiter.
    ///     An earlier implementation trimmed both quote characters independently, which silently
    ///     truncated any unquoted value ending in an apostrophe: <c>title: Rangers'</c> was read as
    ///     <c>Rangers</c>. These cases guard that regression.
    /// </remarks>
    /// <param name="value">
    ///     The raw value as it appears after the separator.
    /// </param>
    /// <param name="expected">
    ///     The expected title, which retains every quote character verbatim.
    /// </param>
    [Test]
    [Arguments("Rangers'", "Rangers'")]
    [Arguments("\"Mismatched'", "\"Mismatched'")]
    [Arguments("'Mismatched\"", "'Mismatched\"")]
    [Arguments("It's fine", "It's fine")]
    [Arguments("\"", "\"")]
    public async Task Parse_Test_UnmatchedQuotes_AreKeptLiteral(string value, string expected)
    {
        // Arrange
        string line = $"title: {value}";

        // Act
        FrontMatter result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result.Title).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that only the first separator splits the line, so later ones stay in the value.
    /// </summary>
    [Test]
    public async Task Parse_Test_SeparatorInValue_IsKeptLiteral()
    {
        // Arrange
        const string line = "title: Gelyn: a static site generator";

        // Act
        FrontMatter result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Gelyn: a static site generator");
    }

    /// <summary>
    ///     Verifies that a key with no value is treated as absent rather than as an empty title.
    /// </summary>
    /// <param name="line">
    ///     A front matter line whose value is missing, blank, or an empty quoted string.
    /// </param>
    [Test]
    [Arguments("title:")]
    [Arguments("title:   ")]
    [Arguments("title: \"\"")]
    [Arguments("title: ''")]
    public async Task Parse_Test_EmptyValue_ReturnsEmpty(string line)
    {
        // Arrange

        // Act
        FrontMatter result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result).IsSameReferenceAs(FrontMatter.Empty);
    }

    /// <summary>
    ///     Verifies that key matching is ordinal, so keys differing in case are not recognized.
    /// </summary>
    /// <param name="line">
    ///     A front matter line whose key differs from the recognized spelling only in case.
    /// </param>
    [Test]
    [Arguments("Title: Home")]
    [Arguments("TITLE: Home")]
    public async Task Parse_Test_KeyCaseMismatch_ReturnsEmpty(string line)
    {
        // Arrange

        // Act
        FrontMatter result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result).IsSameReferenceAs(FrontMatter.Empty);
    }

    /// <summary>
    ///     Verifies that a repeated key is resolved to its last occurrence.
    /// </summary>
    [Test]
    public async Task Parse_Test_DuplicateKey_LastOccurrenceWins()
    {
        // Arrange
        const string block = """
            ---
            title: First
            title: Second
            ---
        """;

        // Act
        FrontMatter result = FrontMatterParser.Parse(block);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Second");
    }

    /// <summary>
    ///     Verifies that lines carrying no separator, or an empty key, are skipped rather than
    ///     aborting the block.
    /// </summary>
    [Test]
    public async Task Parse_Test_MalformedLines_AreSkipped()
    {
        // Arrange
        const string block = """
            ---
            this line has no separator

            : orphaned value
            title: Home
            ---
        """;

        // Act
        FrontMatter result = FrontMatterParser.Parse(block);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Home");
    }

    /// <summary>
    ///     Verifies that blocks are read line by line regardless of the newline convention used.
    /// </summary>
    /// <param name="block">
    ///     A front matter block delimited by line feeds, carriage returns, or both.
    /// </param>
    [Test]
    [Arguments("---\ntitle: Home\n---")]
    [Arguments("---\r\ntitle: Home\r\n---")]
    [Arguments("---\rtitle: Home\r---")]
    public async Task Parse_Test_NewlineConventions_AreAllSupported(string block)
    {
        // Arrange

        // Act
        FrontMatter result = FrontMatterParser.Parse(block);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Home");
    }

    /// <summary>
    ///     Verifies that dates are parsed with the invariant culture, which accepts both the
    ///     ISO 8601 form and the month-first form.
    /// </summary>
    /// <param name="value">
    ///     A date literal the invariant culture recognizes.
    /// </param>
    [Test]
    [Arguments("2026-09-13")]
    [Arguments("09/13/2026")]
    public async Task Parse_Test_InvariantCultureDate_ReturnsDate(string value)
    {
        // Arrange
        string line = $"date: {value}";

        // Act
        FrontMatter result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result.Date).IsEqualTo(new DateOnly(2026, 9, 13));
    }

    /// <summary>
    ///     Verifies that a value the invariant culture cannot parse leaves the date unset.
    /// </summary>
    /// <param name="value">
    ///     A literal that is not a valid invariant-culture date.
    /// </param>
    [Test]
    [Arguments("13/09/2026")]
    [Arguments("not a date")]
    [Arguments("2026-13-13")]
    public async Task Parse_Test_UnparsableDate_ReturnsEmpty(string value)
    {
        // Arrange
        string line = $"date: {value}";

        // Act
        FrontMatter result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result).IsSameReferenceAs(FrontMatter.Empty);
    }

    /// <summary>
    ///     Verifies that a quoted date is unquoted before it is parsed.
    /// </summary>
    [Test]
    public async Task Parse_Test_QuotedDate_ReturnsDate()
    {
        // Arrange
        const string line = "date: \"2026-09-13\"";

        // Act
        FrontMatter result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result.Date).IsEqualTo(new DateOnly(2026, 9, 13));
    }

    /// <summary>
    ///     Verifies that an unparsable date does not discard a title read from the same block.
    /// </summary>
    [Test]
    public async Task Parse_Test_UnparsableDateWithTitle_KeepsTitle()
    {
        // Arrange
        const string block = """
            ---
            title: Home
            date: not a date
            ---
        """;

        // Act
        FrontMatter result = FrontMatterParser.Parse(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Title).IsEqualTo("Home");
            await Assert.That(result.Date).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that date parsing is culture-invariant: under a culture whose date pattern is
    ///     day-first, the ISO form still parses and the day-first form is still rejected.
    /// </summary>
    [Test]
    [NotInParallel]
    public async Task Parse_Test_DateParsing_IsCultureInvariant()
    {
        // Arrange
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            // Act
            FrontMatter iso = FrontMatterParser.Parse("date: 2026-09-13");
            FrontMatter dayFirst = FrontMatterParser.Parse("date: 13/09/2026");

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(iso.Date).IsEqualTo(new DateOnly(2026, 9, 13));
                await Assert.That(dayFirst.Date).IsNull();
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    #endregion // Parse Tests
}
