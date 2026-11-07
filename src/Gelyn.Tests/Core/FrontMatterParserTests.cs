using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    #region TryParse Tests

    /// <summary>
    ///     Verifies that every recognized key is read from a block, delimiters and all.
    /// </summary>
    [Test]
    public async Task TryParse_Test_RecognizedKeys_ReturnAllValues()
    {
        // Arrange
        const string block = """
            ---
            title: Home
            date: 2026-09-13
            revised: 2026-10-08
            description: Notes about software
            enableMathRendering: true
            enableDiagramRendering: true
            enableSyntaxHighlighting: true
            enableComments: true
            enableFeed: true
            ---
        """;

        // Act
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result?.Title).IsEqualTo("Home");
            await Assert.That(result?.Date).IsEqualTo(new DateOnly(2026, 9, 13));
            await Assert.That(result?.Revised).IsEqualTo(new DateOnly(2026, 10, 8));
            await Assert.That(result?.Description).IsEqualTo("Notes about software");
            await Assert.That(result?.EnableMathRendering).IsTrue();
            await Assert.That(result?.EnableDiagramRendering).IsTrue();
            await Assert.That(result?.EnableSyntaxHighlighting).IsTrue();
            await Assert.That(result?.EnableComments).IsTrue();
            await Assert.That(result?.EnableFeed).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that the declared stylesheets and scripts are read as lists in the order written, in both
    ///     the block and the flow style of a YAML sequence.
    /// </summary>
    [Test]
    public async Task TryParse_Test_StylesheetsAndScripts_AreReadInOrder()
    {
        // Arrange
        const string block = """
            ---
            stylesheets:
              - assets/chart.css
              - ../shared/print.css
            scripts: [assets/chart.js]
            ---
        """;

        // Act
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(string.Join(',', result?.Stylesheets ?? [])).IsEqualTo("assets/chart.css,../shared/print.css");
            await Assert.That(string.Join(',', result?.Scripts ?? [])).IsEqualTo("assets/chart.js");
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
    public async Task TryParse_Test_MathRendering_IsReadAsADeclaredBoolean(string line, bool? expected)
    {
        // Act
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _);

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
    public async Task TryParse_Test_FeedOptIn_IsReadAsADeclaredBoolean(string line, bool? expected)
    {
        // Act
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _);

        // Assert
        await Assert.That(result?.EnableFeed).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that the comments opt-in is read as a declared boolean, and that a block omitting the key
    ///     is distinguishable from one declaring it false.
    /// </summary>
    [Test]
    [Arguments("enableComments: true", true)]
    [Arguments("enableComments: false", false)]
    [Arguments("title: Home", null)]
    public async Task TryParse_Test_Comments_IsReadAsADeclaredBoolean(string line, bool? expected)
    {
        // Act
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _);

        // Assert
        await Assert.That(result?.EnableComments).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a block carrying no entries yields no front matter.
    /// </summary>
    [Test]
    [Arguments("")]
    [Arguments("---\n---")]
    public async Task TryParse_Test_BlockWithoutEntries_ReturnsNull(string block)
    {
        // Act
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _);

        // Assert
        await Assert.That(result).IsNull();
    }

    /// <summary>
    ///     Verifies that delimiters are recognized regardless of the newline convention the block uses.
    /// </summary>
    [Test]
    [Arguments("---\r\ntitle: Home\r\n---")]
    [Arguments("---\rtitle: Home\r---")]
    public async Task TryParse_Test_NewlineConventions_AreAllSupported(string block)
    {
        // Act
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _);

        // Assert
        await Assert.That(result?.Title).IsEqualTo("Home");
    }

    /// <summary>
    ///     Verifies that keys outside the recognized vocabulary are ignored and never rejected.
    /// </summary>
    [Test]
    public async Task TryParse_Test_UnrecognizedKeys_AreIgnored()
    {
        // Arrange
        const string block = """
            ---
            author: Stefan
            draft: true
            ---
        """;

        // Act
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _);

        // Assert
        await Assert.That(result?.Title).IsNull();
    }

    /// <summary>
    ///     Verifies that key matching is ordinal, so keys differing in case are not recognized.
    /// </summary>
    [Test]
    [Arguments("Title: Home")]
    [Arguments("TITLE: Home")]
    public async Task TryParse_Test_KeyCaseMismatch_LeavesTitleUnset(string line)
    {
        // Act
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _);

        // Assert
        await Assert.That(result?.Title).IsNull();
    }

    /// <summary>
    ///     Verifies that a key with no value at all is treated as absent.
    /// </summary>
    [Test]
    [Arguments("title:")]
    [Arguments("title:   ")]
    public async Task TryParse_Test_AbsentValue_LeavesTitleUnset(string line)
    {
        // Act
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _);

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
    public async Task TryParse_Test_EmptyQuotedValue_IsPreserved(string line)
    {
        // Act
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _);

        // Assert
        await Assert.That(result?.Title).IsEqualTo(string.Empty);
    }

    /// <summary>
    ///     Verifies that a block declaring something other than a mapping is reported as malformed.
    /// </summary>
    [Test]
    [Arguments("just a bare sentence")]
    [Arguments("title:Home")]
    public async Task TryParse_Test_NonMappingBlock_Throws(string block)
    {
        // Act and Assert
        await Assert.That(() => FrontMatterParser.TryParse(block, out _, out _)).Throws<YamlException>();
    }

    /// <summary>
    ///     Verifies that a value the invariant culture cannot read as a date leaves the date unset and does
    ///     not abort the block.
    /// </summary>
    [Test]
    [Arguments("not a date")]
    [Arguments("2026-13-13")]
    public async Task TryParse_Test_UnparsableDate_LeavesDateUnset(string value)
    {
        // Arrange
        string block = $"title: Home\ndate: {value}";

        // Act
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _);

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
    public async Task TryParse_Test_DateParsing_IsCultureInvariant()
    {
        // Arrange
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            // Act
            FrontMatterParser.TryParse("date: 2026-09-13", out FrontMatter? iso, out _);
            FrontMatterParser.TryParse("date: 13/09/2026", out FrontMatter? dayFirst, out _);

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

    /// <summary>
    ///     Verifies that a key no member reads is reported with the line of the file it sits on, under either
    ///     line break, and that the keys around it are still read.
    /// </summary>
    [Test]
    [Arguments("\n")]
    [Arguments("\r\n")]
    public async Task TryParse_Test_UnknownKey_IsReportedWithItsLine(string newLine)
    {
        // Arrange
        string block = string.Join(newLine, "---", "title: Home", "titel: Typo", "date: 2026-09-13", "---");

        // Act
        bool isComplete = FrontMatterParser.TryParse(
            block,
            out FrontMatter? result,
            out IReadOnlyList<UnknownKey>? unknownKeys);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result?.Title).IsEqualTo("Home");
            await Assert.That(result?.Date).IsEqualTo(new DateOnly(2026, 9, 13));
            await Assert.That(isComplete).IsFalse();
            await Assert.That(unknownKeys).IsEquivalentTo([new UnknownKey { Name = "titel", Line = 3 }]);
        }
    }

    /// <summary>
    ///     Verifies that an unknown key holding a mapping or a sequence is reported once, without the keys
    ///     nested in its value, and that skipping the value leaves the keys after it readable.
    /// </summary>
    [Test]
    public async Task TryParse_Test_UnknownKeyWithNestedValue_IsReportedOnce()
    {
        // Arrange
        const string block = """
            ---
            author:
              name: Ada
            tags: [notes, yaml]
            title: Home
            ---
            """;

        // Act
        bool isComplete = FrontMatterParser.TryParse(
            block,
            out FrontMatter? result,
            out IReadOnlyList<UnknownKey>? unknownKeys);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result?.Title).IsEqualTo("Home");
            await Assert.That(isComplete).IsFalse();
            await Assert.That(unknownKeys?.Select(static key => key.Name)).IsEquivalentTo(["author", "tags"]);
            await Assert.That(unknownKeys?.Select(static key => key.Line)).IsEquivalentTo([2, 4]);
        }
    }

    /// <summary>
    ///     Verifies that a block declaring recognized keys alone succeeds and reports no unknown key.
    /// </summary>
    [Test]
    public async Task TryParse_Test_RecognizedKeys_ReportNoUnknownKey()
    {
        // Arrange
        const string block = """
            ---
            title: Home
            enableFeed: true
            stylesheets: [assets/home.css]
            ---
            """;

        // Act
        bool isComplete = FrontMatterParser.TryParse(block, out _, out IReadOnlyList<UnknownKey>? unknownKeys);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(isComplete).IsTrue();
            await Assert.That(unknownKeys).IsNull();
        }
    }

    #endregion // TryParse Tests
}
