using System.Threading.Tasks;

using Gelyn.Extensions;
using Gelyn.Model;

namespace Gelyn.Tests.Extensions;

/// <summary>
///     Unit tests for the <see cref="ContentKindExtensions"/> class.
/// </summary>
[Category(Categories.Internal)]
public class ContentKindExtensionsTests
{
    #region FromFileName Tests

    /// <summary>
    ///     Verifies that a Markdown file is a page, whatever the case of its extension.
    /// </summary>
    [Test]
    [Arguments("about.md")]
    [Arguments("About.MD")]
    public async Task FromFileName_Test_MarkdownExtension_IsAPage(string name)
    {
        // Act
        var kind = ContentKind.FromFileName(name);

        // Assert
        await Assert.That(kind).IsEqualTo(ContentKind.Page);
    }

    /// <summary>
    ///     Verifies that a <c>.css</c> file is a stylesheet, whatever the case of its extension.
    /// </summary>
    [Test]
    [Arguments("styles.css")]
    [Arguments("Styles.CSS")]
    public async Task FromFileName_Test_StylesheetExtension_IsAStylesheet(string name)
    {
        // Act
        var kind = ContentKind.FromFileName(name);

        // Assert
        await Assert.That(kind).IsEqualTo(ContentKind.Stylesheet);
    }

    /// <summary>
    ///     Verifies that a <c>.js</c> file is a script, whatever the case of its extension.
    /// </summary>
    [Test]
    [Arguments("app.js")]
    [Arguments("App.JS")]
    public async Task FromFileName_Test_ScriptExtension_IsAScript(string name)
    {
        // Act
        var kind = ContentKind.FromFileName(name);

        // Assert
        await Assert.That(kind).IsEqualTo(ContentKind.Script);
    }

    /// <summary>
    ///     Verifies that an <c>.ico</c> file is an icon, whatever the case of its extension.
    /// </summary>
    [Test]
    [Arguments("favicon.ico")]
    [Arguments("Favicon.ICO")]
    public async Task FromFileName_Test_IconExtension_IsAFavicon(string name)
    {
        // Act
        var kind = ContentKind.FromFileName(name);

        // Assert
        await Assert.That(kind).IsEqualTo(ContentKind.Favicon);
    }

    /// <summary>
    ///     Verifies that only the last extension counts, so that a name merely containing a known one, or
    ///     carrying none at all, is copied as is.
    /// </summary>
    [Test]
    [Arguments("diagram.png")]
    [Arguments("CNAME")]
    [Arguments("draft.md.bak")]
    [Arguments("notes.markdown")]
    [Arguments("styles.")]
    public async Task FromFileName_Test_OtherName_IsAMiscFile(string name)
    {
        // Act
        var kind = ContentKind.FromFileName(name);

        // Assert
        await Assert.That(kind).IsEqualTo(ContentKind.MiscFile);
    }

    #endregion // FromFileName Tests
}
