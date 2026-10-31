using System;
using System.Globalization;
using System.Threading.Tasks;

using Gelyn.Core;
using Gelyn.Model;

using SharpYaml;

namespace Gelyn.Tests.Core;

/// <summary>
///     Unit tests for the <see cref="FrontMatterParser"/> class.
/// </summary>
public class FrontMatterParserTests
{
    #region Parse Tests

    /// <summary>
    ///     Verifies that every recognized key is read from a block, delimiters and all.
    /// </summary>
    [Test]
    public async Task Parse_Test_RecognizedKeys_ReturnAllValues()
    {
        // Arrange
        const string block = """
            ---
            title: Home
            date: 2026-09-13
            description: Notes about software
            enableMathRendering: true
            enableDiagramRendering: true
            enableFeed: true
            ---
        """;

        // Act
        FrontMatter? result = FrontMatterParser.Parse(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result?.Title).IsEqualTo("Home");
            await Assert.That(result?.Date).IsEqualTo(new DateOnly(2026, 9, 13));
            await Assert.That(result?.Description).IsEqualTo("Notes about software");
            await Assert.That(result?.EnableMathRendering).IsTrue();
            await Assert.That(result?.EnableDiagramRendering).IsTrue();
            await Assert.That(result?.EnableFeed).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that both spellings of a boolean are read, and that a block omitting the key is
    ///     distinguishable from one declaring it false.
    /// </summary>
    [Test]
    [Arguments("enableMathRendering: true", true)]
    [Arguments("enableMathRendering: false", false)]
    [Arguments("title: Home", null)]
    public async Task Parse_Test_MathRendering_IsReadAsADeclaredBoolean(string line, bool? expected)
    {
        // Act
        FrontMatter? result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result?.EnableMathRendering).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that the feed opt-in is read as a declared boolean, and that a block omitting the key is
    ///     distinguishable from one declaring it false.
    /// </summary>
    /// <remarks>
    ///     Both of those keep the page out of the feed, so the distinction earns a test of its own only
    ///     because the key is the whole of what puts a page in: a value misread as absent is a page silently
    ///     dropped from the feed, which nothing else would report.
    /// </remarks>
    [Test]
    [Arguments("enableFeed: true", true)]
    [Arguments("enableFeed: false", false)]
    [Arguments("title: Home", null)]
    public async Task Parse_Test_FeedOptIn_IsReadAsADeclaredBoolean(string line, bool? expected)
    {
        // Act
        FrontMatter? result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result?.EnableFeed).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a block carrying no entries yields no front matter.
    /// </summary>
    [Test]
    [Arguments("")]
    [Arguments("---\n---")]
    public async Task Parse_Test_BlockWithoutEntries_ReturnsNull(string block)
    {
        // Act
        FrontMatter? result = FrontMatterParser.Parse(block);

        // Assert
        await Assert.That(result).IsNull();
    }

    /// <summary>
    ///     Verifies that delimiters are recognized regardless of the newline convention the block uses.
    /// </summary>
    [Test]
    [Arguments("---\r\ntitle: Home\r\n---")]
    [Arguments("---\rtitle: Home\r---")]
    public async Task Parse_Test_NewlineConventions_AreAllSupported(string block)
    {
        // Act
        FrontMatter? result = FrontMatterParser.Parse(block);

        // Assert
        await Assert.That(result?.Title).IsEqualTo("Home");
    }

    /// <summary>
    ///     Verifies that keys outside the recognized vocabulary are ignored and never rejected.
    /// </summary>
    [Test]
    public async Task Parse_Test_UnrecognizedKeys_AreIgnored()
    {
        // Arrange
        const string block = """
            ---
            author: Stefan
            draft: true
            ---
        """;

        // Act
        FrontMatter? result = FrontMatterParser.Parse(block);

        // Assert
        await Assert.That(result?.Title).IsNull();
    }

    /// <summary>
    ///     Verifies that key matching is ordinal, so keys differing in case are not recognized.
    /// </summary>
    [Test]
    [Arguments("Title: Home")]
    [Arguments("TITLE: Home")]
    public async Task Parse_Test_KeyCaseMismatch_LeavesTitleUnset(string line)
    {
        // Act
        FrontMatter? result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result?.Title).IsNull();
    }

    /// <summary>
    ///     Verifies that a key with no value at all is treated as absent.
    /// </summary>
    [Test]
    [Arguments("title:")]
    [Arguments("title:   ")]
    public async Task Parse_Test_AbsentValue_LeavesTitleUnset(string line)
    {
        // Act
        FrontMatter? result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result?.Title).IsNull();
    }

    /// <summary>
    ///     Verifies that a value quoted as an empty string is kept as one, so that a page declaring an empty
    ///     title is distinguishable from a page declaring no title.
    /// </summary>
    [Test]
    [Arguments("title: \"\"")]
    [Arguments("title: ''")]
    public async Task Parse_Test_EmptyQuotedValue_IsPreserved(string line)
    {
        // Act
        FrontMatter? result = FrontMatterParser.Parse(line);

        // Assert
        await Assert.That(result?.Title).IsEqualTo(string.Empty);
    }

    /// <summary>
    ///     Verifies that a block declaring something other than a mapping is reported as malformed.
    /// </summary>
    [Test]
    [Arguments("just a bare sentence")]
    [Arguments("title:Home")]
    public async Task Parse_Test_NonMappingBlock_Throws(string block)
    {
        // Act and Assert
        await Assert.That(() => FrontMatterParser.Parse(block)).Throws<YamlException>();
    }

    /// <summary>
    ///     Verifies that a value the invariant culture cannot read as a date leaves the date unset and does
    ///     not abort the block.
    /// </summary>
    [Test]
    [Arguments("not a date")]
    [Arguments("2026-13-13")]
    public async Task Parse_Test_UnparsableDate_LeavesDateUnset(string value)
    {
        // Arrange
        string block = $"title: Home\ndate: {value}";

        // Act
        FrontMatter? result = FrontMatterParser.Parse(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result?.Title).IsEqualTo("Home");
            await Assert.That(result?.Date).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that date parsing is culture-invariant: under a culture whose date pattern is day-first, the
    ///     ISO form still parses and the day-first form is still rejected.
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
            FrontMatter? iso = FrontMatterParser.Parse("date: 2026-09-13");
            FrontMatter? dayFirst = FrontMatterParser.Parse("date: 13/09/2026");

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(iso?.Date).IsEqualTo(new DateOnly(2026, 9, 13));
                await Assert.That(dayFirst?.Date).IsNull();
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    #endregion // Parse Tests
}
