using System;
using System.Threading.Tasks;

using Gelyn.Core.Internals;

namespace Gelyn.Tests.Core;

/// <summary>
///     Unit tests for the <see cref="StringUtils"/> class.
/// </summary>
public class StringUtilsTests
{
    #region EnsureEndsOn Tests

    /// <summary>
    ///     Verifies that a value is left with exactly one terminator, whether it arrived with none, with one
    ///     or with several.
    /// </summary>
    [Test]
    [Arguments("https://example.com/blog", "https://example.com/blog/")]
    [Arguments("https://example.com/blog/", "https://example.com/blog/")]
    [Arguments("https://example.com/blog//", "https://example.com/blog/")]
    [Arguments("https://example.com/blog///", "https://example.com/blog/")]
    public async Task EnsureEndsOn_Test_Value_EndsOnExactlyOneTerminator(string value, string expected)
    {
        // Act
        string result = StringUtils.EnsureEndsOn(value, '/');

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a value consisting only of terminators collapses to one, and that an empty value
    ///     becomes the terminator alone.
    /// </summary>
    [Test]
    [Arguments("", "/")]
    [Arguments("/", "/")]
    [Arguments("//", "/")]
    public async Task EnsureEndsOn_Test_ValueWithoutABody_IsTheTerminatorAlone(string value, string expected)
    {
        // Act
        string result = StringUtils.EnsureEndsOn(value, '/');

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a slice of a larger value is terminated on its own terms, the characters around it
    ///     reaching neither the trim nor the result.
    /// </summary>
    [Test]
    public async Task EnsureEndsOn_Test_Slice_IsTerminatedWithoutItsSurroundings()
    {
        // Arrange
        const string prefix = "see ";
        const string slice = "https://example.com/blog//";
        const string sentence = $"{prefix}{slice} for more";

        // Act
        string result = StringUtils.EnsureEndsOn(sentence.AsSpan(prefix.Length, slice.Length), '/');

        // Assert
        await Assert.That(result).IsEqualTo("https://example.com/blog/");
    }

    /// <summary>
    ///     Verifies that a terminator occurring inside the value is left where it is, only the run at the end
    ///     being collapsed.
    /// </summary>
    [Test]
    public async Task EnsureEndsOn_Test_InteriorTerminators_AreUntouched()
    {
        // Act
        string result = StringUtils.EnsureEndsOn("a//b//c", '/');

        // Assert
        await Assert.That(result).IsEqualTo("a//b//c/");
    }

    /// <summary>
    ///     Verifies that any character serves as the terminator, the path separator among them.
    /// </summary>
    [Test]
    [Arguments("blog", '\\', "blog\\")]
    [Arguments("blog", '/', "blog/")]
    [Arguments("item", ',', "item,")]
    public async Task EnsureEndsOn_Test_Terminator_IsNotRestrictedToASlash(
        string value,
        char terminator,
        string expected)
    {
        // Act
        string result = StringUtils.EnsureEndsOn(value, terminator);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a value is terminated correctly on either side of the stack buffer's limit, the
    ///     lengths past it renting a pooled array whose own length is only ever at least what was asked for.
    /// </summary>
    [Test]
    [Arguments(255)]
    [Arguments(256)]
    [Arguments(257)]
    [Arguments(1_024)]
    public async Task EnsureEndsOn_Test_ValueBeyondTheStackBuffer_IsStillTerminated(int bodyLength)
    {
        // Arrange
        string value = new('a', bodyLength);

        // Act
        string result = StringUtils.EnsureEndsOn(value, '/');

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result).Length().IsEqualTo(bodyLength + 1);
            await Assert.That(result).IsEqualTo(value + '/');
        }
    }

    /// <summary>
    ///     Verifies that a default span is the empty one, so that a null string reaching the parameter through
    ///     its implicit conversion yields the terminator and throws nothing.
    /// </summary>
    [Test]
    public async Task EnsureEndsOn_Test_DefaultSpan_YieldsTheTerminator()
    {
        // Act
        string result = StringUtils.EnsureEndsOn(default, '/');

        // Assert
        await Assert.That(result).IsEqualTo("/");
    }

    #endregion // EnsureEndsOn Tests

    #region StartsWithSegment Tests

    /// <summary>
    ///     Verifies that a value sitting below the prefix is accepted, while a sibling spelled as an extension
    ///     of it is not.
    /// </summary>
    [Test]
    [Arguments("blog/post.html", "blog", true)]
    [Arguments("blog/a/b.html", "blog", true)]
    [Arguments("blogs/post.html", "blog", false)]
    [Arguments("blog.html", "blog", false)]
    public async Task StartsWithSegment_Test_Sibling_IsNotMistakenForADescendant(
        string value,
        string prefix,
        bool expected)
    {
        // Act
        bool result = StringUtils.StartsWithSegment(value, prefix, '/', StringComparison.Ordinal);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that the prefix on its own is rejected, there being no separator to follow it, and that
    ///     the prefix carrying one is accepted.
    /// </summary>
    [Test]
    [Arguments("blog", false)]
    [Arguments("blog/", true)]
    public async Task StartsWithSegment_Test_PrefixItself_NeedsTheSeparatorToMatch(string value, bool expected)
    {
        // Act
        bool result = StringUtils.StartsWithSegment(value, "blog", '/', StringComparison.Ordinal);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a trailing run of separators on the prefix is disregarded, so that a caller need not
    ///     normalize it first.
    /// </summary>
    [Test]
    [Arguments("blog")]
    [Arguments("blog/")]
    [Arguments("blog//")]
    [Arguments("blog///")]
    public async Task StartsWithSegment_Test_TrailingSeparatorsOnThePrefix_AreDisregarded(string prefix)
    {
        // Act
        bool result = StringUtils.StartsWithSegment("blog/post.html", prefix, '/', StringComparison.Ordinal);

        // Assert
        await Assert.That(result).IsTrue();
    }

    /// <summary>
    ///     Verifies that an empty prefix reduces the test to the separator alone, which is what a value rooted
    ///     at the separator carries and a relative one does not.
    /// </summary>
    [Test]
    [Arguments("/etc/hosts", true)]
    [Arguments("/", true)]
    [Arguments("etc/hosts", false)]
    [Arguments("", false)]
    public async Task StartsWithSegment_Test_EmptyPrefix_IsTheSeparatorAlone(string value, bool expected)
    {
        // Act
        bool result = StringUtils.StartsWithSegment(value, string.Empty, '/', StringComparison.Ordinal);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that an empty value begins with nothing, a prefix shorter than it included.
    /// </summary>
    [Test]
    public async Task StartsWithSegment_Test_EmptyValue_BeginsWithNothing()
    {
        // Act
        bool result = StringUtils.StartsWithSegment(default, "blog", '/', StringComparison.Ordinal);

        // Assert
        await Assert.That(result).IsFalse();
    }

    /// <summary>
    ///     Verifies that the comparison decides whether a prefix spelled in another case matches, which is
    ///     what keeps a directional test exact and an overlap test lenient.
    /// </summary>
    [Test]
    [Arguments(StringComparison.Ordinal, false)]
    [Arguments(StringComparison.OrdinalIgnoreCase, true)]
    public async Task StartsWithSegment_Test_Comparison_DecidesTheCaseOfThePrefix(
        StringComparison comparison,
        bool expected)
    {
        // Act
        bool result = StringUtils.StartsWithSegment("BLOG/post.html", "blog", '/', comparison);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that any character serves as the separator, the path separator among them.
    /// </summary>
    [Test]
    [Arguments("blog\\post.html", '\\', true)]
    [Arguments("blog\\post.html", '/', false)]
    [Arguments("blog,post.html", ',', true)]
    public async Task StartsWithSegment_Test_Separator_IsNotRestrictedToASlash(
        string value,
        char separator,
        bool expected)
    {
        // Act
        bool result = StringUtils.StartsWithSegment(value, "blog", separator, StringComparison.Ordinal);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a slice of a larger value is tested on its own terms, the characters around it
    ///     reaching neither the prefix test nor the separator test.
    /// </summary>
    [Test]
    public async Task StartsWithSegment_Test_Slice_IsTestedWithoutItsSurroundings()
    {
        // Arrange
        const string prefix = "see ";
        const string slice = "blog/post.html";
        const string sentence = $"{prefix}{slice} for more";

        // Act
        bool result = StringUtils.StartsWithSegment(
            sentence.AsSpan(prefix.Length, slice.Length),
            "blog",
            '/',
            StringComparison.Ordinal);

        // Assert
        await Assert.That(result).IsTrue();
    }

    #endregion // StartsWithSegment Tests

    #region JoinSegments Tests

    /// <summary>
    ///     Verifies that the prefix, the segments and the final segment are each separated from the one
    ///     before it by exactly one separator.
    /// </summary>
    [Test]
    public async Task JoinSegments_Test_Segments_AreSeparatedOneFromTheNext()
    {
        // Act
        string result = StringUtils.JoinSegments("/gelyn", ["blog", "2026"], "post.html", '/');

        // Assert
        await Assert.That(result).IsEqualTo("/gelyn/blog/2026/post.html");
    }

    /// <summary>
    ///     Verifies that an empty segment list leaves the prefix and the final segment adjacent, which is the
    ///     shape a landing page takes.
    /// </summary>
    [Test]
    [Arguments("/gelyn", "index.html", "/gelyn/index.html")]
    [Arguments("", "index.html", "/index.html")]
    public async Task JoinSegments_Test_EmptySegmentList_LeavesThePrefixBesideTheLast(
        string prefix,
        string last,
        string expected)
    {
        // Act
        string result = StringUtils.JoinSegments(prefix, [], last, '/');

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that an empty prefix yields a root-relative value, which is what a site served from the
    ///     root of a domain publishes.
    /// </summary>
    [Test]
    public async Task JoinSegments_Test_EmptyPrefix_YieldsARootRelativeValue()
    {
        // Act
        string result = StringUtils.JoinSegments(default, ["blog"], "post.html", '/');

        // Assert
        await Assert.That(result).IsEqualTo("/blog/post.html");
    }

    /// <summary>
    ///     Verifies that a prefix already ending on the separator yields a doubled one, the prefix reaching
    ///     the result verbatim.
    /// </summary>
    [Test]
    public async Task JoinSegments_Test_PrefixEndingOnTheSeparator_IsTakenVerbatim()
    {
        // Act
        string result = StringUtils.JoinSegments("/gelyn/", [], "index.html", '/');

        // Assert
        await Assert.That(result).IsEqualTo("/gelyn//index.html");
    }

    /// <summary>
    ///     Verifies that any character serves as the separator, the path separator among them.
    /// </summary>
    [Test]
    [Arguments('/', "root/blog/post.html")]
    [Arguments('\\', "root\\blog\\post.html")]
    [Arguments(',', "root,blog,post.html")]
    public async Task JoinSegments_Test_Separator_IsNotRestrictedToASlash(char separator, string expected)
    {
        // Act
        string result = StringUtils.JoinSegments("root", ["blog"], "post.html", separator);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a value is joined correctly on either side of the stack buffer's limit, the lengths
    ///     past it renting a pooled array whose own length is only ever at least what was asked for.
    /// </summary>
    [Test]
    [Arguments(1)]
    [Arguments(84)]
    [Arguments(85)]
    [Arguments(300)]
    public async Task JoinSegments_Test_ValueBeyondTheStackBuffer_IsStillJoined(int segmentLength)
    {
        // Arrange
        string segment = new('a', segmentLength);
        string last = new('b', segmentLength);

        // Act
        string result = StringUtils.JoinSegments(default, [segment, segment], last, '/');

        // Assert
        await Assert.That(result).IsEqualTo($"/{segment}/{segment}/{last}");
    }

    /// <summary>
    ///     Verifies that a slice of a larger prefix is joined on its own terms, the characters around it
    ///     reaching neither the length nor the result.
    /// </summary>
    [Test]
    public async Task JoinSegments_Test_PrefixSlice_IsJoinedWithoutItsSurroundings()
    {
        // Arrange
        const string lead = "see ";
        const string slice = "/gelyn";
        const string sentence = $"{lead}{slice} for more";

        // Act
        string result = StringUtils.JoinSegments(
            sentence.AsSpan(lead.Length, slice.Length),
            ["blog"],
            "post.html",
            '/');

        // Assert
        await Assert.That(result).IsEqualTo("/gelyn/blog/post.html");
    }

    #endregion // JoinSegments Tests

    #region TrimDelimiters Tests

    /// <summary>
    ///     Verifies that the delimiter is removed from both ends, leaving what sat between them.
    /// </summary>
    [Test]
    public async Task TrimDelimiters_Test_BothEnds_AreRemoved()
    {
        // Act
        ReadOnlySpan<char> result = StringUtils.TrimDelimiters("---\ntitle: Home\n---", "---");

        // Assert
        await Assert.That(result.ToString()).IsEqualTo("\ntitle: Home");
    }

    /// <summary>
    ///     Verifies that a text ending on the delimiter's own characters keeps them, the trailing delimiter
    ///     counting only as a line of its own.
    /// </summary>
    [Test]
    [Arguments("title: a---")]
    [Arguments("title: a--- ")]
    public async Task TrimDelimiters_Test_TextEndingOnTheCharacters_KeepsThem(string text)
    {
        // Act
        ReadOnlySpan<char> result = StringUtils.TrimDelimiters(text, "---");

        // Assert
        await Assert.That(result.ToString()).IsEqualTo("title: a---");
    }

    /// <summary>
    ///     Verifies that an indented trailing delimiter is removed along with its indentation, which is the
    ///     shape a block written as a raw string literal takes.
    /// </summary>
    [Test]
    public async Task TrimDelimiters_Test_IndentedTrailingDelimiter_IsRemovedWithItsIndentation()
    {
        // Act
        ReadOnlySpan<char> result = StringUtils.TrimDelimiters("    ---\n    title: Home\n    ---", "---");

        // Assert
        await Assert.That(result.ToString()).IsEqualTo("\n    title: Home");
    }

    /// <summary>
    ///     Verifies that the delimiter is recognized regardless of the newline convention the text uses.
    /// </summary>
    [Test]
    [Arguments("---\ntitle: Home\n---", "\ntitle: Home")]
    [Arguments("---\r\ntitle: Home\r\n---", "\r\ntitle: Home\r")]
    [Arguments("---\rtitle: Home\r---", "\rtitle: Home")]
    public async Task TrimDelimiters_Test_NewlineConventions_AreAllRecognized(string text, string expected)
    {
        // Act
        ReadOnlySpan<char> result = StringUtils.TrimDelimiters(text, "---");

        // Assert
        await Assert.That(result.ToString()).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a delimiter sitting between the two ends is left where it is, only the ends being
    ///     matched.
    /// </summary>
    [Test]
    public async Task TrimDelimiters_Test_InteriorDelimiter_IsLeftWhereItIs()
    {
        // Act
        ReadOnlySpan<char> result = StringUtils.TrimDelimiters("---\na: 1\n---\nb: 2\n---", "---");

        // Assert
        await Assert.That(result.ToString()).IsEqualTo("\na: 1\n---\nb: 2");
    }

    /// <summary>
    ///     Verifies that text carrying no delimiter is returned with its surrounding whitespace removed and
    ///     nothing else taken.
    /// </summary>
    [Test]
    [Arguments("title: Home", "title: Home")]
    [Arguments("  title: Home  ", "title: Home")]
    [Arguments("\n\ntitle: Home\n\n", "title: Home")]
    public async Task TrimDelimiters_Test_TextWithoutADelimiter_IsOnlyTrimmed(string text, string expected)
    {
        // Act
        ReadOnlySpan<char> result = StringUtils.TrimDelimiters(text, "---");

        // Assert
        await Assert.That(result.ToString()).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that text holding nothing but delimiters yields an empty span.
    /// </summary>
    [Test]
    [Arguments("---")]
    [Arguments("---\n---")]
    [Arguments("  ---\n---  ")]
    [Arguments("")]
    public async Task TrimDelimiters_Test_NothingButDelimiters_YieldsAnEmptySpan(string text)
    {
        // Act
        ReadOnlySpan<char> result = StringUtils.TrimDelimiters(text, "---");

        // Assert
        await Assert.That(result.IsEmpty).IsTrue();
    }

    /// <summary>
    ///     Verifies that the whitespace around the whole text is removed even when the delimiters are
    ///     present, so that the result opens on the first entry's line.
    /// </summary>
    [Test]
    public async Task TrimDelimiters_Test_SurroundingWhitespace_IsRemoved()
    {
        // Act
        ReadOnlySpan<char> result = StringUtils.TrimDelimiters("\n  ---\ntitle: Home\n---  \n", "---");

        // Assert
        await Assert.That(result.ToString()).IsEqualTo("\ntitle: Home");
    }

    /// <summary>
    ///     Verifies that any value serves as the delimiter, not only a run of dashes.
    /// </summary>
    [Test]
    [Arguments("+++", "+++\ntitle: Home\n+++")]
    [Arguments("...", "...\ntitle: Home\n...")]
    public async Task TrimDelimiters_Test_Delimiter_IsNotRestrictedToDashes(string delimiter, string text)
    {
        // Act
        ReadOnlySpan<char> result = StringUtils.TrimDelimiters(text, delimiter);

        // Assert
        await Assert.That(result.ToString()).IsEqualTo("\ntitle: Home");
    }

    /// <summary>
    ///     Verifies that a slice of a larger text is trimmed on its own terms, the characters around it
    ///     reaching neither end of the result.
    /// </summary>
    [Test]
    public async Task TrimDelimiters_Test_Slice_IsTrimmedWithoutItsSurroundings()
    {
        // Arrange
        const string lead = "body\n";
        const string slice = "---\ntitle: Home\n---";
        const string document = $"{lead}{slice}\nmore";

        // Act
        ReadOnlySpan<char> result = StringUtils.TrimDelimiters(
            document.AsSpan(lead.Length, slice.Length),
            "---");

        // Assert
        await Assert.That(result.ToString()).IsEqualTo("\ntitle: Home");
    }

    #endregion // TrimDelimiters Tests
}
