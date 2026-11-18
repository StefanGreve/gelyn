using System;
using System.Threading.Tasks;

using Gelyn.Core.Internals;

namespace Gelyn.Tests.Core;

/// <summary>
///     Unit tests for the <see cref="StringUtils"/> class.
/// </summary>
public class StringUtilsTests
{
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

    #region NormalizeLineEndings Tests

    /// <summary>
    ///     Verifies that a carriage return is folded into a line feed whether it comes alone or paired, at
    ///     the start, in the middle or at the end of the text, and that a line feed is left as it is.
    /// </summary>
    [Test]
    [Arguments("a\r\nb", "a\nb")]
    [Arguments("a\rb", "a\nb")]
    [Arguments("a\r\nb\rc\nd", "a\nb\nc\nd")]
    [Arguments("\r\na", "\na")]
    [Arguments("a\r\n", "a\n")]
    [Arguments("a\r", "a\n")]
    [Arguments("\r", "\n")]
    [Arguments("\r\n\r\n", "\n\n")]
    [Arguments("\n\r", "\n\n")]
    public async Task NormalizeLineEndings_Test_CarriageReturn_BecomesALineFeed(string text, string expected)
    {
        // Act
        string result = StringUtils.NormalizeLineEndings(text);

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a text holding no carriage return comes back as the same instance, so that the
    ///     common case on a Unix host allocates nothing.
    /// </summary>
    [Test]
    [Arguments("")]
    [Arguments("a")]
    [Arguments("a\nb\n")]
    public async Task NormalizeLineEndings_Test_TextWithoutACarriageReturn_IsTheSameInstance(string text)
    {
        // Act
        string result = StringUtils.NormalizeLineEndings(text);

        // Assert
        await Assert.That(result).IsSameReferenceAs(text);
    }

    /// <summary>
    ///     Verifies that the Unicode line breaks HTML treats as content are left alone, which is what sets
    ///     the method apart from <see cref="string.ReplaceLineEndings(string)"/>.
    /// </summary>
    [Test]
    public async Task NormalizeLineEndings_Test_UnicodeLineBreaks_AreLeftAlone()
    {
        // Arrange
        const string text = "a\u0085b\U00002028c\U00002029d\u000Ce\r\nf";

        // Act
        string result = StringUtils.NormalizeLineEndings(text);

        // Assert
        await Assert.That(result).IsEqualTo("a\u0085b\U00002028c\U00002029d\u000Ce\nf");
    }

    #endregion // NormalizeLineEndings Tests
}
