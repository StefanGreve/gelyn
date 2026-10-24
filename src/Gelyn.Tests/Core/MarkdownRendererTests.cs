using System.Threading.Tasks;

using Gelyn.Core;
using Gelyn.Model;

namespace Gelyn.Tests.Core;

/// <summary>
///     Unit tests for the <see cref="MarkdownRenderer"/> class.
/// </summary>
/// <remarks>
///     What is under test is the pipeline the renderer configures rather than Markdig itself. An extension
///     dropped from the builder changes what every page renders to and fails nothing else in the suite,
///     because the documents the other tests render are prose that needs no extension at all.
/// </remarks>
public class MarkdownRendererTests
{
    #region Extension Tests

    /// <summary>
    ///     Verifies that each extension the pipeline enables reaches the rendered output.
    /// </summary>
    [Test]
    [Arguments("## Hello There", """<h2 id="hello-there">Hello There</h2>""")]
    [Arguments("a ~~b~~ c", "<del>b</del>")]
    [Arguments("see www.example.com now", """<a href="https://www.example.com">""")]
    [Arguments("| a | b |\n|---|---|\n| 1 | 2 |", "<table>")]
    [Arguments("- [x] done", """type="checkbox" checked="checked" """)]
    [Arguments("Term\n:   The definition", "<dt>Term</dt>")]
    [Arguments("text[^1]\n\n[^1]: the note", """class="footnote-ref">""")]
    [Arguments("$$x^2$$", """<span class="math">""")]
    [Arguments("[Usage](usage.md)", """<a href="usage.html">""")]
    [Arguments("[Preview](usage.md#preview)", """<a href="usage.html#preview">""")]
    public async Task Render_Test_EnabledExtension_ReachesTheOutput(string markdown, string expected)
    {
        // Act
        RenderedMarkdown rendered = MarkdownRenderer.Render(markdown);

        // Assert
        await Assert.That(rendered.Html).Contains(expected);
    }

    #endregion // Extension Tests

    #region Front Matter Tests

    /// <summary>
    ///     Verifies that a front matter block is read into the result and left out of the rendered body.
    /// </summary>
    [Test]
    public async Task Render_Test_FrontMatter_IsReadAndNotRendered()
    {
        // Act
        RenderedMarkdown rendered = MarkdownRenderer.Render("---\ntitle: Home\n---\n\n# Body");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(rendered.FrontMatter?.Title).IsEqualTo("Home");
            await Assert.That(rendered.Html).Contains("""<h1 id="body">Body</h1>""");
            await Assert.That(rendered.Html).DoesNotContain("title: Home");
        }
    }

    /// <summary>
    ///     Verifies that a document opening with anything else carries no front matter, because the block
    ///     parser opens at offset zero alone.
    /// </summary>
    [Test]
    public async Task Render_Test_DocumentWithoutFrontMatter_CarriesNone()
    {
        // Act
        RenderedMarkdown rendered = MarkdownRenderer.Render("# Body\n\n---\ntitle: Home\n---");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(rendered.FrontMatter).IsNull();
            await Assert.That(rendered.Html).Contains("Body");
        }
    }

    #endregion // Front Matter Tests

    #region Footnote Tests

    /// <summary>
    ///     Verifies that a footnote definition placed directly after a definition list is dropped, together
    ///     with the reference to it.
    /// </summary>
    /// <remarks>
    ///     A defect in Markdig rather than a decision of this project's: the definition list container is
    ///     never closed, so <c>FootnoteParser.TryOpen</c> declines and both the definition and the reference
    ///     reach the page as literal text, with no warning and a zero exit code. Pinned so that a fix
    ///     upstream turns this test red, which is the signal that the content files depending on the
    ///     workaround below no longer need it.
    /// </remarks>
    [Test]
    public async Task Render_Test_FootnoteAfterADefinitionList_IsDropped()
    {
        // Act
        RenderedMarkdown rendered = MarkdownRenderer.Render("Term\n:   The definition\n\n[^1]: the note\n\nref[^1]");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(rendered.Html).Contains("<p>[^1]: the note</p>");
            await Assert.That(rendered.Html).DoesNotContain("footnote-ref");
        }
    }

    /// <summary>
    ///     Verifies that a block between a definition list and the footnote definitions renders both, which
    ///     is the workaround <c>example/about.md</c> is written around.
    /// </summary>
    [Test]
    public async Task Render_Test_FootnoteAfterAnIntervalBlock_IsRendered()
    {
        // Act
        RenderedMarkdown rendered = MarkdownRenderer.Render("Term\n:   The definition\n\n---\n\nref[^1]\n\n[^1]: the note");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(rendered.Html).Contains("""class="footnote-ref">""");
            await Assert.That(rendered.Html).DoesNotContain("<p>[^1]: the note</p>");
        }
    }

    #endregion // Footnote Tests
}
