using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Gelyn.Core.Yaml;
using Gelyn.Exceptions;

namespace Gelyn.Tests.Core.Yaml;

/// <summary>
///     Unit tests for the <see cref="LeanYamlParser"/> struct.
/// </summary>
/// <remarks>
///     A block is written with <c>\n</c> and read under all three line breaks of YAML, so that every expectation
///     also holds for a file with Windows line endings.
/// </remarks>
[Category(Categories.Internal)]
public class LeanYamlParserTests
{
    private const char Backslash = (char)92;

    private const string NoProperties = "front matter supports no anchors, aliases or tags";

    private static readonly string[] LineBreaks = ["\n", "\r\n", "\r"];

    #region Key Tests

    /// <summary>
    ///     Verifies that a block which declares no key holds none.
    /// </summary>
    [Test]
    [Arguments("")]
    [Arguments("---")]
    [Arguments("---\n---")]
    [Arguments("---\n\n---")]
    [Arguments("\n\n")]
    [Arguments("# only a comment")]
    [Arguments("---\n# c\n...\n")]
    public async Task ReadKey_Test_BlockWithoutKeys_ReturnsFalse(string block)
    {
        // Act
        Entry[][] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Entry[] entries in readings)
                await Assert.That(entries).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that both delimiters are optional, that they may trail spaces and tabs, that three dots close
    ///     the block as well, and that only blank lines and comments may follow the closing one.
    /// </summary>
    [Test]
    [Arguments("title: Home")]
    [Arguments("---\ntitle: Home")]
    [Arguments("title: Home\n---")]
    [Arguments("---  \ntitle: Home\n---\t ")]
    [Arguments("---\ntitle: Home\n...")]
    [Arguments("---\ntitle: Home\n---\n")]
    [Arguments("---\ntitle: Home\n---\n\n# c\n")]
    public async Task ReadKey_Test_Delimiters_AreOptional(string block)
    {
        // Act
        Entry[][] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Entry[] entries in readings)
                await Assert.That(Describe(entries)).IsEqualTo("title=Home");
        }
    }

    /// <summary>
    ///     Verifies that comments and blank lines may sit around the entries, that a comment line may be indented
    ///     with a tab, and that a blank line may hold tabs.
    /// </summary>
    [Test]
    [Arguments("# c\n\ntitle: Home\n\n# c\ndate: 2026-09-13\n", "title=Home,date=2026-09-13")]
    [Arguments("\t# comment\ntitle: Home\ndate: 2026-09-13", "title=Home,date=2026-09-13")]
    [Arguments("title: Home\n\t\ndate: 2026-09-13", "title=Home,date=2026-09-13")]
    [Arguments("title: Home\n  # c\ndate: 2026-09-13", "title=Home,date=2026-09-13")]
    [Arguments("date: 2026-09-13 # c\ntitle: Home # c", "date=2026-09-13,title=Home")]
    public async Task ReadKey_Test_CommentsAndBlankLines_AreSkipped(string block, string expected)
    {
        // Act
        Entry[][] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Entry[] entries in readings)
                await Assert.That(Describe(entries)).IsEqualTo(expected);
        }
    }

    /// <summary>
    ///     Verifies that a key is read as text: a plain key as written, without the spaces and tabs before its
    ///     colon, and a quoted key decoded.
    /// </summary>
    [Test]
    [Arguments("title: Home", "title")]
    [Arguments("title : Home", "title")]
    [Arguments("title\t: Home", "title")]
    [Arguments("Title: Home", "Title")]
    [Arguments("a b: c", "a b")]
    [Arguments("a:b: c", "a:b")]
    [Arguments("url#1: c", "url#1")]
    [Arguments("\"title\": Home", "title")]
    [Arguments("'title': Home", "title")]
    [Arguments("\"title\" : Home", "title")]
    [Arguments("\"ti\\x74le\": Home", "title")]
    [Arguments("'ti''tle': x", "ti'tle")]
    public async Task ReadKey_Test_Key_IsReadAsText(string block, string expected)
    {
        // Act
        Entry[][] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Entry[] entries in readings)
            {
                await Assert.That(entries.Length).IsEqualTo(1);
                await Assert.That(entries[0].Key).IsEqualTo(expected);
                await Assert.That(entries[0].KeyLine).IsEqualTo(1);
            }
        }
    }

    /// <summary>
    ///     Verifies that a key is read as often as the block declares it, since the parser knows no key.
    /// </summary>
    [Test]
    public async Task ReadKey_Test_KeyDeclaredTwice_IsReadTwice()
    {
        // Act
        string keys = ReadKeys("title: a\ntitle: b");

        // Assert
        await Assert.That(keys).IsEqualTo("title@1,title@2");
    }

    #endregion // Key Tests

    #region Scalar Tests

    /// <summary>
    ///     Verifies that a plain scalar is read as written: trimmed, ended by a comment, and kept as text when it
    ///     looks like a number, a boolean or a date.
    /// </summary>
    [Test]
    [Arguments("title: Home", "Home")]
    [Arguments("title:   Home \t ", "Home")]
    [Arguments("title:\tHome", "Home")]
    [Arguments("title : Home", "Home")]
    [Arguments("title\t: Home", "Home")]
    [Arguments("title: 123", "123")]
    [Arguments("title: 1.5", "1.5")]
    [Arguments("title: true", "true")]
    [Arguments("title: nUll", "nUll")]
    [Arguments("title: 2026-09-13", "2026-09-13")]
    [Arguments("title: Home # note", "Home")]
    [Arguments("title: Home\t# note", "Home")]
    [Arguments("title: Home#tag", "Home#tag")]
    [Arguments("title: -dash", "-dash")]
    [Arguments("title: :colon", ":colon")]
    [Arguments("title: ?mark", "?mark")]
    [Arguments("title: a:b", "a:b")]
    [Arguments("title: it's", "it's")]
    [Arguments("title: a\ttab", "a\ttab")]
    [Arguments("title: https://example.com/a#b", "https://example.com/a#b")]
    public async Task ReadValue_Test_PlainScalar_IsReadAsWritten(string block, string expected)
    {
        // Act and Assert
        await AssertText(block, expected);
    }

    /// <summary>
    ///     Verifies that a quoted scalar keeps what a plain one would lose: its blanks, the spelling of null, and
    ///     an empty string, which stays distinct from an absent value.
    /// </summary>
    [Test]
    [Arguments("title: \"null\"", "null")]
    [Arguments("title: 'It''s'", "It's")]
    [Arguments("title: \"\"", "")]
    [Arguments("title: ''", "")]
    [Arguments("title: \"\" # c", "")]
    [Arguments("title: 'a' # c", "a")]
    [Arguments("title: \" a \"", " a ")]
    [Arguments("title: \"a # b\"", "a # b")]
    [Arguments("title: 'a: b'", "a: b")]
    [Arguments("title:\n  \"Home\"", "Home")]
    public async Task ReadValue_Test_QuotedScalar_IsKeptAsText(string block, string expected)
    {
        // Act and Assert
        await AssertText(block, expected);
    }

    /// <summary>
    ///     Verifies that a scalar folds over its lines: a line break becomes a space, every blank line between two
    ///     lines of text becomes a line break, and an escaped line break joins the lines as they are.
    /// </summary>
    [Test]
    [Arguments("title: plain\n  continued", "plain continued")]
    [Arguments("title: one\n\n  two", "one\ntwo")]
    [Arguments("title: a\n\n\n  b", "a\n\nb")]
    [Arguments("title: x\n \tcontinued", "x continued")]
    [Arguments("title: a\n  - b", "a - b")]
    [Arguments("title: a\n  [x]", "a [x]")]
    [Arguments("title:\n  Home", "Home")]
    [Arguments("title:\n \tHome", "Home")]
    [Arguments("title: # c\n  Home", "Home")]
    [Arguments("title:\n\n  # c\n  Home\n  more", "Home more")]
    [Arguments("title: \"multi\n  line\"", "multi line")]
    [Arguments("title: 'multi\n  line'", "multi line")]
    [Arguments("title: \"one\n\n  two\"", "one\ntwo")]
    [Arguments("title: \"a\n\n\n  b\"", "a\n\nb")]
    [Arguments("title: 'a\n\n  b'", "a\nb")]
    [Arguments("title: \"a  \n  b\"", "a b")]
    [Arguments("title: \"fold\\\n  ed\"", "folded")]
    [Arguments("title: \"a \\\n  b\"", "a b")]
    [Arguments("title: 'a\n  # b\n  c'", "a # b c")]
    public async Task ReadValue_Test_ScalarOverSeveralLines_IsFolded(string block, string expected)
    {
        // Act and Assert
        await AssertText(block, expected);
    }

    /// <summary>
    ///     Verifies the two block scalar styles with every indicator: the literal one keeps line breaks, the folded
    ///     one folds them except around lines that are indented further, and the chomping indicator decides which
    ///     final line breaks stay.
    /// </summary>
    [Test]
    [Arguments("title: |\n  Home\n---", "Home\n")]
    [Arguments("title: |\n  Home", "Home")]
    [Arguments("title: |-\n  strip\n---", "strip")]
    [Arguments("title: |+\n  keep\n\n\n---", "keep\n\n\n")]
    [Arguments("title: >\n  folded\n  text\n---", "folded text\n")]
    [Arguments("title: >2\n   indented\n---", " indented\n")]
    [Arguments("title: |1\n  a\n---", " a\n")]
    [Arguments("title: |2-\n   text\n---", " text")]
    [Arguments("title: |-2\n   text\n---", " text")]
    [Arguments("title: | # c\n  text\n---", "text\n")]
    [Arguments("title: >\n  a\n    b\n  c\n---", "a\n  b\nc\n")]
    [Arguments("title: >\n  a\n\n  b\n---", "a\nb\n")]
    [Arguments("title: >\n  a\n  b\n\n  c\n   d\n  e\n---", "a b\nc\n d\ne\n")]
    [Arguments("title: >\n\n  a\n---", "\na\n")]
    [Arguments("title: |\n  a\n\n\n---", "a\n")]
    [Arguments("title: |\n  a\n   b\n  c\n---", "a\n b\nc\n")]
    [Arguments("title: >+\n  a\n\n\n---", "a\n\n\n")]
    [Arguments("title: >-\n  a\n\n\n---", "a")]
    [Arguments("title: |\n  \ttext\n---", "\ttext\n")]
    [Arguments("title: |\n  a\n  # c\nauthor: x", "a\n# c\n")]
    [Arguments("title:\n  |\n    text\n---", "text\n")]
    [Arguments("title: |\nauthor: x", "")]
    [Arguments("title: |+\n\n\nauthor: x", "\n\n")]
    [Arguments("title: |\n \nauthor: x", "")]
    public async Task ReadValue_Test_BlockScalar_KeepsOrFoldsItsLines(string block, string expected)
    {
        // Act and Assert
        await AssertText(block, expected);
    }

    /// <summary>
    ///     Verifies that a double-quoted scalar decodes every escape of YAML 1.2.
    /// </summary>
    [Test]
    public async Task ReadValue_Test_DoubleQuotedScalar_DecodesEveryEscape()
    {
        // Arrange
        (string Escape, string Expected)[] escapes =
        [
            ("0", "\0"),
            ("a", "\a"),
            ("b", "\b"),
            ("t", "\t"),
            ("\t", "\t"),
            ("n", "\n"),
            ("v", "\v"),
            ("f", "\f"),
            ("r", "\r"),
            ("e", ((char)0x1B).ToString()),
            (" ", " "),
            ("\"", "\""),
            ("/", "/"),
            (Backslash.ToString(), Backslash.ToString()),
            ("N", ((char)0x85).ToString()),
            ("_", ((char)0xA0).ToString()),
            ("L", ((char)0x2028).ToString()),
            ("P", ((char)0x2029).ToString()),
            ("x41", "A"),
            ("u00E9", ((char)0xE9).ToString()),
            ("U0001F600", char.ConvertFromUtf32(0x1F600)),
        ];

        // Act and Assert
        using (Assert.Multiple())
        {
            foreach ((string escape, string expected) in escapes)
            {
                Entry[] entries = ReadEntries($"title: \"a{Backslash}{escape}b\"");
                await Assert.That(entries[0].Value.Text).IsEqualTo($"a{expected}b");
            }
        }
    }

    /// <summary>
    ///     Verifies that an escape which names no character stops the block, on the line it sits on.
    /// </summary>
    [Test]
    [Arguments("q", "'{0}q' is not an escape")]
    [Arguments("x4", "'{0}x' is not followed by the 2 hexadecimal digits of a character")]
    [Arguments("uD800", "'{0}u' is not followed by the 4 hexadecimal digits of a character")]
    [Arguments("U00110000", "'{0}U' is not followed by the 8 hexadecimal digits of a character")]
    public async Task ReadValue_Test_UnknownEscape_Throws(string escape, string reason)
    {
        // Arrange
        string block = $"date: 2026-09-13\ntitle: \"a{Backslash}{escape}\"";

        // Act and Assert
        await AssertFailure(block, 2, string.Format(CultureInfo.InvariantCulture, reason, Backslash));
    }

    /// <summary>
    ///     Verifies that an empty value and the plain spellings of null are no scalar.
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
    [Arguments("title:\n  ~")]
    public async Task ReadValue_Test_NullSpelling_IsNull(string block)
    {
        // Act
        Entry[][] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Entry[] entries in readings)
            {
                await Assert.That(entries.Length).IsEqualTo(1);
                await Assert.That(entries[0].Value.Kind).IsEqualTo(YamlNodeKind.Null);
            }
        }
    }

    /// <summary>
    ///     Verifies that a value is reported with its kind and with the line it starts on, which is the line below
    ///     the key when the value sits there.
    /// </summary>
    [Test]
    [Arguments("title: Home", nameof(YamlNodeKind.Scalar), 1)]
    [Arguments("title:\n  Home", nameof(YamlNodeKind.Scalar), 2)]
    [Arguments("title:\n\n  # c\n  Home", nameof(YamlNodeKind.Scalar), 4)]
    [Arguments("title: |\n  text", nameof(YamlNodeKind.Scalar), 1)]
    [Arguments("title: \"a\n  b\"", nameof(YamlNodeKind.Scalar), 1)]
    [Arguments("title: [a, b]", nameof(YamlNodeKind.Sequence), 1)]
    [Arguments("title:\n  [a]", nameof(YamlNodeKind.Sequence), 2)]
    [Arguments("title:\n  - a", nameof(YamlNodeKind.Sequence), 2)]
    [Arguments("title:\n- a", nameof(YamlNodeKind.Sequence), 2)]
    [Arguments("title: {a: b}", nameof(YamlNodeKind.Mapping), 1)]
    [Arguments("title: {a: b,\n  c: d}", nameof(YamlNodeKind.Mapping), 1)]
    [Arguments("title:\n  name: x", nameof(YamlNodeKind.Mapping), 2)]
    [Arguments("title:\n  ? a\n  : b", nameof(YamlNodeKind.Mapping), 2)]
    public async Task ReadValue_Test_Value_IsReportedWithItsKindAndLine(string block, string kind, int line)
    {
        // Act
        Entry[][] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Entry[] entries in readings)
            {
                await Assert.That(entries[0].Value.Kind.ToString()).IsEqualTo(kind);
                await Assert.That(entries[0].Value.Line).IsEqualTo(line);
            }
        }
    }

    /// <summary>
    ///     Verifies that a value is read once: there is none before a key is read, and none after the value of the
    ///     key was read.
    /// </summary>
    [Test]
    public async Task ReadValue_Test_ValueThatIsNotPending_IsNull()
    {
        // Arrange
        static (YamlNodeKind Before, YamlNodeKind First, YamlNodeKind Second) ReadTwice(string text)
        {
            var parser = new LeanYamlParser(text);

            try
            {
                YamlNodeKind before = parser.ReadValue();
                _ = parser.ReadKey(out _);

                return (before, parser.ReadValue(), parser.ReadValue());
            }
            finally
            {
                parser.Dispose();
            }
        }

        // Act
        (YamlNodeKind before, YamlNodeKind first, YamlNodeKind second) = ReadTwice("title: Home");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(before).IsEqualTo(YamlNodeKind.Null);
            await Assert.That(first).IsEqualTo(YamlNodeKind.Scalar);
            await Assert.That(second).IsEqualTo(YamlNodeKind.Null);
        }
    }

    #endregion // Scalar Tests

    #region Sequence Tests

    /// <summary>
    ///     Verifies that a sequence is read in the block and in the flow style: with null entries, a trailing comma,
    ///     comments, entries over several lines, and entries at the column of the key.
    /// </summary>
    [Test]
    [Arguments("stylesheets: [a.css, b.css]", "a.css|b.css")]
    [Arguments("stylesheets: [a.css, ]", "a.css")]
    [Arguments("stylesheets: []", "")]
    [Arguments("stylesheets: [ ]", "")]
    [Arguments("stylesheets: [a.css,\n  b.css]", "a.css|b.css")]
    [Arguments("stylesheets: [\n  a.css,\n  b.css\n  ]", "a.css|b.css")]
    [Arguments("stylesheets: [\"a b.css\", 'c.css']", "a b.css|c.css")]
    [Arguments("stylesheets: [~, a.css]", "<null>|a.css")]
    [Arguments("stylesheets: [a.css, # c\n  b.css]", "a.css|b.css")]
    [Arguments("stylesheets: [a,\n# c\n  b]", "a|b")]
    [Arguments("stylesheets: [a\n  b]", "a b")]
    [Arguments("stylesheets: [a\n\n  b]", "a\nb")]
    [Arguments("stylesheets: [a\n  # c\n  , b]", "a|b")]
    [Arguments("stylesheets: [a b, c]", "a b|c")]
    [Arguments("stylesheets: [a:b]", "a:b")]
    [Arguments("stylesheets: [it's.css]", "it's.css")]
    [Arguments("stylesheets: [a.css] # c", "a.css")]
    [Arguments("stylesheets:\n  [a.css]", "a.css")]
    [Arguments("stylesheets:\n- a.css\n- b.css", "a.css|b.css")]
    [Arguments("stylesheets:\n  - a.css\n  - b.css", "a.css|b.css")]
    [Arguments("stylesheets:\n  -\n  - a.css", "<null>|a.css")]
    [Arguments("stylesheets:\n  - ~\n  - a.css", "<null>|a.css")]
    [Arguments("stylesheets:\n  -\ta.css", "a.css")]
    [Arguments("stylesheets:\n  - a.css\n  # c\n  - b.css", "a.css|b.css")]
    [Arguments("stylesheets:\n# c\n  - a.css", "a.css")]
    [Arguments("stylesheets:\n  - a.css\n\n  - b.css", "a.css|b.css")]
    [Arguments("stylesheets:\n  - a\n    b\n  - c", "a b|c")]
    [Arguments("stylesheets:\n  - a\n   - b", "a - b")]
    [Arguments("stylesheets:\n- a.css\n  - b.css", "a.css - b.css")]
    [Arguments("stylesheets:\n  - |\n    a.css\n  - b.css", "a.css\n|b.css")]
    [Arguments("stylesheets:\n  -\n    a.css", "a.css")]
    [Arguments("stylesheets:\n  - a.css # c\n  - 'b.css' # c", "a.css|b.css")]
    [Arguments("stylesheets:\n  - \"a\n    b\"", "a b")]
    public async Task ReadEntry_Test_Sequence_IsReadInBothStyles(string block, string expected)
    {
        // Act
        Entry[][] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Entry[] entries in readings)
            {
                await Assert.That(entries[0].Value.Kind).IsEqualTo(YamlNodeKind.Sequence);
                await Assert.That(Join(entries[0].Items)).IsEqualTo(expected);
            }
        }
    }

    /// <summary>
    ///     Verifies that a sequence which sits at the column of its key ends where the next key begins.
    /// </summary>
    [Test]
    public async Task ReadEntry_Test_SequenceAtTheColumnOfItsKey_EndsAtTheNextKey()
    {
        // Act
        Entry[] entries = ReadEntries("stylesheets:\n- a.css\ntitle: Home\nscripts:\n- a.js\n- b.js");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(entries.Length).IsEqualTo(3);
            await Assert.That(Join(entries[0].Items)).IsEqualTo("a.css");
            await Assert.That(entries[1].Value.Text).IsEqualTo("Home");
            await Assert.That(Join(entries[2].Items)).IsEqualTo("a.js|b.js");
        }
    }

    /// <summary>
    ///     Verifies that an entry which is itself a sequence or a mapping is skipped and reported by its kind and
    ///     with its own line, and that the other entries are still read.
    /// </summary>
    [Test]
    [Arguments("stylesheets: [a, [b], c]", "a|<sequence>|c", 1)]
    [Arguments("stylesheets: [a, [b, , c]]", "a|<sequence>", 1)]
    [Arguments("stylesheets: [a: b]", "<mapping>", 1)]
    [Arguments("stylesheets: [a, b: ]", "a|<mapping>", 1)]
    [Arguments("stylesheets: [a, {b: c}]", "a|<mapping>", 1)]
    [Arguments("stylesheets:\n  - [nested]", "<sequence>", 2)]
    [Arguments("stylesheets:\n  - a.css\n  - - b", "a.css|<sequence>", 3)]
    [Arguments("stylesheets:\n  - a: b\n  - c.css", "<mapping>|c.css", 2)]
    [Arguments("stylesheets:\n  - {a: b}\n  - c.css", "<mapping>|c.css", 2)]
    [Arguments("stylesheets:\n  - a.css\n  -\n    b: c", "a.css|<mapping>", 4)]
    public async Task ReadEntry_Test_NestedEntry_IsSkippedAndReportedByItsKind(string block, string expected, int line)
    {
        // Act
        Entry[][] readings = ReadUnderEachLineBreak(block);

        // Assert
        using (Assert.Multiple())
        {
            foreach (Entry[] entries in readings)
            {
                Node nested = entries[0].Items.Single(static item => item.Kind is YamlNodeKind.Sequence or YamlNodeKind.Mapping);

                await Assert.That(Join(entries[0].Items)).IsEqualTo(expected);
                await Assert.That(nested.Line).IsEqualTo(line);
            }
        }
    }

    /// <summary>
    ///     Verifies that a value which is no sequence has no entries, before and after it is read.
    /// </summary>
    [Test]
    [Arguments("title: Home")]
    [Arguments("title:")]
    [Arguments("title: {a: b}")]
    public async Task ReadEntry_Test_ValueThatIsNoSequence_HasNoEntries(string block)
    {
        // Arrange
        static (bool Before, bool After) ReadAround(string text)
        {
            var parser = new LeanYamlParser(text);

            try
            {
                _ = parser.ReadKey(out _);
                bool before = parser.ReadEntry(out _);
                _ = parser.ReadValue();

                return (before, parser.ReadEntry(out _));
            }
            finally
            {
                parser.Dispose();
            }
        }

        // Act
        (bool before, bool after) = ReadAround(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(before).IsFalse();
            await Assert.That(after).IsFalse();
        }
    }

    #endregion // Sequence Tests

    #region Skipped Value Tests

    /// <summary>
    ///     Verifies that a value which the caller leaves unread is skipped whatever it holds, also when it carries
    ///     an anchor, a tag or an alias, and that the keys after it are still read with their lines.
    /// </summary>
    [Test]
    [Arguments("titel: Typo\ntitle: Home", "titel@1,title@2")]
    [Arguments("Title: x\ntitle: Home", "Title@1,title@2")]
    [Arguments("a b: c\ntitle: Home", "a b@1,title@2")]
    [Arguments("'ti''tle': x\ntitle: Home", "ti'tle@1,title@2")]
    [Arguments("author: a\nauthor: b\ntitle: Home", "author@1,author@2,title@3")]
    [Arguments("author:\n  name: Ada\ntags: [notes, yaml]\ntitle: Home", "author@1,tags@3,title@4")]
    [Arguments("author: {name: x,\n  url: y}\ntitle: Home", "author@1,title@3")]
    [Arguments("author: {name: O'Brien}\ntitle: Home", "author@1,title@2")]
    [Arguments("author: [it's, \"a\"]\ntitle: Home", "author@1,title@2")]
    [Arguments("author:\n  - x\n  - y\ntitle: Home", "author@1,title@4")]
    [Arguments("author:\n- x\n- y\ntitle: Home", "author@1,title@4")]
    [Arguments("author: |\n  text\n  more\ntitle: Home", "author@1,title@4")]
    [Arguments("author: \"a\n  b\"\ntitle: Home", "author@1,title@3")]
    [Arguments("author: &a x\ntitle: Home", "author@1,title@2")]
    [Arguments("author: !!str x\ntitle: Home", "author@1,title@2")]
    [Arguments("author: *a\ntitle: Home", "author@1,title@2")]
    [Arguments("author: &a\n  name: x\ntitle: Home", "author@1,title@3")]
    [Arguments("author:\n  name: a: b\ntitle: Home", "author@1,title@3")]
    [Arguments("author:\n  deep:\n    deeper: [x\n  other: y\ntitle: Home", "author@1,title@5")]
    [Arguments("author:\n  \"a\\\"b\": x\ntitle: Home", "author@1,title@3")]
    [Arguments("author:\n  'a''b': x\ntitle: Home", "author@1,title@3")]
    [Arguments("author:\n  ? a\n  : b\ntitle: Home", "author@1,title@4")]
    [Arguments("author: [&a x, !!str y, *a]\ntitle: Home", "author@1,title@2")]
    [Arguments("author: [&a , b]\ntitle: Home", "author@1,title@2")]
    [Arguments("author: {\"a\": 'b, c', d: [\"e]\"]}\ntitle: Home", "author@1,title@2")]
    public async Task ReadKey_Test_UnreadValue_IsSkipped(string block, string expected)
    {
        // Act
        string[] readings = [.. LineBreaks.Select(lineBreak => ReadKeys(block.Replace("\n", lineBreak, StringComparison.Ordinal)))];

        // Assert
        using (Assert.Multiple())
        {
            foreach (string keys in readings)
                await Assert.That(keys).IsEqualTo(expected);
        }
    }

    /// <summary>
    ///     Verifies that the entries of a sequence which the caller leaves unread are skipped by the next key.
    /// </summary>
    [Test]
    [Arguments("stylesheets: [a, b, c]\ntitle: Home")]
    [Arguments("stylesheets: [a,\n  b,\n  c]\ntitle: Home")]
    [Arguments("stylesheets:\n  - a\n  - b\n  - c\ntitle: Home")]
    [Arguments("stylesheets:\n- a\n- b\n- c\ntitle: Home")]
    public async Task ReadKey_Test_UnreadEntries_AreSkipped(string block)
    {
        // Arrange
        static (string? Entry, string Key, string? Text) ReadAround(string text)
        {
            var parser = new LeanYamlParser(text);

            try
            {
                _ = parser.ReadKey(out _);
                _ = parser.ReadValue();
                string? entry = parser.ReadEntry(out _) ? parser.Text.ToString() : null;
                string key = parser.ReadKey(out ReadOnlySpan<char> name) ? name.ToString() : string.Empty;

                return (entry, key, parser.ReadValue() is YamlNodeKind.Scalar ? parser.Text.ToString() : null);
            }
            finally
            {
                parser.Dispose();
            }
        }

        // Act
        (string? entry, string key, string? text) = ReadAround(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(entry).IsEqualTo("a");
            await Assert.That(key).IsEqualTo("title");
            await Assert.That(text).IsEqualTo("Home");
        }
    }

    /// <summary>
    ///     Verifies that an anchor, an alias or a tag stops the block on a value that is read, while the same value
    ///     passes when the caller leaves it unread.
    /// </summary>
    [Test]
    [Arguments("title: &a Home", 1)]
    [Arguments("title: !!str 123", 1)]
    [Arguments("title: !custom Home", 1)]
    [Arguments("title: *a", 1)]
    [Arguments("title: &a\n  Home", 1)]
    [Arguments("date: 2026-09-13\ntitle: *a", 2)]
    [Arguments("stylesheets:\n  - &a x.css\n  - *a", 2)]
    [Arguments("stylesheets: [&a x.css]", 1)]
    [Arguments("stylesheets: [*a]", 1)]
    public async Task ReadValue_Test_AnchorAliasOrTag_Throws(string block, int line)
    {
        // Act and Assert
        using (Assert.Multiple())
        {
            foreach (string lineBreak in LineBreaks)
            {
                string text = block.Replace("\n", lineBreak, StringComparison.Ordinal);
                FrontMatterException? exception = await Assert.That(() => ReadEntries(text)).Throws<FrontMatterException>();

                await Assert.That(exception?.Line).IsEqualTo(line);
                await Assert.That(exception?.Reason).IsEqualTo(NoProperties);
                await Assert.That(() => ReadKeys(text)).ThrowsNothing();
            }
        }
    }

    /// <summary>
    ///     Verifies that an anchor, an alias or a tag stops the block on a key, whether its value is read or not.
    /// </summary>
    [Test]
    [Arguments("&a title: Home")]
    [Arguments("*a : Home")]
    [Arguments("!!str title: Home")]
    public async Task ReadKey_Test_AnchorAliasOrTagOnAKey_Throws(string block)
    {
        // Act and Assert
        await AssertFailure(block, 1, NoProperties);
    }

    /// <summary>
    ///     Verifies that only a comment may follow an alias, which the parser reaches on a value that is skipped.
    /// </summary>
    [Test]
    public async Task ReadKey_Test_TextAfterASkippedAlias_Throws()
    {
        // Act
        FrontMatterException? exception = await Assert.That(() => ReadKeys("author: *a b")).Throws<FrontMatterException>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception?.Line).IsEqualTo(1);
            await Assert.That(exception?.Reason).IsEqualTo("text follows the alias");
        }
    }

    #endregion // Skipped Value Tests

    #region Malformed Block Tests

    /// <summary>
    ///     Verifies that a tab stops the block wherever spaces have to indent a line.
    /// </summary>
    [Test]
    [Arguments("\ttitle: Home", 1)]
    [Arguments("title: plain\n\tcontinued", 2)]
    [Arguments("title: |\n\ttext", 2)]
    [Arguments("title: |\n  a\n \tb", 3)]
    [Arguments("author:\n\tname: x", 2)]
    [Arguments("title:\n \t- a", 2)]
    [Arguments("author:\n \tname: x", 2)]
    [Arguments("stylesheets: [a.css,\n\tb.css]", 2)]
    [Arguments("title: \"a\n\tb\"", 2)]
    public async Task ReadKey_Test_TabInIndentation_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "a tab indents it");
    }

    /// <summary>
    ///     Verifies that a line at the first column which is no key and its colon stops the block, as does a
    ///     mapping that is not written in block style at that column.
    /// </summary>
    [Test]
    [Arguments("just a bare sentence", 1)]
    [Arguments("title:Home", 1)]
    [Arguments("- a\n- b", 1)]
    [Arguments("{title: Home}", 1)]
    [Arguments(": x", 1)]
    [Arguments("\"title\":Home", 1)]
    [Arguments("\"title\n  more\": Home", 1)]
    [Arguments("title: Home\nplain", 2)]
    [Arguments("title: plain\ncontinued", 2)]
    [Arguments("date:\n>-\n  2026-09-13", 2)]
    [Arguments("title:\n\"Home\"", 2)]
    [Arguments("stylesheets:\n[a.css]", 2)]
    [Arguments("stylesheets:\n  - a.css\n- b.css", 3)]
    public async Task ReadKey_Test_LineThatIsNoKey_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "it is not a key followed by ': '");
    }

    /// <summary>
    ///     Verifies that a colon which a space or the end of the line follows stops the block inside plain text,
    ///     where YAML would read it as the start of a mapping.
    /// </summary>
    [Test]
    [Arguments("title: a: b", 1)]
    [Arguments("title: Home:", 1)]
    [Arguments("author: a: b", 1)]
    [Arguments("title: Home\n  bad: indent", 2)]
    [Arguments("title: a\n  : b", 2)]
    [Arguments("title:\n  Home\n  a: b", 3)]
    public async Task ReadValue_Test_ColonInsidePlainText_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "': ' inside plain text starts a mapping, so quote the text");
    }

    /// <summary>
    ///     Verifies that plain text cannot start with an indicator of YAML.
    /// </summary>
    [Test]
    [Arguments("title: @home", "@")]
    [Arguments("title: `code`", "`")]
    [Arguments("title: %x", "%")]
    [Arguments("title: ,x", ",")]
    [Arguments("title: ]x", "]")]
    [Arguments("title: }x", "}")]
    [Arguments("title: - a", "- ")]
    [Arguments("title: -", "- ")]
    [Arguments("title: ? a", "? ")]
    [Arguments("title: : a", ": ")]
    [Arguments("@title: Home", "@")]
    [Arguments("title:\n  @home", "@")]
    [Arguments("stylesheets: [@a]", "@")]
    [Arguments("stylesheets: [a, : ]", ": ")]
    public async Task ReadValue_Test_PlainTextThatStartsWithAnIndicator_Throws(string block, string start)
    {
        // Arrange
        int line = block.Contains('\n', StringComparison.Ordinal) ? 2 : 1;

        // Act and Assert
        await AssertFailure(block, line, $"plain text cannot start with '{start}', so quote it");
    }

    /// <summary>
    ///     Verifies that a directive and a complex key stop the block.
    /// </summary>
    [Test]
    [Arguments("? title\n: Home", 1)]
    [Arguments("%YAML 1.2\ntitle: Home", 1)]
    [Arguments("title: Home\n?", 2)]
    public async Task ReadKey_Test_DirectiveOrComplexKey_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "front matter supports no directives or complex keys");
    }

    /// <summary>
    ///     Verifies that a quote or a bracket which is never closed stops the block on the line it opens on, also
    ///     when the closing delimiter of the block comes first.
    /// </summary>
    [Test]
    [Arguments("date: 2026-09-13\ntitle: \"unterminated", "the quote that opens on it is never closed")]
    [Arguments("date: 2026-09-13\ntitle: 'open\n  more", "the quote that opens on it is never closed")]
    [Arguments("date: 2026-09-13\ntitle: \"Fifty\n---", "the quote that opens on it is never closed")]
    [Arguments("date: 2026-09-13\ntitle: [Ten", "the bracket that opens on it is never closed")]
    [Arguments("date: 2026-09-13\ntitle: [Ten\n---", "the bracket that opens on it is never closed")]
    [Arguments("date: 2026-09-13\ntitle: {Ninety\n---", "the bracket that opens on it is never closed")]
    [Arguments("date: 2026-09-13\nstylesheets: [a,\n  b", "the bracket that opens on it is never closed")]
    public async Task ReadValue_Test_UnclosedQuoteOrBracket_ThrowsOnTheLineItOpensOn(string block, string reason)
    {
        // Act and Assert
        await AssertFailure(block, 2, reason);
    }

    /// <summary>
    ///     Verifies that only a comment, which a space or a tab sets apart, may follow a value that ends within its
    ///     line.
    /// </summary>
    [Test]
    [Arguments("title: \"a\" b", "text follows the closing quote")]
    [Arguments("title: \"a\"# c", "text follows the closing quote")]
    [Arguments("title: 'a''b' c", "text follows the closing quote")]
    [Arguments("stylesheets: [a.css] x", "text follows the closing bracket")]
    [Arguments("stylesheets: [a.css]# c", "text follows the closing bracket")]
    [Arguments("title: {a: b} x", "text follows the closing bracket")]
    [Arguments("title: >text", "text follows the block indicator")]
    [Arguments("title: |0", "text follows the block indicator")]
    [Arguments("title: |++", "text follows the block indicator")]
    [Arguments("title: |# c", "text follows the block indicator")]
    public async Task ReadValue_Test_TextAfterAValue_Throws(string block, string reason)
    {
        // Act and Assert
        await AssertFailure(block, 1, reason);
    }

    /// <summary>
    ///     Verifies the rules of a flow sequence: no empty entry, and a comma between two entries.
    /// </summary>
    [Test]
    [Arguments("stylesheets: [a, , b]", "the list holds an empty entry")]
    [Arguments("stylesheets: [,]", "the list holds an empty entry")]
    [Arguments("stylesheets: [\"a\" \"b\"]", "an entry of the list is followed by neither ',' nor ']'")]
    [Arguments("stylesheets: [a [b]]", "an entry of the list is followed by neither ',' nor ']'")]
    public async Task ReadEntry_Test_MalformedFlowSequence_Throws(string block, string reason)
    {
        // Act and Assert
        await AssertFailure(block, 1, reason);
    }

    /// <summary>
    ///     Verifies that a line which continues a value has to be indented, and that an indented line has to
    ///     continue a value.
    /// </summary>
    [Test]
    [Arguments("title: \"multi\nline\"", 2, "it continues a value but is not indented")]
    [Arguments("title: 'multi\nline'", 2, "it continues a value but is not indented")]
    [Arguments("title: \"a\\\n\"", 2, "it continues a value but is not indented")]
    [Arguments("stylesheets: [a.css,\nb.css]", 2, "it continues a value but is not indented")]
    [Arguments("stylesheets: [a.css\n]", 2, "it continues a value but is not indented")]
    [Arguments("stylesheets: [a.css\nb.css]", 2, "it continues a value but is not indented")]
    [Arguments("  title: Home", 1, "it is indented but continues no value")]
    [Arguments("title: Home\n  date: 2026-09-13\n", 2, "': ' inside plain text starts a mapping, so quote the text")]
    [Arguments("title: \"Home\"\n  more", 2, "it is indented but continues no value")]
    [Arguments("title: one\n  # c\n  two", 3, "it is indented but continues no value")]
    [Arguments("title: one # c\n  two", 2, "it is indented but continues no value")]
    [Arguments("title: |\n    a\n  b", 3, "it is indented but continues no value")]
    [Arguments("stylesheets: [a.css]\n  more", 2, "it is indented but continues no value")]
    [Arguments("stylesheets:\n  - \"a\"\n    more", 3, "it is indented but continues no value")]
    [Arguments("stylesheets:\n    - a\n  - b", 3, "it is indented but continues no value")]
    [Arguments("stylesheets:\n  - a.css\n  b.css", 3, "it is indented but continues no value")]
    [Arguments("title: |\n   \n  text", 2, "it is indented further than the text below it")]
    [Arguments("title: |\n  \nauthor: x", 2, "it is indented further than the text below it")]
    public async Task ReadKey_Test_MisplacedIndentation_Throws(string block, int line, string reason)
    {
        // Act and Assert
        await AssertFailure(block, line, reason);
    }

    /// <summary>
    ///     Verifies that a second document stops the block, where a YAML reader would drop its keys in silence.
    /// </summary>
    [Test]
    [Arguments("title: Home\n--- x", 2)]
    [Arguments("title: Home\n... x", 2)]
    [Arguments("title: Home\n...\nextra: x", 3)]
    [Arguments("title: Home\n---\nextra: x", 3)]
    [Arguments("---\ntitle: Home\n---\n\n---", 5)]
    public async Task ReadKey_Test_SecondDocument_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "a second document starts on it");
    }

    /// <summary>
    ///     Verifies that a character outside the printable set of YAML stops the block on its line, and that the
    ///     message names the character.
    /// </summary>
    [Test]
    [Arguments(0x0000)]
    [Arguments(0x0001)]
    [Arguments(0x0008)]
    [Arguments(0x000B)]
    [Arguments(0x001F)]
    [Arguments(0x007F)]
    [Arguments(0x0080)]
    [Arguments(0x0084)]
    [Arguments(0x0086)]
    [Arguments(0x009F)]
    [Arguments(0xD800)]
    [Arguments(0xDC00)]
    [Arguments(0xFFFE)]
    [Arguments(0xFFFF)]
    public async Task ReadKey_Test_UnprintableCharacter_Throws(int code)
    {
        // Arrange
        string block = $"title: Home\r\ndescription: \"a{(char)code}b\"";

        // Act
        FrontMatterException? exception = await Assert.That(() => ReadKeys(block)).Throws<FrontMatterException>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception?.Line).IsEqualTo(2);
            await Assert.That(exception?.Reason).IsEqualTo($"it holds the character U+{code:X4}, which YAML does not allow");
            await Assert.That(exception?.Message).IsEqualTo($"The front matter is malformed on line 2, because it holds the character U+{code:X4}, which YAML does not allow.");
        }
    }

    /// <summary>
    ///     Verifies that every printable character is text: all of ASCII, a pair of surrogates, and the characters
    ///     that YAML 1.1 read as line breaks, which break no line and count as none.
    /// </summary>
    [Test]
    public async Task ReadValue_Test_PrintableCharacters_AreText()
    {
        // Arrange
        string ascii = string.Concat(Enumerable.Range(0x20, 0x5F).Select(static code => (char)code));
        string others = string.Concat((char)0x85, (char)0xA0, (char)0x2028, (char)0x2029, (char)0xFEFF, (char)0xFFFD) + char.ConvertFromUtf32(0x1F600);
        string block = $"title: |\n  x{ascii}\ndescription: a{others}b\ntitel: Typo";

        // Act
        Entry[] entries = ReadEntries(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(entries.Length).IsEqualTo(3);
            await Assert.That(entries[0].Value.Text).IsEqualTo($"x{ascii}\n");
            await Assert.That(entries[1].Value.Text).IsEqualTo($"a{others}b");
            await Assert.That(entries[2].KeyLine).IsEqualTo(4);
        }
    }

    #endregion // Malformed Block Tests

    #region Helpers

    // Reads every key with its value and with every entry of a sequence.
    private static Entry[] ReadEntries(string block)
    {
        List<Entry> entries = [];
        var parser = new LeanYamlParser(block);

        try
        {
            while (parser.ReadKey(out ReadOnlySpan<char> key))
            {
                string name = key.ToString();
                int keyLine = parser.KeyLine;
                YamlNodeKind kind = parser.ReadValue();
                var value = new Node(kind, kind is YamlNodeKind.Scalar ? parser.Text.ToString() : null, parser.ValueLine);
                List<Node> items = [];

                while (parser.ReadEntry(out YamlNodeKind itemKind))
                    items.Add(new Node(itemKind, itemKind is YamlNodeKind.Scalar ? parser.Text.ToString() : null, parser.ValueLine));

                entries.Add(new Entry(name, keyLine, value, items));
            }
        }
        finally
        {
            parser.Dispose();
        }

        return [.. entries];
    }

    // Reads the keys alone, so that the parser skips every value.
    private static string ReadKeys(string block)
    {
        List<string> keys = [];
        var parser = new LeanYamlParser(block);

        try
        {
            while (parser.ReadKey(out ReadOnlySpan<char> key))
                keys.Add($"{key.ToString()}@{parser.KeyLine}");
        }
        finally
        {
            parser.Dispose();
        }

        return string.Join(',', keys);
    }

    private static Entry[][] ReadUnderEachLineBreak(string block) =>
        [.. LineBreaks.Select(lineBreak => ReadEntries(block.Replace("\n", lineBreak, StringComparison.Ordinal)))];

    private static string Describe(Entry[] entries) =>
        string.Join(',', entries.Select(static entry => $"{entry.Key}={entry.Value.Text}"));

    private static string Join(IReadOnlyList<Node> items) => string.Join('|', items.Select(static item => item.Kind switch
    {
        YamlNodeKind.Scalar => item.Text,
        YamlNodeKind.Null => "<null>",
        YamlNodeKind.Sequence => "<sequence>",
        _ => "<mapping>",
    }));

    private static async Task AssertText(string block, string expected)
    {
        using (Assert.Multiple())
        {
            foreach (Entry[] entries in ReadUnderEachLineBreak(block))
            {
                await Assert.That(entries[0].Value.Kind).IsEqualTo(YamlNodeKind.Scalar);
                await Assert.That(entries[0].Value.Text).IsEqualTo(expected);
            }
        }
    }

    // A malformed block fails the same way whether its values are read or skipped.
    private static async Task AssertFailure(string block, int line, string reason)
    {
        using (Assert.Multiple())
        {
            foreach (string lineBreak in LineBreaks)
            {
                string text = block.Replace("\n", lineBreak, StringComparison.Ordinal);
                FrontMatterException? read = await Assert.That(() => ReadEntries(text)).Throws<FrontMatterException>();
                FrontMatterException? skipped = await Assert.That(() => ReadKeys(text)).Throws<FrontMatterException>();

                await Assert.That(read?.Line).IsEqualTo(line);
                await Assert.That(read?.Reason).IsEqualTo(reason);
                await Assert.That(skipped?.Line).IsEqualTo(line);
                await Assert.That(skipped?.Reason).IsEqualTo(reason);
            }
        }
    }

    private sealed record Node(YamlNodeKind Kind, string? Text, int Line);

    private sealed record Entry(string Key, int KeyLine, Node Value, IReadOnlyList<Node> Items);

    #endregion
}
