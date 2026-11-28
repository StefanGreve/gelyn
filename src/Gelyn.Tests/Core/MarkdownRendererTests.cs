using System.Text;
using System.Threading.Tasks;

using Gelyn.Core;
using Gelyn.Core.Internals.Primitives;
using Gelyn.Model;

namespace Gelyn.Tests.Core;

/// <summary>
///     Unit tests for the <see cref="MarkdownRenderer"/> class.
/// </summary>
/// <remarks>
///     What is under test is the pipeline the renderer configures, and not Markdig itself. An extension
///     dropped from the builder changes what every page renders to and fails nothing else in the suite,
///     because the documents the other tests render are prose that needs no extension at all.
/// </remarks>
[Category(Categories.Internal)]
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
    [Arguments("> [!NOTE]\n> Heads up", """class="markdown-alert markdown-alert-note""")]
    [Arguments("*[HTML]: HyperText Markup Language\n\nWrite HTML", """<abbr title="HyperText Markup Language">HTML</abbr>""")]
    [Arguments("[TOC]\n\n## First\n\n## Second", """<nav class="toc" role="doc-toc">""")]
    [Arguments("a \"\"quoted\"\" work", "<cite>quoted</cite>")]
    [Arguments("^^^\n![Logo](logo.png)\n^^^ The logo", "<figcaption>The logo</figcaption>")]
    [Arguments("a. one\na. two", """<ol type="a">""")]
    [Arguments("```mermaid\ngraph LR\n```", """<pre class="mermaid">""")]
    public async Task Render_Test_EnabledExtension_ReachesTheOutput(string markdown, string expected)
    {
        // Arrange
        using var bodies = new PageBodies();

        // Act
        RenderedMarkdown rendered = MarkdownRenderer.Render(markdown, bodies);

        // Assert
        await Assert.That(DecodeBody(rendered)).Contains(expected);
    }

    /// <summary>
    ///     Verifies that a link to a <c>.markdown</c> file is left as written, because the build copies such a
    ///     file instead of rendering it.
    /// </summary>
    [Test]
    public async Task Render_Test_LinkToMarkdownExtension_IsLeftAsWritten()
    {
        // Arrange
        using var bodies = new PageBodies();

        // Act
        RenderedMarkdown rendered = MarkdownRenderer.Render("[Notes](notes.markdown)", bodies);

        // Assert
        await Assert.That(DecodeBody(rendered)).Contains("""<a href="notes.markdown">""");
    }

    #endregion // Extension Tests

    #region Front Matter Tests

    /// <summary>
    ///     Verifies that a front matter block is read into the result and left out of the rendered body.
    /// </summary>
    [Test]
    public async Task Render_Test_FrontMatter_IsReadAndNotRendered()
    {
        // Arrange
        using var bodies = new PageBodies();

        // Act
        RenderedMarkdown rendered = MarkdownRenderer.Render("---\ntitle: Home\n---\n\n# Body", bodies);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(rendered.FrontMatter?.Title).IsEqualTo("Home");
            await Assert.That(DecodeBody(rendered)).Contains("""<h1 id="body">Body</h1>""");
            await Assert.That(DecodeBody(rendered)).DoesNotContain("title: Home");
        }
    }

    /// <summary>
    ///     Verifies that a document opening with anything else carries no front matter, because the block
    ///     parser opens at offset zero alone.
    /// </summary>
    [Test]
    public async Task Render_Test_DocumentWithoutFrontMatter_CarriesNone()
    {
        // Arrange
        using var bodies = new PageBodies();

        // Act
        RenderedMarkdown rendered = MarkdownRenderer.Render("# Body\n\n---\ntitle: Home\n---", bodies);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(rendered.FrontMatter).IsNull();
            await Assert.That(DecodeBody(rendered)).Contains("Body");
        }
    }

    #endregion // Front Matter Tests

    #region Footnote Tests

    /// <summary>
    ///     Verifies that a footnote definition placed directly after a definition list is dropped, together
    ///     with the reference to it.
    /// </summary>
    /// <remarks>
    ///     A defect in Markdig, not a decision of this project's: the definition list container is
    ///     never closed, so <c>FootnoteParser.TryOpen</c> declines and both the definition and the reference
    ///     reach the page as literal text, with no warning and a zero exit code. Pinned so that a fix
    ///     upstream turns this test red, which is the signal that the content files depending on the
    ///     workaround below no longer need it.
    /// </remarks>
    [Test]
    public async Task Render_Test_FootnoteAfterADefinitionList_IsDropped()
    {
        // Arrange
        using var bodies = new PageBodies();

        // Act
        RenderedMarkdown rendered = MarkdownRenderer.Render("Term\n:   The definition\n\n[^1]: the note\n\nref[^1]", bodies);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(DecodeBody(rendered)).Contains("<p>[^1]: the note</p>");
            await Assert.That(DecodeBody(rendered)).DoesNotContain("footnote-ref");
        }
    }

    /// <summary>
    ///     Verifies that a block between a definition list and the footnote definitions renders both, which
    ///     is the workaround <c>example/about.md</c> is written around.
    /// </summary>
    [Test]
    public async Task Render_Test_FootnoteAfterAnIntervalBlock_IsRendered()
    {
        // Arrange
        using var bodies = new PageBodies();

        // Act
        RenderedMarkdown rendered = MarkdownRenderer.Render("Term\n:   The definition\n\n---\n\nref[^1]\n\n[^1]: the note", bodies);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(DecodeBody(rendered)).Contains("""class="footnote-ref">""");
            await Assert.That(DecodeBody(rendered)).DoesNotContain("<p>[^1]: the note</p>");
        }
    }

    #endregion // Footnote Tests

    #region Body Tests

    /// <summary>
    ///     Verifies that a document holding an unpaired surrogate is refused in the render, so that its page fails
    ///     before anything reaches the disk.
    /// </summary>
    [Test]
    public async Task Render_Test_UnpairedSurrogate_IsRefused()
    {
        // Arrange
        using var bodies = new PageBodies();

        // Act and Assert
        await Assert.That(() => MarkdownRenderer.Render("a" + (char)0xD800, bodies)).Throws<EncoderFallbackException>();
    }

    #endregion // Body Tests

    #region Helpers

    private static string DecodeBody(RenderedMarkdown rendered) => Encoding.UTF8.GetString(rendered.Body.Span);

    #endregion
}
