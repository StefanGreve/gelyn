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
}
