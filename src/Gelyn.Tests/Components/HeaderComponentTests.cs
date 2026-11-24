using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Gelyn.Components;
using Gelyn.Core;
using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

using TUnit.Assertions.Enums;

namespace Gelyn.Tests.Components;

/// <summary>
///     Unit tests for the <see cref="HeaderComponent"/> class.
/// </summary>
[Category(Categories.Internal)]
public partial class HeaderComponentTests
{
    private static readonly SiteOptions Options = new() { Title = "Site" };

    #region Entry Tests

    /// <summary>
    ///     Verifies that every page of the navigation is linked once, in the order given.
    /// </summary>
    [Test]
    public async Task Render_Test_Entries_AreLinkedOnceInOrder()
    {
        // Act
        Banner banner = HeaderComponent.Render(Options, [Page("/index.html", "Home"), Page("/about.html", "About"), Page("/blog/index.html", "Blog")]);

        // Assert
        await Assert.That(Hrefs(Html(banner))).IsEquivalentTo(["/index.html", "/about.html", "/blog/index.html"], CollectionOrdering.Matching);
    }

    /// <summary>
    ///     Verifies that the first entry carries the site title in place of the landing page's own, and every other
    ///     entry the title of its page.
    /// </summary>
    [Test]
    public async Task Render_Test_FirstEntry_CarriesTheSiteTitle()
    {
        // Act
        Banner banner = HeaderComponent.Render(Options, [Page("/index.html", "Home"), Page("/about.html", "About")]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Html(banner)).Contains(">Site</a>");
            await Assert.That(Html(banner)).Contains(">About</a>");
            await Assert.That(Html(banner)).DoesNotContain(">Home</a>");
        }
    }

    /// <summary>
    ///     Verifies that an href is encoded for the attribute it is written into, while the entry is still found
    ///     under the href as the page carries it.
    /// </summary>
    [Test]
    public async Task Render_Test_Href_IsEncoded()
    {
        // Act
        Banner banner = HeaderComponent.Render(Options, [Page("/index.html", "Home"), Page("/a&b.html", "Ampersand")]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Html(banner)).Contains("""<a href="/a&amp;b.html">Ampersand</a>""");
            await Assert.That(Marked(banner, "/a&b.html")).Contains("""<a aria-current="page" href="/a&amp;b.html">""");
        }
    }

    #endregion // Entry Tests

    #region Mark Tests

    /// <summary>
    ///     Verifies that a page the navigation links takes the mark on its own entry and on no other.
    /// </summary>
    [Test]
    [Arguments("/index.html")]
    [Arguments("/about.html")]
    [Arguments("/blog/index.html")]
    public async Task CutFor_Test_LinkedPage_MarksItsOwnEntryOnly(string href)
    {
        // Arrange
        Banner banner = HeaderComponent.Render(Options, [Page("/index.html", "Home"), Page("/about.html", "About"), Page("/blog/index.html", "Blog")]);

        // Act
        string marked = Marked(banner, href);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(marked).Contains($"""<a aria-current="page" href="{href}">""");
            await Assert.That(Mark().Count(marked)).IsEqualTo(1);
            await Assert.That(Hrefs(marked)).IsEquivalentTo(["/index.html", "/about.html", "/blog/index.html"], CollectionOrdering.Matching);
        }
    }

    /// <summary>
    ///     Verifies that a title with characters of more than one byte ahead of the current entry leaves the mark
    ///     on the right entry, the offsets being counted in bytes.
    /// </summary>
    [Test]
    public async Task CutFor_Test_MultiByteTitleAhead_MarksTheRightEntry()
    {
        // Arrange
        Banner banner = HeaderComponent.Render(Options, [Page("/index.html", "Home"), Page("/notes.html", "\U000065E5\U0000672C \U0001F600"), Page("/blog/index.html", "Blog")]);

        // Act
        string marked = Marked(banner, "/blog/index.html");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(marked).Contains("""<a aria-current="page" href="/blog/index.html">""");
            await Assert.That(Mark().Count(marked)).IsEqualTo(1);
        }
    }

    /// <summary>
    ///     Verifies that a page the navigation does not link writes the banner as it is, with no mark anywhere.
    /// </summary>
    [Test]
    public async Task CutFor_Test_PageOutsideTheNavigation_WritesNoMark()
    {
        // Arrange
        Banner banner = HeaderComponent.Render(Options, [Page("/index.html", "Home"), Page("/about.html", "About")]);

        // Act
        string marked = Marked(banner, "/blog/post.html");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(marked).IsEqualTo(Html(banner));
            await Assert.That(Mark().Count(marked)).IsEqualTo(0);
        }
    }

    #endregion // Mark Tests

    #region Helpers

    [GeneratedRegex("href=\"([^\"]*)\"")]
    private static partial Regex Href();

    [GeneratedRegex("aria-current")]
    private static partial Regex Mark();

    private static IReadOnlyList<string> Hrefs(string html) =>
        [.. Href().Matches(html).Select(static match => match.Groups[1].Value)];

    private static string Html(Banner banner) =>
        Encoding.UTF8.GetString(banner.Bytes.Span);

    // Written the way PageLayout writes the banner for a page.
    private static string Marked(Banner banner, string href)
    {
        using var buffer = new PageBuffer();
        banner.CutFor(Page(href, "Current")).WriteTo(buffer);

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static ContentPage Page(string href, string title) =>
        new()
        {
            SourcePath = $"{title}.md",
            OutputPath = $"{title}.html",
            Href = href,
            Title = title,
            Description = null,
            Date = null,
            Revised = null,
            EnableMathRendering = false,
            EnableDiagramRendering = false,
            EnableSyntaxHighlighting = false,
            EnableComments = false,
            Body = ReadOnlyMemory<byte>.Empty,
            InNavigation = true,
            IsNotFoundPage = false,
            InFeed = false,
            Stylesheets = [],
            Scripts = [],
            Favicon = null,
            UnknownKeys = [],
            InvalidValues = [],
        };

    #endregion
}
