using System;
using System.Threading.Tasks;

using Gelyn.Core.Internals;

namespace Gelyn.Tests.Core.Internals;

/// <summary>
///     Unit tests for the <see cref="UrlUtils"/> class.
/// </summary>
[Category(Categories.Internal)]
public class UrlUtilsTests
{
    #region AsDirectory Tests

    /// <summary>
    ///     Verifies that the directory keeps the origin and the non-empty path segments, and ends on one slash.
    /// </summary>
    [Test]
    [Arguments("https://example.com", "https://example.com/")]
    [Arguments("https://example.com/", "https://example.com/")]
    [Arguments("https://example.com/blog", "https://example.com/blog/")]
    [Arguments("https://example.com/blog//", "https://example.com/blog/")]
    [Arguments("https://example.com//docs", "https://example.com/docs/")]
    [Arguments("https://example.com///docs/", "https://example.com/docs/")]
    [Arguments("https://example.com/a//b", "https://example.com/a/b/")]
    [Arguments("http://example.com:8080/blog", "http://example.com:8080/blog/")]
    public async Task AsDirectory_Test_Path_IsNormalized(string url, string expected)
    {
        // Act
        Uri result = UrlUtils.AsDirectory(new Uri(url));

        // Assert
        await Assert.That(result.AbsoluteUri).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that the query and the fragment are dropped, so that no slash is appended after them.
    /// </summary>
    [Test]
    [Arguments("https://example.com/blog?x=1")]
    [Arguments("https://example.com/blog#frag")]
    [Arguments("https://example.com/blog?x=1#frag")]
    [Arguments("https://example.com/blog/?x=1#frag")]
    public async Task AsDirectory_Test_QueryAndFragment_AreDropped(string url)
    {
        // Act
        Uri result = UrlUtils.AsDirectory(new Uri(url));

        // Assert
        await Assert.That(result.AbsoluteUri).IsEqualTo("https://example.com/blog/");
    }

    #endregion

    #region ResolveBasePath Tests

    /// <summary>
    ///     Verifies that the base path keeps the non-empty segments alone, without a trailing slash.
    /// </summary>
    [Test]
    [Arguments("https://example.com", "")]
    [Arguments("https://example.com/", "")]
    [Arguments("https://example.com//", "")]
    [Arguments("https://example.com/blog", "/blog")]
    [Arguments("https://example.com/blog/", "/blog")]
    [Arguments("https://example.com//docs", "/docs")]
    [Arguments("https://example.com///docs/", "/docs")]
    [Arguments("https://example.com/a//b/", "/a/b")]
    [Arguments("https://example.com/blog?x=1#frag", "/blog")]
    public async Task ResolveBasePath_Test_Path_IsNormalized(string url, string expected)
    {
        // Act
        string result = UrlUtils.ResolveBasePath(new Uri(url));

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that an absent base URL resolves to the root of the origin.
    /// </summary>
    [Test]
    public async Task ResolveBasePath_Test_AbsentBaseUrl_IsEmpty()
    {
        // Act
        string result = UrlUtils.ResolveBasePath(null);

        // Assert
        await Assert.That(result).IsEmpty();
    }

    #endregion
}
