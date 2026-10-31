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
}
