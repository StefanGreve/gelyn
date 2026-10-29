using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Gelyn.Components;
using Gelyn.Core.Context;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Tests.Components;

/// <summary>
///     Unit tests for the <see cref="TableOfDocumentsComponent"/> class.
/// </summary>
public partial class TableOfDocumentsComponentTests
{
    #region Scope Tests

    /// <summary>
    ///     Verifies that the table lists the pages below the marker's own directory as well as the pages
    ///     beside it, so that a section index reaches its whole subtree.
    /// </summary>
    [Test]
    public async Task Render_Test_NestedPage_IsListed()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/hello.html", "Hello", "2026-03-01"),
            Page("blog/2026/retro.html", "Retro", "2026-07-01"));

        // Assert
        await Assert.That(Links(table)).IsEqualTo("/blog/2026/retro.html,/blog/hello.html");
    }

    /// <summary>
    ///     Verifies that the marker's own page is never an entry in its own table.
    /// </summary>
    [Test]
    public async Task Render_Test_MarkerPage_IsNotListed()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/hello.html", "Hello", "2026-03-01"));

        // Assert
        await Assert.That(Links(table)).IsEqualTo("/blog/hello.html");
    }

    /// <summary>
    ///     Verifies that a page above the marker's own directory is left out, so that a section index does
    ///     not reach back up the tree.
    /// </summary>
    [Test]
    public async Task Render_Test_PageAboveTheMarker_IsNotListed()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("index.html", "Home", "2026-01-01"),
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/hello.html", "Hello", "2026-03-01"));

        // Assert
        await Assert.That(Links(table)).IsEqualTo("/blog/hello.html");
    }

    /// <summary>
    ///     Verifies that a directory whose name merely begins with the marker's own is out of scope, the
    ///     prefix test running on whole segments and not on characters.
    /// </summary>
    [Test]
    public async Task Render_Test_SiblingDirectorySharingAPrefix_IsNotListed()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/hello.html", "Hello", "2026-03-01"),
            Page("blogs/other.html", "Other", "2026-04-01"));

        // Assert
        await Assert.That(Links(table)).IsEqualTo("/blog/hello.html");
    }

    /// <summary>
    ///     Verifies that a marker on the landing page reaches the whole site, the content root resolving to
    ///     the empty prefix every page carries.
    /// </summary>
    [Test]
    public async Task Render_Test_MarkerOnTheLandingPage_ListsTheWholeSite()
    {
        // Act
        string table = Render(
            "index.html",
            Page("index.html", "Home", "2026-01-01"),
            Page("about.html", "About", "2026-01-15"),
            Page("blog/hello.html", "Hello", "2026-03-01"));

        // Assert
        await Assert.That(Links(table)).IsEqualTo("/blog/hello.html,/about.html");
    }

    #endregion // Scope Tests

    #region Ordering Tests

    /// <summary>
    ///     Verifies that entries are ordered by declared date, newest first, and not by the order the scan
    ///     located them in.
    /// </summary>
    [Test]
    public async Task Render_Test_Entries_AreOrderedNewestFirst()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/middle.html", "Middle", "2026-05-01"),
            Page("blog/oldest.html", "Oldest", "2026-03-01"),
            Page("blog/newest.html", "Newest", "2026-09-01"));

        // Assert
        await Assert.That(Titles(table)).IsEqualTo("Newest,Middle,Oldest");
    }

    /// <summary>
    ///     Verifies that two pages declaring the same date are ordered by title, so that the order does not
    ///     fall back on a file name the author never chose.
    /// </summary>
    /// <remarks>
    ///     The two tied pages are declared in the order the tie-break has to reverse, because the primary
    ///     sort is stable and would otherwise return them already in the expected order on its own.
    /// </remarks>
    [Test]
    public async Task Render_Test_EntriesSharingADate_AreOrderedByTitle()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/alpha.html", "Zulu", "2026-03-01"),
            Page("blog/zulu.html", "Alpha", "2026-03-01"));

        // Assert
        await Assert.That(Titles(table)).IsEqualTo("Alpha,Zulu");
    }

    #endregion // Ordering Tests

    #region Draft Tests

    /// <summary>
    ///     Verifies that a page declaring no date is a draft and reaches no table.
    /// </summary>
    [Test]
    public async Task Render_Test_DatelessPage_IsNotListed()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/hello.html", "Hello", "2026-03-01"),
            Page("blog/draft.html", "Draft"));

        // Assert
        await Assert.That(Links(table)).IsEqualTo("/blog/hello.html");
    }

    /// <summary>
    ///     Verifies that a directory with nothing to list renders nothing at all, not an empty table.
    /// </summary>
    [Test]
    public async Task Render_Test_NothingToList_RendersNothing()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("index.html", "Home", "2026-01-01"),
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/draft.html", "Draft"));

        // Assert
        await Assert.That(table).IsEmpty();
    }

    #endregion // Draft Tests

    #region Escaping Tests

    /// <summary>
    ///     Verifies that a title is escaped for the element body it sits in, the value coming from front
    ///     matter and so from the author.
    /// </summary>
    [Test]
    public async Task Render_Test_Title_IsEscaped()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/hello.html", "Tags <b> & \"quotes\"", "2026-03-01"));

        // Assert
        await Assert.That(table).Contains("Tags &lt;b&gt; &amp; &quot;quotes&quot;</a>");
    }

    /// <summary>
    ///     Verifies that a URL is escaped for the attribute it sits in.
    /// </summary>
    [Test]
    public async Task Render_Test_Href_IsEscaped()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/a&b.html", "Ampersand", "2026-03-01"));

        // Assert
        await Assert.That(table).Contains("""<a href="/blog/a&amp;b.html">""");
    }

    #endregion // Escaping Tests

    #region Item Type Tests

    /// <summary>
    ///     Verifies that an index below the marker is marked as a folder while every other entry is marked
    ///     as a file, which is the distinction the shipped themes style.
    /// </summary>
    [Test]
    public async Task Render_Test_NestedIndex_IsMarkedAsAFolder()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/2026/retro.html", "Retro", "2026-07-01"),
            Page("blog/2026/index.html", "Year 2026", "2026-06-01"),
            Page("blog/hello.html", "Hello", "2026-03-01"));

        // Assert
        await Assert.That(Types(table)).IsEqualTo("file,folder,file");
    }

    /// <summary>
    ///     Verifies that a page whose name merely ends with the index file name is marked as a file, the
    ///     last segment having to match in full.
    /// </summary>
    [Test]
    public async Task Render_Test_NestedPageNamedLikeAnIndex_IsMarkedAsAFile()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/2026/myindex.html", "My Index", "2026-07-01"));

        // Assert
        await Assert.That(Types(table)).IsEqualTo("file");
    }

    /// <summary>
    ///     Verifies that an index sitting beside the marker is marked as a file, a folder row standing for a
    ///     directory the marker's own directory contains.
    /// </summary>
    [Test]
    public async Task Render_Test_IndexBesideTheMarker_IsMarkedAsAFile()
    {
        // Act
        string table = Render(
            "blog/hello.html",
            Page("blog/hello.html", "Hello", "2026-03-01"),
            Page("blog/index.html", "Blog", "2026-02-01"));

        // Assert
        await Assert.That(Types(table)).IsEqualTo("file");
    }

    #endregion // Item Type Tests

    #region Markup Tests

    /// <summary>
    ///     Verifies that the table carries one header cell per column and that a row carries as many cells as
    ///     the header does.
    /// </summary>
    [Test]
    public async Task Render_Test_Table_CarriesAHeaderPerColumn()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/hello.html", "Hello", "2026-03-01"));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Headers(table)).IsEqualTo("Date,Title");
            await Assert.That(Count(table, "<td>")).IsEqualTo(Count(table, "<th>"));
        }
    }

    /// <summary>
    ///     Verifies that a date is rendered in both a human-readable and a machine-readable form, so that the
    ///     displayed value can be localized without the attribute following it.
    /// </summary>
    [Test]
    public async Task Render_Test_Date_CarriesAMachineReadableAttribute()
    {
        // Act
        string table = Render(
            "blog/index.html",
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/hello.html", "Hello", "2026-03-01"));

        // Assert
        await Assert.That(table).Contains("""<time datetime="2026-03-01">Mar 1, 2026</time>""");
    }

    #endregion // Markup Tests

    #region Helpers

    private static string Render(string currentPath, params ContentPage[] pages)
    {
        string current = Native(currentPath);

        return TableOfDocumentsComponent.Render(new RenderContext
        {
            Options = new SiteOptions(),
            Navigation = [],
            Documents = pages,
            Page = pages.Single(page => string.Equals(page.OutputPath, current, StringComparison.Ordinal)),
        });
    }

    private static ContentPage Page(string outputPath, string title, string? date = null) => new()
    {
        OutputPath = Native(outputPath),
        Href = $"/{outputPath}",
        Title = title,
        Description = null,
        Date = date is null ? null : DateOnly.Parse(date, CultureInfo.InvariantCulture),
        EnableMathRendering = false,
        EnableDiagramRendering = false,
        Html = string.Empty,
        InNavigation = false,
        Stylesheets = [],
        Scripts = [],
        Favicon = null,
    };

    // The component scopes by output path, which carries the native separator wherever the suite runs.
    private static string Native(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    // Joined instead of returned as a sequence, because IsEquivalentTo disregards the order under test.
    private static string Links(string table) => Joined(RowLink(), table, "href");

    private static string Titles(string table) => Joined(RowLink(), table, "title");

    private static string Types(string table) => Joined(RowType(), table, "type");

    private static string Headers(string table) => Joined(HeaderCell(), table, "header");

    private static string Joined(Regex pattern, string table, string group) => string.Join(
        ',',
        pattern.Matches(table).Select(match => match.Groups[group].Value));

    private static int Count(string table, string value) =>
        table.Split(value, StringSplitOptions.None).Length - 1;

    [GeneratedRegex("""<a href="(?<href>[^"]*)">(?<title>[^<]*)</a>""")]
    private static partial Regex RowLink();

    [GeneratedRegex("""<tr data-type="(?<type>[^"]*)">""")]
    private static partial Regex RowType();

    [GeneratedRegex("<th>(?<header>[^<]*)</th>")]
    private static partial Regex HeaderCell();

    #endregion
}
