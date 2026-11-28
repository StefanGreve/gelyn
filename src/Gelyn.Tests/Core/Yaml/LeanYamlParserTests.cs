using System;
using System.Collections.Generic;
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
    ///     Verifies that a key is read as written, without the spaces and tabs before its colon.
    /// </summary>
    [Test]
    [Arguments("title: Home", "title")]
    [Arguments("title : Home", "title")]
    [Arguments("title\t: Home", "title")]
    [Arguments("Title: Home", "Title")]
    [Arguments("a b: c", "a b")]
    [Arguments("a:b: c", "a:b")]
    [Arguments("url#1: c", "url#1")]
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
    ///     Verifies that plain text is read as written: trimmed, ended by a comment, and kept as text when it looks
    ///     like a number, a boolean or a date.
    /// </summary>
    [Test]
    [Arguments("title: Home", "Home")]
    [Arguments("title:   Home \t ", "Home")]
    [Arguments("title:\tHome", "Home")]
    [Arguments("title: 123", "123")]
    [Arguments("title: 1.5", "1.5")]
    [Arguments("title: true", "true")]
    [Arguments("title: nUll", "nUll")]
    [Arguments("title: 2026-09-13", "2026-09-13")]
    [Arguments("title: Notes about software", "Notes about software")]
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
    public async Task ReadValue_Test_PlainText_IsReadAsWritten(string block, string expected)
    {
        // Act and Assert
        await AssertText(block, expected);
    }

    /// <summary>
    ///     Verifies that quoted text keeps what plain text would lose: its blanks, a colon or a hash sign, the
    ///     spelling of null, and an empty string, which stays distinct from an absent value.
    /// </summary>
    [Test]
    [Arguments("title: \"null\"", "null")]
    [Arguments("title: \"\"", "")]
    [Arguments("title: ''", "")]
    [Arguments("title: \"\" # c", "")]
    [Arguments("title: 'a' # c", "a")]
    [Arguments("title: \" a \"", " a ")]
    [Arguments("title: \"a # b\"", "a # b")]
    [Arguments("title: 'a: b'", "a: b")]
    [Arguments("title: \"Part 2: the parser\"", "Part 2: the parser")]
    public async Task ReadValue_Test_QuotedText_IsKeptAsWritten(string block, string expected)
    {
        // Act and Assert
        await AssertText(block, expected);
    }

    /// <summary>
    ///     Verifies the escapes of quoted text: two single quotes stand for one inside single quotes, and a backslash
    ///     stands before a double quote or a backslash inside double quotes, where it is plain text otherwise.
    /// </summary>
    [Test]
    [Arguments("title: 'It''s'", "It's")]
    [Arguments("title: ''''", "'")]
    [Arguments("title: 'a''''b'", "a''b")]
    [Arguments("title: 'C:\\temp'", "C:\\temp")]
    [Arguments("title: 'say \"hi\"'", "say \"hi\"")]
    [Arguments("title: \"A \\\"quoted\\\" word\"", "A \"quoted\" word")]
    [Arguments("title: \"C:\\\\temp\"", "C:\\temp")]
    [Arguments("title: \"\\\\\"", "\\")]
    [Arguments("title: \"it's\"", "it's")]
    public async Task ReadValue_Test_QuotedText_DecodesItsEscapes(string block, string expected)
    {
        // Act and Assert
        await AssertText(block, expected);
    }

    /// <summary>
    ///     Verifies that any other backslash sequence in double quotes stops the block, so that no escape is read
    ///     as one the author did not mean.
    /// </summary>
    [Test]
    [Arguments("t")]
    [Arguments("n")]
    [Arguments("x41")]
    [Arguments("/")]
    [Arguments("q")]
    public async Task ReadValue_Test_OtherEscape_Throws(string escape)
    {
        // Arrange
        string block = $"date: 2026-09-13\ntitle: \"a{Backslash}{escape}b\"";
        string reason = $"'{Backslash}{escape[0]}' is not one of the two escapes, '{Backslash}{Backslash}' and '{Backslash}\"'";

        // Act and Assert
        await AssertFailure(block, 2, reason);
    }

    /// <summary>
    ///     Verifies that an empty value and the plain spellings of null are no text.
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
    ///     Verifies that a value is reported with its kind and with the line it starts on, which for a list below
    ///     its key is the line of the first entry.
    /// </summary>
    [Test]
    [Arguments("title: Home", nameof(YamlNodeKind.Scalar), 1)]
    [Arguments("title: 'Home'", nameof(YamlNodeKind.Scalar), 1)]
    [Arguments("title: [a, b]", nameof(YamlNodeKind.Sequence), 1)]
    [Arguments("title:\n  - a", nameof(YamlNodeKind.Sequence), 2)]
    [Arguments("title:\n- a", nameof(YamlNodeKind.Sequence), 2)]
    [Arguments("title:\n\n  # c\n  - a", nameof(YamlNodeKind.Sequence), 4)]
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
            YamlNodeKind before = parser.ReadValue();
            _ = parser.ReadKey(out _);

            return (before, parser.ReadValue(), parser.ReadValue());
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

    /// <summary>
    ///     Verifies that printable ASCII and text in other scripts are kept as written.
    /// </summary>
    [Test]
    public async Task ReadValue_Test_EveryPrintableCharacter_IsText()
    {
        // Arrange
        string ascii = string.Concat(Enumerable.Range(0x20, 0x5F).Select(static code => (char)code));
        string others = string.Concat((char)0xE9, (char)0x65E5, (char)0x672C) + char.ConvertFromUtf32(0x1F600);
        string block = $"title: x{ascii}\ndescription: a{others}b\ntitel: Typo";

        // Act
        Entry[] entries = ReadEntries(block);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(entries.Length).IsEqualTo(3);
            await Assert.That(entries[0].Value.Text).IsEqualTo($"x{ascii}");
            await Assert.That(entries[1].Value.Text).IsEqualTo($"a{others}b");
            await Assert.That(entries[2].KeyLine).IsEqualTo(3);
        }
    }

    #endregion // Scalar Tests

    #region List Tests

    /// <summary>
    ///     Verifies that a list is read in brackets on one line and with one entry to a line: with entries that are
    ///     plain, quoted or null, a trailing comma, comments, and dashes at the column of the key or further in.
    /// </summary>
    [Test]
    [Arguments("stylesheets: [a.css, b.css]", "a.css|b.css")]
    [Arguments("stylesheets: [ a.css ,b.css ]", "a.css|b.css")]
    [Arguments("stylesheets: [a.css, ]", "a.css")]
    [Arguments("stylesheets: []", "")]
    [Arguments("stylesheets: [ ]", "")]
    [Arguments("stylesheets: [\"a b.css\", 'c.css']", "a b.css|c.css")]
    [Arguments("stylesheets: [\"a, b.css\", 'c].css']", "a, b.css|c].css")]
    [Arguments("stylesheets: [~, a.css]", "<null>|a.css")]
    [Arguments("stylesheets: [a b, c]", "a b|c")]
    [Arguments("stylesheets: [a:b]", "a:b")]
    [Arguments("stylesheets: [it's.css]", "it's.css")]
    [Arguments("stylesheets: [a.css] # c", "a.css")]
    [Arguments("stylesheets:\n- a.css\n- b.css", "a.css|b.css")]
    [Arguments("stylesheets:\n  - a.css\n  - b.css", "a.css|b.css")]
    [Arguments("stylesheets: # c\n  - a.css", "a.css")]
    [Arguments("stylesheets:\n# c\n  - a.css", "a.css")]
    [Arguments("stylesheets:\n  -\n  - a.css", "<null>|a.css")]
    [Arguments("stylesheets:\n  - ~\n  - a.css", "<null>|a.css")]
    [Arguments("stylesheets:\n  - # c\n  - a.css", "<null>|a.css")]
    [Arguments("stylesheets:\n  -\ta.css", "a.css")]
    [Arguments("stylesheets:\n  - a.css\n  # c\n  - b.css", "a.css|b.css")]
    [Arguments("stylesheets:\n  - a.css\n\n  - b.css", "a.css|b.css")]
    [Arguments("stylesheets:\n  - a.css # c\n  - 'b.css' # c", "a.css|b.css")]
    [Arguments("stylesheets:\n  - \"a: b.css\"", "a: b.css")]
    public async Task ReadEntry_Test_List_IsReadInBothForms(string block, string expected)
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
    ///     Verifies that a list whose dashes sit at the column of its key ends where the next key begins.
    /// </summary>
    [Test]
    public async Task ReadEntry_Test_ListAtTheColumnOfItsKey_EndsAtTheNextKey()
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
    ///     Verifies that an entry is reported with the line it sits on.
    /// </summary>
    [Test]
    public async Task ReadEntry_Test_Entry_IsReportedWithItsLine()
    {
        // Act
        Entry[] entries = ReadEntries("title: Home\nstylesheets:\n  - a.css\n\n  - b.css\nscripts: [a.js, b.js]");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(string.Join(',', entries[1].Items.Select(static item => item.Line))).IsEqualTo("3,5");
            await Assert.That(string.Join(',', entries[2].Items.Select(static item => item.Line))).IsEqualTo("6,6");
        }
    }

    /// <summary>
    ///     Verifies that a value which is no list has no entries, before and after it is read.
    /// </summary>
    [Test]
    [Arguments("title: Home")]
    [Arguments("title:")]
    public async Task ReadEntry_Test_ValueThatIsNoList_HasNoEntries(string block)
    {
        // Arrange
        static (bool Before, bool After) ReadAround(string text)
        {
            var parser = new LeanYamlParser(text);
            _ = parser.ReadKey(out _);
            bool before = parser.ReadEntry(out _);
            _ = parser.ReadValue();

            return (before, parser.ReadEntry(out _));
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

    #endregion // List Tests

    #region Unread Value Tests

    /// <summary>
    ///     Verifies that a value which the caller leaves unread is passed over, and that the keys after it are still
    ///     read with their lines.
    /// </summary>
    [Test]
    [Arguments("titel: Typo\ntitle: Home", "titel@1,title@2")]
    [Arguments("Title: x\ntitle: Home", "Title@1,title@2")]
    [Arguments("a b: c\ntitle: Home", "a b@1,title@2")]
    [Arguments("author:\ntitle: Home", "author@1,title@2")]
    [Arguments("author: a\nauthor: b\ntitle: Home", "author@1,author@2,title@3")]
    [Arguments("author: \"a: b\"\ntitle: Home", "author@1,title@2")]
    [Arguments("tags: [notes, yaml]\ntitle: Home", "tags@1,title@2")]
    [Arguments("tags:\n  - x\n  - y\ntitle: Home", "tags@1,title@4")]
    [Arguments("tags:\n- x\n- y\ntitle: Home", "tags@1,title@4")]
    public async Task ReadKey_Test_UnreadValue_IsPassedOver(string block, string expected)
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
    ///     Verifies that the entries of a list which the caller leaves unread are passed over by the next key.
    /// </summary>
    [Test]
    [Arguments("stylesheets: [a, b, c]\ntitle: Home")]
    [Arguments("stylesheets:\n  - a\n  - b\n  - c\ntitle: Home")]
    [Arguments("stylesheets:\n- a\n- b\n- c\ntitle: Home")]
    public async Task ReadKey_Test_UnreadEntries_ArePassedOver(string block)
    {
        // Arrange
        static (string? Entry, string Key, string? Text) ReadAround(string text)
        {
            var parser = new LeanYamlParser(text);
            _ = parser.ReadKey(out _);
            _ = parser.ReadValue();
            string? entry = parser.ReadEntry(out _) ? parser.Text.ToString() : null;
            string key = parser.ReadKey(out ReadOnlySpan<char> name) ? name.ToString() : string.Empty;

            return (entry, key, parser.ReadValue() is YamlNodeKind.Scalar ? parser.Text.ToString() : null);
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

    #endregion // Unread Value Tests

    #region Malformed Block Tests

    /// <summary>
    ///     Verifies that a tab stops the block where spaces have to indent a line.
    /// </summary>
    [Test]
    [Arguments("\ttitle: Home", 1)]
    [Arguments("title: Home\n\tdate: 2026-09-13", 2)]
    [Arguments("stylesheets:\n\t- a.css", 2)]
    [Arguments("stylesheets:\n  - a.css\n\t- b.css", 3)]
    public async Task ReadKey_Test_TabInIndentation_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "a tab indents it");
    }

    /// <summary>
    ///     Verifies that an indented line stops the block unless it is an entry of a list, since a value ends with
    ///     its line: text does not continue below, and neither a value nor a mapping sits below its key.
    /// </summary>
    [Test]
    [Arguments("  title: Home", 1)]
    [Arguments("title: plain\n  continued", 2)]
    [Arguments("title: one\n\n  two", 3)]
    [Arguments("title: Home\n  # c\n  two", 3)]
    [Arguments("title: \"Home\"\n  more", 2)]
    [Arguments("title:\n  Home", 2)]
    [Arguments("title: # c\n  Home", 2)]
    [Arguments("author:\n  name: Ada", 2)]
    [Arguments("stylesheets: [a.css]\n  more", 2)]
    [Arguments("stylesheets:\n  - a.css\n    more", 3)]
    [Arguments("stylesheets:\n  - a.css\n  b.css", 3)]
    [Arguments("stylesheets:\n  - a.css\n   - b.css", 3)]
    [Arguments("stylesheets:\n    - a\n  - b", 3)]
    [Arguments("stylesheets:\n  -\n    a.css", 3)]
    public async Task ReadKey_Test_IndentedLine_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "it is indented, and a value ends with its line");
    }

    /// <summary>
    ///     Verifies that block text stops the block, on the line of its indicator.
    /// </summary>
    [Test]
    [Arguments("title: |", 1)]
    [Arguments("title: >", 1)]
    [Arguments("title: |-", 1)]
    [Arguments("title: >+2", 1)]
    [Arguments("description: |\n  Who I am\n  and what I write", 1)]
    [Arguments("stylesheets:\n  - |\n    a.css", 2)]
    public async Task ReadValue_Test_BlockText_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "front matter holds no block text, so write the text on one line");
    }

    /// <summary>
    ///     Verifies that a line at the first column which is no key and its colon stops the block.
    /// </summary>
    [Test]
    [Arguments("just a bare sentence", 1)]
    [Arguments("title # note: x", 1)]
    [Arguments("title:Home", 1)]
    [Arguments("- a\n- b", 1)]
    [Arguments("- a: b", 1)]
    [Arguments("? title\n: Home", 1)]
    [Arguments("? title: Home", 1)]
    [Arguments(": x", 1)]
    [Arguments("{title: Home}", 1)]
    [Arguments("%YAML 1.2\ntitle: Home", 1)]
    [Arguments("title: Home\nplain", 2)]
    [Arguments("title: Home\n?", 2)]
    [Arguments("stylesheets:\n[a.css]", 2)]
    [Arguments("stylesheets:\n  - a.css\n- b.css", 3)]
    public async Task ReadKey_Test_LineThatIsNoKey_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "it is not a key followed by ': '");
    }

    /// <summary>
    ///     Verifies that a key in quotes stops the block.
    /// </summary>
    [Test]
    [Arguments("\"title\": Home", 1)]
    [Arguments("'title': Home", 1)]
    [Arguments("title:\n\"Home\"", 2)]
    public async Task ReadKey_Test_QuotedKey_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "a key is written without quotes");
    }

    /// <summary>
    ///     Verifies that an anchor, an alias or a tag stops the block, on a key and on any value.
    /// </summary>
    [Test]
    [Arguments("&a title: Home", 1)]
    [Arguments("*a : Home", 1)]
    [Arguments("!!str title: Home", 1)]
    [Arguments("title: &a Home", 1)]
    [Arguments("title: !!str 123", 1)]
    [Arguments("title: !custom Home", 1)]
    [Arguments("title: *a", 1)]
    [Arguments("author: &a x", 1)]
    [Arguments("date: 2026-09-13\ntitle: *a", 2)]
    [Arguments("stylesheets:\n  - &a x.css", 2)]
    [Arguments("stylesheets: [&a x.css]", 1)]
    [Arguments("stylesheets: [*a]", 1)]
    public async Task ReadKey_Test_AnchorAliasOrTag_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "front matter supports no anchors, aliases or tags");
    }

    /// <summary>
    ///     Verifies that a colon which a blank or the end of the line follows stops the block inside plain text,
    ///     where YAML would read it as the start of a mapping.
    /// </summary>
    [Test]
    [Arguments("title: a: b", 1)]
    [Arguments("title: Home:", 1)]
    [Arguments("title: Part 2: the parser", 1)]
    [Arguments("author: a: b", 1)]
    [Arguments("stylesheets:\n  - a: b", 2)]
    [Arguments("stylesheets: [a: b]", 1)]
    [Arguments("stylesheets: [a, b: ]", 1)]
    [Arguments("stylesheets: [a:]", 1)]
    public async Task ReadValue_Test_ColonInsidePlainText_Throws(string block, int line)
    {
        // Act and Assert
        await AssertFailure(block, line, "': ' inside plain text starts a mapping, so quote the text");
    }

    /// <summary>
    ///     Verifies that plain text cannot start with an indicator of YAML, which also keeps a mapping and a nested
    ///     list out of a value.
    /// </summary>
    [Test]
    [Arguments("title: @home", "@")]
    [Arguments("title: `code`", "`")]
    [Arguments("title: %x", "%")]
    [Arguments("title: ,x", ",")]
    [Arguments("title: ]x", "]")]
    [Arguments("title: }x", "}")]
    [Arguments("title: {a: b}", "{")]
    [Arguments("title: - a", "- ")]
    [Arguments("title: -", "- ")]
    [Arguments("title: ? a", "? ")]
    [Arguments("title: : a", ": ")]
    [Arguments("stylesheets: [@a]", "@")]
    [Arguments("stylesheets: [#a]", "#")]
    [Arguments("stylesheets: [|a]", "|")]
    [Arguments("stylesheets: [a, [b]]", "[")]
    [Arguments("stylesheets: [a, {b: c}]", "{")]
    [Arguments("stylesheets: [a, : ]", ": ")]
    [Arguments("stylesheets:\n  - [nested]", "[")]
    [Arguments("stylesheets:\n  - {a: b}", "{")]
    [Arguments("stylesheets:\n  - - b", "- ")]
    public async Task ReadValue_Test_PlainTextThatStartsWithAnIndicator_Throws(string block, string start)
    {
        // Arrange
        int line = block.Contains('\n', StringComparison.Ordinal) ? 2 : 1;

        // Act and Assert
        await AssertFailure(block, line, $"plain text cannot start with '{start}', so quote it");
    }

    /// <summary>
    ///     Verifies that a quote or a bracket stops the block when it is not closed on the line it opens on.
    /// </summary>
    [Test]
    [Arguments("date: 2026-09-13\ntitle: \"unterminated", "the quote that opens on it is not closed on the same line")]
    [Arguments("date: 2026-09-13\ntitle: 'open\n  more'", "the quote that opens on it is not closed on the same line")]
    [Arguments("date: 2026-09-13\ntitle: \"multi\n  line\"", "the quote that opens on it is not closed on the same line")]
    [Arguments("date: 2026-09-13\ntitle: \"a\\\"", "the quote that opens on it is not closed on the same line")]
    [Arguments("date: 2026-09-13\ntitle: \"a\\", "the quote that opens on it is not closed on the same line")]
    [Arguments("date: 2026-09-13\ntitle: 'a''", "the quote that opens on it is not closed on the same line")]
    [Arguments("date: 2026-09-13\ntitle: [Ten", "the bracket that opens on it is not closed on the same line")]
    [Arguments("date: 2026-09-13\ntitle: [", "the bracket that opens on it is not closed on the same line")]
    [Arguments("date: 2026-09-13\nstylesheets: [a.css,\n  b.css]", "the bracket that opens on it is not closed on the same line")]
    [Arguments("date: 2026-09-13\nstylesheets: [a.css, # c", "the bracket that opens on it is not closed on the same line")]
    public async Task ReadValue_Test_UnclosedQuoteOrBracket_Throws(string block, string reason)
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
    public async Task ReadValue_Test_TextAfterAValue_Throws(string block, string reason)
    {
        // Act and Assert
        await AssertFailure(block, 1, reason);
    }

    /// <summary>
    ///     Verifies the rules of a list in brackets: no empty entry, and a comma between two entries.
    /// </summary>
    [Test]
    [Arguments("stylesheets: [a, , b]", "the list holds an empty entry")]
    [Arguments("stylesheets: [,]", "the list holds an empty entry")]
    [Arguments("stylesheets: [\"a\" \"b\"]", "an entry of the list is followed by neither ',' nor ']'")]
    [Arguments("stylesheets: [a [b]]", "an entry of the list is followed by neither ',' nor ']'")]
    [Arguments("stylesheets: [a}", "an entry of the list is followed by neither ',' nor ']'")]
    public async Task ReadEntry_Test_MalformedListInBrackets_Throws(string block, string reason)
    {
        // Act and Assert
        await AssertFailure(block, 1, reason);
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
    ///     Verifies the message of the exception, which names the line and the reason.
    /// </summary>
    [Test]
    public async Task ReadKey_Test_MalformedBlock_NamesTheLineAndTheReason()
    {
        // Act
        FrontMatterException? exception = await Assert.That(() => ReadKeys("title: Home\r\n\ttitle: Home")).Throws<FrontMatterException>();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exception?.Line).IsEqualTo(2);
            await Assert.That(exception?.Reason).IsEqualTo("a tab indents it");
            await Assert.That(exception?.Message).IsEqualTo("The front matter is malformed on line 2, because a tab indents it.");
        }
    }

    #endregion // Malformed Block Tests

    #region Helpers

    // Reads every key with its value and with every entry of a list.
    private static Entry[] ReadEntries(string block)
    {
        List<Entry> entries = [];
        var parser = new LeanYamlParser(block);

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

        return [.. entries];
    }

    // Reads the keys alone, so that the parser reads every value by itself.
    private static string ReadKeys(string block)
    {
        List<string> keys = [];
        var parser = new LeanYamlParser(block);

        while (parser.ReadKey(out ReadOnlySpan<char> key))
            keys.Add($"{key.ToString()}@{parser.KeyLine}");

        return string.Join(',', keys);
    }

    private static Entry[][] ReadUnderEachLineBreak(string block) =>
        [.. LineBreaks.Select(lineBreak => ReadEntries(block.Replace("\n", lineBreak, StringComparison.Ordinal)))];

    private static string Describe(Entry[] entries) =>
        string.Join(',', entries.Select(static entry => $"{entry.Key}={entry.Value.Text}"));

    private static string Join(IReadOnlyList<Node> items) =>
        string.Join('|', items.Select(static item => item.Kind is YamlNodeKind.Scalar ? item.Text : "<null>"));

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

    // A malformed block fails the same way whether the caller reads its values or leaves them to the parser.
    private static async Task AssertFailure(string block, int line, string reason)
    {
        using (Assert.Multiple())
        {
            foreach (string lineBreak in LineBreaks)
            {
                string text = block.Replace("\n", lineBreak, StringComparison.Ordinal);
                FrontMatterException? read = await Assert.That(() => ReadEntries(text)).Throws<FrontMatterException>();
                FrontMatterException? unread = await Assert.That(() => ReadKeys(text)).Throws<FrontMatterException>();

                await Assert.That(read?.Line).IsEqualTo(line);
                await Assert.That(read?.Reason).IsEqualTo(reason);
                await Assert.That(unread?.Line).IsEqualTo(line);
                await Assert.That(unread?.Reason).IsEqualTo(reason);
            }
        }
    }

    private sealed record Node(YamlNodeKind Kind, string? Text, int Line);

    private sealed record Entry(string Key, int KeyLine, Node Value, IReadOnlyList<Node> Items);

    #endregion
}
