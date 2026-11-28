using System;
using System.Threading.Tasks;

using Gelyn.Core.Internals.Primitives;


namespace Gelyn.Tests.Core.Internals.Primitives;

/// <summary>
///     Unit tests for the <see cref="StringUtils"/> class.
/// </summary>
[Category(Categories.Internal)]
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
}
