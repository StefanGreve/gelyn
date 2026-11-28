using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Gelyn.Core.Yaml;
using Gelyn.Exceptions;
using Gelyn.Model;

namespace Gelyn.Tests.Core.Yaml;

/// <summary>
///     Unit tests for the <see cref="FrontMatterParser"/> class.
/// </summary>
[Category(Categories.Internal)]
public class FrontMatterParserTests
{
    private static readonly string[] LineBreaks = ["\n", "\r\n", "\r"];

    #region Parse Tests

    /// <summary>
    ///     Verifies that each of the eleven keys reaches the member meant for it.
    /// </summary>
    [Test]
    public async Task Parse_Test_EveryKey_ReachesItsMember()
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
        Reading reading = ParseBlock(block);
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
    public async Task Parse_Test_BlockWithoutKeys_ReturnsNull(string block)
    {
        // Act
        Reading[] readings = ParseUnderEachLineBreak(block);

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
    public async Task Parse_Test_BlockWithAKey_ReturnsARecord(string block)
    {
        // Act
        Reading reading = ParseBlock(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reading.FrontMatter).IsNotNull();
            await Assert.That(reading.FrontMatter?.Title).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that a key is matched by its text, so that a key in another case reaches no member.
    /// </summary>
    [Test]
    [Arguments("title: Home", "Home")]
    [Arguments("title : Home", "Home")]
    [Arguments("Title: Home", null)]
    [Arguments("TITLE: Home", null)]
    public async Task Parse_Test_Key_IsMatchedByItsText(string block, string? expected)
    {
        // Act
        Reading reading = ParseBlock(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reading.FrontMatter?.Title).IsEqualTo(expected);
            await Assert.That(reading.UnknownKeys.Count).IsEqualTo(expected is null ? 1 : 0);
        }
    }

    /// <summary>
    ///     Verifies that text is kept as the block declares it: a value that looks like another type stays text, and
    ///     an empty string stays distinct from an absent value.
    /// </summary>
    [Test]
    [Arguments("title: 123", "123")]
    [Arguments("title: true", "true")]
    [Arguments("title: 2026-09-13", "2026-09-13")]
    [Arguments("title: \"\"", "")]
    [Arguments("title: ''", "")]
    [Arguments("title: \"null\"", "null")]
    [Arguments("title: \"Part 2: the parser\"", "Part 2: the parser")]
    [Arguments("title: 'It''s here'", "It's here")]
    public async Task Parse_Test_Text_IsKeptAsDeclared(string block, string expected)
    {
        // Act
        Reading[] readings = ParseUnderEachLineBreak(block);

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
    public async Task Parse_Test_NullSpelling_LeavesTheMemberAbsent(string block)
    {
        // Act
        Reading reading = ParseBlock(block);
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
    public async Task Parse_Test_Boolean_IsOneOfSixSpellings(string value, bool expected)
    {
        // Act
        Reading reading = ParseBlock($"enableFeed: {value}");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reading.FrontMatter?.EnableFeed).IsEqualTo(expected);
            await Assert.That(reading.InvalidValues).IsEmpty();
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
        FrontMatter? result = FrontMatterParser.Parse(line, out _, out _);

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
        FrontMatter? result = FrontMatterParser.Parse(line, out _, out _);

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
    public async Task Parse_Test_Comments_IsReadAsADeclaredBoolean(string line, bool? expected)
    {
        // Act
        FrontMatter? result = FrontMatterParser.Parse(line, out _, out _);

        // Assert
        await Assert.That(result?.EnableComments).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a date is written as <c>yyyy-MM-dd</c>, plain or quoted.
    /// </summary>
    [Test]
    [Arguments("date: 2026-09-13")]
    [Arguments("date: \"2026-09-13\"")]
    [Arguments("date: '2026-09-13'")]
    [Arguments("date: 2026-09-13 # c")]
    public async Task Parse_Test_Date_IsWrittenAsAnIsoCalendarDate(string block)
    {
        // Act
        Reading[] readings = ParseUnderEachLineBreak(block);

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
            FrontMatter? iso = FrontMatterParser.Parse("date: 2026-09-13", out _, out _);
            FrontMatter? dayFirst = FrontMatterParser.Parse("date: 13/09/2026", out _, out _);

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
    ///     Verifies that a value its member does not accept leaves the member absent and is reported with its key,
    ///     the line it starts on and the reason, and that the keys around it are still read.
    /// </summary>
    [Test]
    [Arguments("date: not a date", "date", 2, "'not a date' is not a date written as yyyy-MM-dd")]
    [Arguments("date: 2026-13-13", "date", 2, "'2026-13-13' is not a date written as yyyy-MM-dd")]
    [Arguments("date: 09/13/2026", "date", 2, "'09/13/2026' is not a date written as yyyy-MM-dd")]
    [Arguments("date: Sep 13, 2026", "date", 2, "'Sep 13, 2026' is not a date written as yyyy-MM-dd")]
    [Arguments("date: 2026-9-3", "date", 2, "'2026-9-3' is not a date written as yyyy-MM-dd")]
    [Arguments("date: 2026/09/13", "date", 2, "'2026/09/13' is not a date written as yyyy-MM-dd")]
    [Arguments("date: 2026-09-13T10:00:00", "date", 2, "'2026-09-13T10:00:00' is not a date written as yyyy-MM-dd")]
    [Arguments("date: \" 2026-09-13\"", "date", 2, "' 2026-09-13' is not a date written as yyyy-MM-dd")]
    [Arguments("date: \"\"", "date", 2, "'' is not a date written as yyyy-MM-dd")]
    [Arguments("date: [2026-09-13]", "date", 2, "a list is not a date written as yyyy-MM-dd")]
    [Arguments("revised: 13/09/2026", "revised", 2, "'13/09/2026' is not a date written as yyyy-MM-dd")]
    [Arguments("enableFeed: yes", "enableFeed", 2, "'yes' is not true or false")]
    [Arguments("enableFeed: 1", "enableFeed", 2, "'1' is not true or false")]
    [Arguments("enableFeed: on", "enableFeed", 2, "'on' is not true or false")]
    [Arguments("enableFeed: tRuE", "enableFeed", 2, "'tRuE' is not true or false")]
    [Arguments("enableFeed: \" true\"", "enableFeed", 2, "' true' is not true or false")]
    [Arguments("enableFeed: \"\"", "enableFeed", 2, "'' is not true or false")]
    [Arguments("enableFeed: [true]", "enableFeed", 2, "a list is not true or false")]
    [Arguments("enableComments:\n  - true", "enableComments", 3, "a list is not true or false")]
    [Arguments("title: [a, b]", "title", 2, "a list is not text")]
    [Arguments("title:\n  - a", "title", 3, "a list is not text")]
    [Arguments("stylesheets: a.css", "stylesheets", 2, "'a.css' is not a list")]
    [Arguments("scripts: \"a.js\"", "scripts", 2, "'a.js' is not a list")]
    public async Task Parse_Test_RejectedValue_IsReportedAndTheRestIsRead(string entry, string name, int line, string message)
    {
        // Arrange
        string block = $"description: before\n{entry}\nenableDiagramRendering: true";

        // Act
        Reading[] readings = ParseUnderEachLineBreak(block);

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
    public async Task Parse_Test_List_KeepsItsEntriesInOrder(string block, string expected)
    {
        // Act
        Reading[] readings = ParseUnderEachLineBreak(block);

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
    ///     Verifies that a key no member reads is reported with its line, once for each time it is declared, and
    ///     that the keys after it are still read.
    /// </summary>
    [Test]
    [Arguments("titel: Typo\ntitle: Home", "titel@1")]
    [Arguments("---\ntitle: Home\ntitel: Typo\ndate: 2026-09-13\n---", "titel@3")]
    [Arguments("Title: x\ntitle: Home", "Title@1")]
    [Arguments("a b: c\ntitle: Home", "a b@1")]
    [Arguments("author:\ntitle: Home", "author@1")]
    [Arguments("author: a\nauthor: b\ntitle: Home", "author@1,author@2")]
    [Arguments("author: \"a: b\"\ntitle: Home", "author@1")]
    [Arguments("tags: [notes, yaml]\ntitle: Home", "tags@1")]
    [Arguments("tags:\n  - x\n  - y\ntitle: Home", "tags@1")]
    [Arguments("tags:\n- x\n- y\ntitle: Home", "tags@1")]
    public async Task Parse_Test_UnknownKey_IsReportedAndPassedOver(string block, string expected)
    {
        // Act
        Reading[] readings = ParseUnderEachLineBreak(block);

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
    ///     Verifies that a recognized key stops the block when it is declared a second time.
    /// </summary>
    [Test]
    [Arguments("title: a\ntitle: b", "title")]
    [Arguments("enableFeed: true\nenableFeed: false", "enableFeed")]
    [Arguments("stylesheets: [a]\nstylesheets:\n  - b", "stylesheets")]
    public async Task Parse_Test_RecognizedKeyDeclaredTwice_Throws(string block, string name)
    {
        // Act and Assert
        await AssertFailure(block, 2, $"'{name}' is declared twice");
    }

    /// <summary>
    ///     Verifies that an anchor, an alias or a tag stops the block, whether a member reads the key or not, since
    ///     front matter has no use for them.
    /// </summary>
    [Test]
    [Arguments("title: &a Home", 1)]
    [Arguments("title: !!str 123", 1)]
    [Arguments("title: *a", 1)]
    [Arguments("author: &a x", 1)]
    [Arguments("stylesheets:\n  - &a x.css", 2)]
    [Arguments("stylesheets: [*a]", 1)]
    public async Task Parse_Test_AnchorAliasOrTag_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "front matter supports no anchors, aliases or tags");
    }

    /// <summary>
    ///     Verifies that a malformed block stops the read with the line at fault and the reason, also when the
    ///     fault sits in a value that its member rejects or that no member reads.
    /// </summary>
    [Test]
    [Arguments("date: 2026-09-13\ntitle: \"unterminated", 2, "the quote that opens on it is not closed on the same line")]
    [Arguments("just a bare sentence", 1, "it is not a key followed by ': '")]
    [Arguments("title:Home", 1, "it is not a key followed by ': '")]
    [Arguments("title: Part 2: the parser", 1, "': ' inside plain text starts a mapping, so quote the text")]
    [Arguments("title: [a, , b]", 1, "the list holds an empty entry")]
    [Arguments("author: [a, , b]", 1, "the list holds an empty entry")]
    [Arguments("author:\n  name: Ada", 2, "it is indented, and a value ends with its line")]
    [Arguments("description: |\n  Who I am", 1, "front matter holds no block text, so write the text on one line")]
    [Arguments("stylesheets:\n  - a.css\n  - [b.css]", 3, "plain text cannot start with '[', so quote it")]
    [Arguments("title: Home\n--- x", 2, "a second document starts on it")]
    public async Task Parse_Test_MalformedBlock_Throws(string block, int line, string reason)
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
    public async Task Parse_Test_PlainBlock_AllocatesTheRecordAndItsTextAlone()
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
            GC.KeepAlive(FrontMatterParser.Parse(text, out _, out _));

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

    #endregion // Parse Tests

    #region Helpers

    private static Reading ParseBlock(string block)
    {
        FrontMatter? frontMatter = FrontMatterParser.Parse(block, out IReadOnlyList<UnknownKey> unknownKeys, out IReadOnlyList<InvalidValue> invalidValues);

        return new Reading(frontMatter, unknownKeys, invalidValues);
    }

    // A block is written with \n and read under all three line breaks of YAML.
    private static Reading[] ParseUnderEachLineBreak(string block) =>
        [.. LineBreaks.Select(lineBreak => ParseBlock(block.Replace("\n", lineBreak, StringComparison.Ordinal)))];

    private static string? Join(IReadOnlyList<string?>? entries) =>
        entries is null ? null : string.Join('|', entries.Select(static entry => entry ?? "<null>"));

    private static async Task AssertFailure(string block, int line, string reason)
    {
        using (Assert.Multiple())
        {
            foreach (string lineBreak in LineBreaks)
            {
                string text = block.Replace("\n", lineBreak, StringComparison.Ordinal);
                FrontMatterException? exception = await Assert.That(() => ParseBlock(text)).Throws<FrontMatterException>();

                await Assert.That(exception?.Line).IsEqualTo(line);
                await Assert.That(exception?.Reason).IsEqualTo(reason);
            }
        }
    }

    private sealed record Reading(FrontMatter? FrontMatter, IReadOnlyList<UnknownKey> UnknownKeys, IReadOnlyList<InvalidValue> InvalidValues);

    #endregion
}
