using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Gelyn.Core.Yaml;
using Gelyn.Exceptions;
using Gelyn.Model;

using SharpYaml;

using TUnit.Assertions.Enums;

namespace Gelyn.Tests.Core.Yaml;

/// <summary>
///     Unit tests for the <see cref="FrontMatterParser"/> class.
/// </summary>
[Category(Categories.Internal)]
public class FrontMatterParserTests
{
    private static readonly string[] LineBreaks = ["\n", "\r\n", "\r"];

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
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _, out _);

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
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _, out _);

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
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _, out _);

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
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _, out _);

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
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _, out _);

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
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _, out _);

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
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _, out _);

        // Assert
        await Assert.That(result?.Title).IsEqualTo("Home");
    }

    /// <summary>
    ///     Verifies that a block scalar keeps its final line break when the closing delimiter follows it, as it
    ///     does before another key.
    /// </summary>
    [Test]
    [Arguments("---\ntitle: |\n  Home\n---")]
    [Arguments("---\ntitle: |\n  Home\nauthor: Stefan\n---")]
    public async Task TryParse_Test_BlockScalar_KeepsItsFinalLineBreak(string block)
    {
        // Act
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _, out _);

        // Assert
        await Assert.That(result?.Title).IsEqualTo("Home\n");
    }

    /// <summary>
    ///     Verifies that keys outside the recognized vocabulary are skipped and never fail the block.
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
        FrontMatterParser.TryParse(block, out FrontMatter? result, out _, out _);

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
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _, out _);

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
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _, out _);

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
        FrontMatterParser.TryParse(line, out FrontMatter? result, out _, out _);

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
        await Assert.That(() => FrontMatterParser.TryParse(block, out _, out _, out _)).Throws<YamlException>();
    }

    /// <summary>
    ///     Verifies that a value not written as an ISO 8601 calendar date leaves the date unset and does not
    ///     abort the block but is reported with its key and line, including the spellings the invariant culture
    ///     would otherwise accept.
    /// </summary>
    [Test]
    [Arguments("not a date")]
    [Arguments("2026-13-13")]
    [Arguments("09/13/2026")]
    [Arguments("Sep 13, 2026")]
    [Arguments("2026-9-3")]
    [Arguments("2026/09/13")]
    public async Task TryParse_Test_UnparsableDate_LeavesDateUnset(string value)
    {
        // Arrange
        string block = $"title: Home\ndate: {value}";

        // Act
        bool isComplete = FrontMatterParser.TryParse(
            block,
            out FrontMatter? result,
            out _,
            out IReadOnlyList<InvalidValue> invalidValues);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result?.Title).IsEqualTo("Home");
            await Assert.That(result?.Date).IsNull();
            await Assert.That(isComplete).IsFalse();
            await Assert.That(invalidValues.Count).IsEqualTo(1);
            await Assert.That(invalidValues[0].Name).IsEqualTo("date");
            await Assert.That(invalidValues[0].Line).IsEqualTo(2);
            await Assert.That(invalidValues[0].Message).Contains(value);
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
            FrontMatterParser.TryParse("date: 2026-09-13", out FrontMatter? iso, out _, out _);
            FrontMatterParser.TryParse("date: 13/09/2026", out FrontMatter? dayFirst, out _, out _);

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
            out IReadOnlyList<UnknownKey> unknownKeys,
            out _);

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
            out IReadOnlyList<UnknownKey> unknownKeys,
            out _);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result?.Title).IsEqualTo("Home");
            await Assert.That(isComplete).IsFalse();
            await Assert.That(unknownKeys?.Select(static key => (key.Name, key.Line))).IsEquivalentTo([("author", 2), ("tags", 4)], CollectionOrdering.Matching);
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
        bool isComplete = FrontMatterParser.TryParse(block, out _, out IReadOnlyList<UnknownKey> unknownKeys, out _);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(isComplete).IsTrue();
            await Assert.That(unknownKeys).IsEmpty();
        }
    }

    #endregion // TryParse Tests

    #region Read Tests

    /// <summary>
    ///     Verifies that each of the eleven keys reaches the member meant for it.
    /// </summary>
    [Test]
    public async Task Read_Test_EveryKey_ReachesItsMember()
    {
        // Arrange
        const string block = """
            ---
            title: Home
            date: 2026-09-13
            revised: 2026-10-08
            description: Notes about software
            enableMathRendering: true
            enableDiagramRendering: false
            enableSyntaxHighlighting: True
            enableComments: FALSE
            enableFeed: TRUE
            stylesheets: [assets/a.css]
            scripts:
              - assets/a.js
            ---
            """;

        // Act
        Reading reading = Read(block);
        FrontMatter? result = reading.FrontMatter;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result?.Title).IsEqualTo("Home");
            await Assert.That(result?.Date).IsEqualTo(new DateOnly(2026, 9, 13));
            await Assert.That(result?.Revised).IsEqualTo(new DateOnly(2026, 10, 8));
            await Assert.That(result?.Description).IsEqualTo("Notes about software");
            await Assert.That(result?.EnableMathRendering).IsTrue();
            await Assert.That(result?.EnableDiagramRendering).IsFalse();
            await Assert.That(result?.EnableSyntaxHighlighting).IsTrue();
            await Assert.That(result?.EnableComments).IsFalse();
            await Assert.That(result?.EnableFeed).IsTrue();
            await Assert.That(Join(result?.Stylesheets)).IsEqualTo("assets/a.css");
            await Assert.That(Join(result?.Scripts)).IsEqualTo("assets/a.js");
            await Assert.That(reading.UnknownKeys).IsEmpty();
            await Assert.That(reading.InvalidValues).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that a block that declares no key yields no front matter.
    /// </summary>
    [Test]
    [Arguments("")]
    [Arguments("---\n---")]
    [Arguments("# only a comment")]
    public async Task Read_Test_BlockWithoutKeys_ReturnsNull(string block)
    {
        // Act
        Reading[] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Reading reading in readings)
                await Assert.That(reading.FrontMatter).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that a block yields a record once it declares a key, whatever the key holds, so that an empty
    ///     block stays distinguishable from one whose keys are all unknown or empty.
    /// </summary>
    [Test]
    [Arguments("author: Ada")]
    [Arguments("title: ~")]
    [Arguments("title: # c")]
    public async Task Read_Test_BlockWithAKey_ReturnsARecord(string block)
    {
        // Act
        Reading reading = Read(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reading.FrontMatter).IsNotNull();
            await Assert.That(reading.FrontMatter?.Title).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that a key is matched by its text, so that a quoted key reaches its member once it is decoded,
    ///     while a key in another case reaches none.
    /// </summary>
    [Test]
    [Arguments("title : Home", "Home")]
    [Arguments("\"title\": Home", "Home")]
    [Arguments("'title': Home", "Home")]
    [Arguments("\"ti\\x74le\": Home", "Home")]
    [Arguments("Title: Home", null)]
    [Arguments("TITLE: Home", null)]
    public async Task Read_Test_Key_IsMatchedByItsText(string block, string? expected)
    {
        // Act
        Reading reading = Read(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reading.FrontMatter?.Title).IsEqualTo(expected);
            await Assert.That(reading.UnknownKeys.Count).IsEqualTo(expected is null ? 1 : 0);
        }
    }

    /// <summary>
    ///     Verifies that text is kept as the block declares it: a value that looks like another type stays text, an
    ///     empty string stays distinct from an absent value, and a block scalar keeps its final line break.
    /// </summary>
    [Test]
    [Arguments("title: 123", "123")]
    [Arguments("title: true", "true")]
    [Arguments("title: 2026-09-13", "2026-09-13")]
    [Arguments("title: \"\"", "")]
    [Arguments("title: \"null\"", "null")]
    [Arguments("title: plain\n  continued", "plain continued")]
    [Arguments("title: |\n  Home\n---", "Home\n")]
    public async Task Read_Test_Text_IsKeptAsDeclared(string block, string expected)
    {
        // Act
        Reading[] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Reading reading in readings)
            {
                await Assert.That(reading.FrontMatter?.Title).IsEqualTo(expected);
                await Assert.That(reading.InvalidValues).IsEmpty();
            }
        }
    }

    /// <summary>
    ///     Verifies that an empty value and the plain spellings of null leave the member absent without a report,
    ///     for every kind of member.
    /// </summary>
    [Test]
    [Arguments("title: ~")]
    [Arguments("title: null")]
    [Arguments("title: Null")]
    [Arguments("title: NULL")]
    [Arguments("title:")]
    [Arguments("title:   ")]
    [Arguments("title: # c")]
    [Arguments("title: ~ # c")]
    [Arguments("title:\n  # c")]
    [Arguments("date: ~")]
    [Arguments("date:")]
    [Arguments("enableFeed: null")]
    [Arguments("stylesheets: ~")]
    [Arguments("stylesheets:")]
    public async Task Read_Test_NullSpelling_LeavesTheMemberAbsent(string block)
    {
        // Act
        Reading reading = Read(block);
        FrontMatter? result = reading.FrontMatter;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result).IsNotNull();
            await Assert.That(result?.Title).IsNull();
            await Assert.That(result?.Date).IsNull();
            await Assert.That(result?.EnableFeed).IsNull();
            await Assert.That(result?.Stylesheets).IsNull();
            await Assert.That(reading.InvalidValues).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that a boolean is one of the six spellings of the core schema, plain or quoted.
    /// </summary>
    [Test]
    [Arguments("true", true)]
    [Arguments("True", true)]
    [Arguments("TRUE", true)]
    [Arguments("false", false)]
    [Arguments("False", false)]
    [Arguments("FALSE", false)]
    [Arguments("\"true\"", true)]
    [Arguments("'FALSE'", false)]
    [Arguments("true # c", true)]
    public async Task Read_Test_Boolean_IsOneOfSixSpellings(string value, bool expected)
    {
        // Act
        Reading reading = Read($"enableFeed: {value}");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reading.FrontMatter?.EnableFeed).IsEqualTo(expected);
            await Assert.That(reading.InvalidValues).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that a date is written as <c>yyyy-MM-dd</c>, plain, quoted or in a folded block that is
    ///     stripped of its line break.
    /// </summary>
    [Test]
    [Arguments("date: 2026-09-13")]
    [Arguments("date: \"2026-09-13\"")]
    [Arguments("date: '2026-09-13'")]
    [Arguments("date: 2026-09-13 # c")]
    [Arguments("date:\n  2026-09-13")]
    [Arguments("date: >-\n  2026-09-13\n---")]
    public async Task Read_Test_Date_IsWrittenAsAnIsoCalendarDate(string block)
    {
        // Act
        Reading[] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Reading reading in readings)
            {
                await Assert.That(reading.FrontMatter?.Date).IsEqualTo(new DateOnly(2026, 9, 13));
                await Assert.That(reading.InvalidValues).IsEmpty();
            }
        }
    }

    /// <summary>
    ///     Verifies that a value its member does not accept leaves the member absent and is reported with its key,
    ///     the line it starts on and the reason, and that the keys around it are still read.
    /// </summary>
    [Test]
    [Arguments("date: not a date", "date", 2, "'not a date' is not a date written as yyyy-MM-dd")]
    [Arguments("date: 2026-13-13", "date", 2, "'2026-13-13' is not a date written as yyyy-MM-dd")]
    [Arguments("date: 09/13/2026", "date", 2, "'09/13/2026' is not a date written as yyyy-MM-dd")]
    [Arguments("date: Sep 13, 2026", "date", 2, "'Sep 13, 2026' is not a date written as yyyy-MM-dd")]
    [Arguments("date: 2026-9-3", "date", 2, "'2026-9-3' is not a date written as yyyy-MM-dd")]
    [Arguments("date: 2026-09-13T10:00:00", "date", 2, "'2026-09-13T10:00:00' is not a date written as yyyy-MM-dd")]
    [Arguments("date: \" 2026-09-13\"", "date", 2, "' 2026-09-13' is not a date written as yyyy-MM-dd")]
    [Arguments("date: \"\"", "date", 2, "'' is not a date written as yyyy-MM-dd")]
    [Arguments("date: 2026-09-13\n  x", "date", 2, "'2026-09-13 x' is not a date written as yyyy-MM-dd")]
    [Arguments("date: |\n  2026-09-13", "date", 2, "'2026-09-13\\n' is not a date written as yyyy-MM-dd")]
    [Arguments("date:\n  nope", "date", 3, "'nope' is not a date written as yyyy-MM-dd")]
    [Arguments("date: [2026-09-13]", "date", 2, "a list is not a date written as yyyy-MM-dd")]
    [Arguments("date: {a: b}", "date", 2, "a mapping is not a date written as yyyy-MM-dd")]
    [Arguments("revised: 13/09/2026", "revised", 2, "'13/09/2026' is not a date written as yyyy-MM-dd")]
    [Arguments("enableFeed: yes", "enableFeed", 2, "'yes' is not true or false")]
    [Arguments("enableFeed: 1", "enableFeed", 2, "'1' is not true or false")]
    [Arguments("enableFeed: on", "enableFeed", 2, "'on' is not true or false")]
    [Arguments("enableFeed: tRuE", "enableFeed", 2, "'tRuE' is not true or false")]
    [Arguments("enableFeed: \" true\"", "enableFeed", 2, "' true' is not true or false")]
    [Arguments("enableFeed: \"\"", "enableFeed", 2, "'' is not true or false")]
    [Arguments("enableFeed: |\n  true", "enableFeed", 2, "'true\\n' is not true or false")]
    [Arguments("enableFeed: [true]", "enableFeed", 2, "a list is not true or false")]
    [Arguments("enableComments: {a: b}", "enableComments", 2, "a mapping is not true or false")]
    [Arguments("title: [a, b]", "title", 2, "a list is not text")]
    [Arguments("title: {a: b}", "title", 2, "a mapping is not text")]
    [Arguments("title:\n  name: x", "title", 3, "a mapping is not text")]
    [Arguments("title:\n  - a", "title", 3, "a list is not text")]
    [Arguments("stylesheets: a.css", "stylesheets", 2, "'a.css' is not a list")]
    [Arguments("stylesheets: {a: b}", "stylesheets", 2, "a mapping is not a list")]
    [Arguments("scripts:\n  a: b", "scripts", 3, "a mapping is not a list")]
    public async Task Read_Test_RejectedValue_IsReportedAndTheRestIsRead(string entry, string name, int line, string message)
    {
        // Arrange
        string block = $"description: before\n{entry}\nenableDiagramRendering: true";

        // Act
        Reading[] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Reading reading in readings)
            {
                await Assert.That(reading.InvalidValues.Count).IsEqualTo(1);
                await Assert.That(reading.InvalidValues[0].Name).IsEqualTo(name);
                await Assert.That(reading.InvalidValues[0].Line).IsEqualTo(line);
                await Assert.That(reading.InvalidValues[0].Message).IsEqualTo(message);
                await Assert.That(reading.FrontMatter?.Description).IsEqualTo("before");
                await Assert.That(reading.FrontMatter?.EnableDiagramRendering).IsTrue();
                await Assert.That(reading.FrontMatter?.Title).IsNull();
                await Assert.That(reading.FrontMatter?.Date).IsNull();
                await Assert.That(reading.FrontMatter?.Revised).IsNull();
                await Assert.That(reading.FrontMatter?.EnableFeed).IsNull();
                await Assert.That(reading.FrontMatter?.EnableComments).IsNull();
                await Assert.That(reading.FrontMatter?.Stylesheets).IsNull();
                await Assert.That(reading.FrontMatter?.Scripts).IsNull();
            }
        }
    }

    /// <summary>
    ///     Verifies that a list keeps its entries in the order declared, in the block and in the flow style, that
    ///     an entry without a value is kept as <see langword="null"/>, and that an empty list is no absent one.
    /// </summary>
    [Test]
    [Arguments("stylesheets: [a.css, b.css]", "a.css|b.css")]
    [Arguments("stylesheets: []", "")]
    [Arguments("stylesheets: [~, a.css]", "<null>|a.css")]
    [Arguments("stylesheets:\n  - a.css\n  - b.css", "a.css|b.css")]
    [Arguments("stylesheets:\n- a.css\n- b.css", "a.css|b.css")]
    [Arguments("stylesheets:\n  -\n  - a.css", "<null>|a.css")]
    public async Task Read_Test_List_KeepsItsEntriesInOrder(string block, string expected)
    {
        // Act
        Reading[] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Reading reading in readings)
            {
                await Assert.That(Join(reading.FrontMatter?.Stylesheets)).IsEqualTo(expected);
                await Assert.That(reading.InvalidValues).IsEmpty();
            }
        }
    }

    /// <summary>
    ///     Verifies that an entry which is itself a list or a mapping is dropped and reported under the key of the
    ///     list with its own line, and that the other entries stay.
    /// </summary>
    [Test]
    [Arguments("stylesheets: [a, [b], c]", "a|c", 1, "a list is not a path")]
    [Arguments("stylesheets: [a: b]", "", 1, "a mapping is not a path")]
    [Arguments("stylesheets: [a, b: ]", "a", 1, "a mapping is not a path")]
    [Arguments("stylesheets: [a, {b: c}]", "a", 1, "a mapping is not a path")]
    [Arguments("stylesheets:\n  - [nested]", "", 2, "a list is not a path")]
    [Arguments("stylesheets:\n  - a.css\n  - - b", "a.css", 3, "a list is not a path")]
    [Arguments("stylesheets:\n  - a: b\n  - c.css", "c.css", 2, "a mapping is not a path")]
    [Arguments("stylesheets:\n  - {a: b}\n  - c.css", "c.css", 2, "a mapping is not a path")]
    [Arguments("stylesheets:\n  - a.css\n  -\n    b: c", "a.css", 4, "a mapping is not a path")]
    public async Task Read_Test_NestedEntry_IsDroppedAndReported(string block, string expected, int line, string message)
    {
        // Act
        Reading[] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Reading reading in readings)
            {
                await Assert.That(Join(reading.FrontMatter?.Stylesheets)).IsEqualTo(expected);
                await Assert.That(reading.InvalidValues.Count).IsEqualTo(1);
                await Assert.That(reading.InvalidValues[0].Name).IsEqualTo("stylesheets");
                await Assert.That(reading.InvalidValues[0].Line).IsEqualTo(line);
                await Assert.That(reading.InvalidValues[0].Message).IsEqualTo(message);
            }
        }
    }

    /// <summary>
    ///     Verifies that a key no member reads is reported with its line, once for each time it is declared, that
    ///     its value is skipped whatever it holds, and that the keys after it are still read.
    /// </summary>
    [Test]
    [Arguments("titel: Typo\ntitle: Home", "titel@1")]
    [Arguments("Title: x\ntitle: Home", "Title@1")]
    [Arguments("a b: c\ntitle: Home", "a b@1")]
    [Arguments("'ti''tle': x\ntitle: Home", "ti'tle@1")]
    [Arguments("author: a\nauthor: b\ntitle: Home", "author@1,author@2")]
    [Arguments("author:\n  name: Ada\ntags: [notes, yaml]\ntitle: Home", "author@1,tags@3")]
    [Arguments("author: {name: x,\n  url: y}\ntitle: Home", "author@1")]
    [Arguments("author: {name: O'Brien}\ntitle: Home", "author@1")]
    [Arguments("author: [it's, \"a\"]\ntitle: Home", "author@1")]
    [Arguments("author:\n  - x\n  - y\ntitle: Home", "author@1")]
    [Arguments("author:\n- x\n- y\ntitle: Home", "author@1")]
    [Arguments("author: |\n  text\n  more\ntitle: Home", "author@1")]
    [Arguments("author: \"a\n  b\"\ntitle: Home", "author@1")]
    [Arguments("author: &a x\ntitle: Home", "author@1")]
    [Arguments("author: !!str x\ntitle: Home", "author@1")]
    [Arguments("author: *a\ntitle: Home", "author@1")]
    [Arguments("author: &a\n  name: x\ntitle: Home", "author@1")]
    [Arguments("author:\n  name: a: b\ntitle: Home", "author@1")]
    [Arguments("author:\n  deep:\n    deeper: [x\n  other: y\ntitle: Home", "author@1")]
    [Arguments("author:\n  \"a\\\"b\": x\ntitle: Home", "author@1")]
    [Arguments("author:\n  'a''b': x\ntitle: Home", "author@1")]
    [Arguments("author:\n  ? a\n  : b\ntitle: Home", "author@1")]
    [Arguments("author: [&a x, !!str y, *a]\ntitle: Home", "author@1")]
    [Arguments("author: [&a , b]\ntitle: Home", "author@1")]
    [Arguments("author: {\"a\": 'b, c', d: [\"e]\"]}\ntitle: Home", "author@1")]
    public async Task Read_Test_UnknownKey_IsReportedAndSkipped(string block, string expected)
    {
        // Act
        Reading[] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Reading reading in readings)
            {
                await Assert.That(string.Join(',', reading.UnknownKeys.Select(static key => $"{key.Name}@{key.Line}"))).IsEqualTo(expected);
                await Assert.That(reading.FrontMatter?.Title).IsEqualTo("Home");
                await Assert.That(reading.InvalidValues).IsEmpty();
            }
        }
    }

    /// <summary>
    ///     Verifies that a recognized key stops the block when it is declared a second time, whichever way it is
    ///     spelled.
    /// </summary>
    [Test]
    [Arguments("title: a\ntitle: b", "title")]
    [Arguments("enableFeed: true\nenableFeed: false", "enableFeed")]
    [Arguments("\"title\": a\ntitle: b", "title")]
    [Arguments("stylesheets: [a]\nstylesheets:\n  - b", "stylesheets")]
    public async Task Read_Test_RecognizedKeyDeclaredTwice_Throws(string block, string name)
    {
        // Act and Assert
        await AssertFailure(block, 2, $"'{name}' is declared twice");
    }

    /// <summary>
    ///     Verifies that an anchor, an alias or a tag stops the block on the value of a recognized key, since front
    ///     matter has no use for them, while the value of an unknown key may carry them.
    /// </summary>
    [Test]
    [Arguments("title: &a Home", 1)]
    [Arguments("title: !!str 123", 1)]
    [Arguments("title: *a", 1)]
    [Arguments("author: &a x\ntitle: *a", 2)]
    [Arguments("stylesheets:\n  - &a x.css\n  - *a", 2)]
    [Arguments("stylesheets: [*a]", 1)]
    public async Task Read_Test_AnchorAliasOrTagOnARecognizedKey_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "front matter supports no anchors, aliases or tags");
    }

    /// <summary>
    ///     Verifies that a malformed block stops the read with the line at fault and the reason, also when the
    ///     fault sits in a value that its member rejects or that no member reads.
    /// </summary>
    [Test]
    [Arguments("date: 2026-09-13\ntitle: \"unterminated", 2, "the quote that opens on it is never closed")]
    [Arguments("title: [a, , b]", 1, "the list holds an empty entry")]
    [Arguments("author: [a, , b]", 1, "the list holds an empty entry")]
    [Arguments("author: x\n  more: y", 2, "': ' inside plain text starts a mapping, so quote the text")]
    [Arguments("title: Home\n--- x", 2, "a second document starts on it")]
    public async Task Read_Test_MalformedBlock_Throws(string block, int line, string reason)
    {
        // Act and Assert
        await AssertFailure(block, line, reason);
    }

    /// <summary>
    ///     Verifies that a block of plain values allocates its record and its text alone: no list for keys or
    ///     values that it does not report, no copy of the block and no buffer.
    /// </summary>
    /// <remarks>
    ///     The first code the JIT emits boxes the characters that the generic span helpers compare, which the
    ///     optimized code and Native AOT do not. The parser is therefore called until its optimized code is in
    ///     place.
    /// </remarks>
    [Test]
    public async Task Read_Test_PlainBlock_AllocatesTheRecordAndItsTextAlone()
    {
        // Arrange
        const string block = "---\ntitle: Post 0-2\ndate: 2026-01-03\ndescription: Page Post 0-2\n---\n";

        [MethodImpl(MethodImplOptions.NoInlining)]
        static FrontMatter Create() => new()
        {
            Title = new string('x', 8),
            Date = new DateOnly(2026, 1, 3),
            Description = new string('x', 13),
        };

        static long MeasureRecord()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(Create());

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        static long MeasureParser(string text)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(FrontMatterParser.Read(text, out _, out _));

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        _ = MeasureRecord();
        long expected = MeasureRecord();
        long allocated = long.MaxValue;
        long deadline = Environment.TickCount64 + 30_000;

        // Act
        while (allocated != expected && Environment.TickCount64 < deadline)
        {
            for (int call = 0; call < 2_000; call++)
                _ = MeasureParser(block);

            await Task.Delay(50);
            allocated = MeasureParser(block);
        }

        // Assert
        await Assert.That(allocated).IsEqualTo(expected);
    }

    #endregion // Read Tests

    #region Helpers

    private static Reading Read(string block)
    {
        FrontMatter? frontMatter = FrontMatterParser.Read(block, out IReadOnlyList<UnknownKey> unknownKeys, out IReadOnlyList<InvalidValue> invalidValues);

        return new Reading(frontMatter, unknownKeys, invalidValues);
    }

    // A block is written with \n and read under all three line breaks of YAML.
    private static Reading[] ReadUnderEachLineBreak(string block) =>
        [.. LineBreaks.Select(lineBreak => Read(block.Replace("\n", lineBreak, StringComparison.Ordinal)))];

    private static string? Join(IReadOnlyList<string?>? entries) =>
        entries is null ? null : string.Join('|', entries.Select(static entry => entry ?? "<null>"));

    private static async Task AssertFailure(string block, int line, string reason)
    {
        using (Assert.Multiple())
        {
            foreach (string lineBreak in LineBreaks)
            {
                string text = block.Replace("\n", lineBreak, StringComparison.Ordinal);
                FrontMatterException? exception = await Assert.That(() => Read(text)).Throws<FrontMatterException>();

                await Assert.That(exception?.Line).IsEqualTo(line);
                await Assert.That(exception?.Reason).IsEqualTo(reason);
            }
        }
    }

    private sealed record Reading(FrontMatter? FrontMatter, IReadOnlyList<UnknownKey> UnknownKeys, IReadOnlyList<InvalidValue> InvalidValues);

    #endregion
}
