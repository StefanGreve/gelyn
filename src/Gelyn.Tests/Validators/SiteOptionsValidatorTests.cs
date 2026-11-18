using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Core;
using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Tests.Stubs;
using Gelyn.Validators;

using Microsoft.Extensions.Options;

namespace Gelyn.Tests.Validators;

/// <summary>
///     Unit tests for the <see cref="SiteOptionsValidator"/> class.
/// </summary>
[Category(Categories.Internal)]
public class SiteOptionsValidatorTests
{
    #region Validate Tests

    /// <summary>
    ///     Verifies that an absolute web URL is accepted whether or not it carries a path.
    /// </summary>
    [Test]
    [Arguments("https://example.com")]
    [Arguments("http://example.com")]
    [Arguments("https://example.com/gelyn/docs")]
    public async Task Validate_Test_ServableBaseUrl_Succeeds(string baseUrl)
    {
        // Arrange
        var options = new SiteOptions { BaseUrl = new Uri(baseUrl) };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    /// <summary>
    ///     Verifies that a URL carrying no origin to resolve a link against, or a scheme a reader's browser
    ///     cannot fetch, is rejected under a message naming the setting at fault.
    /// </summary>
    [Test]
    [Arguments("example.com")]
    [Arguments("/gelyn/docs")]
    [Arguments("file:///tmp/site")]
    [Arguments("ftp://example.com")]
    public async Task Validate_Test_UnservableBaseUrl_Fails(string baseUrl)
    {
        // Arrange
        var options = new SiteOptions { BaseUrl = new Uri(baseUrl, UriKind.RelativeOrAbsolute) };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains(nameof(SiteOptions.BaseUrl));
        }
    }

    /// <summary>
    ///     Verifies that a footer link carrying a label and an absolute web URL is accepted.
    /// </summary>
    [Test]
    [Arguments("https://github.com/octocat")]
    [Arguments("http://example.com/a?b=c")]
    public async Task Validate_Test_ServableFooterLink_Succeeds(string url)
    {
        // Arrange
        var options = new SiteOptions { FooterLinks = [new FooterLink { Label = "GitHub", Url = new Uri(url) }] };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    /// <summary>
    ///     Verifies that a footer link whose URL carries no origin, or a scheme that would run or embed content
    ///     in place of navigating, is rejected under a message naming the entry and the member at fault.
    /// </summary>
    [Test]
    [Arguments("example.com")]
    [Arguments("/feed.xml")]
    [Arguments("javascript:alert(1)")]
    [Arguments("data:text/html,x")]
    [Arguments("ftp://example.com")]
    public async Task Validate_Test_UnservableFooterLinkUrl_Fails(string url)
    {
        // Arrange
        var link = new FooterLink { Label = "Broken", Url = new Uri(url, UriKind.RelativeOrAbsolute) };
        var options = new SiteOptions { FooterLinks = [link] };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains("FooterLinks[0].Url");
        }
    }

    /// <summary>
    ///     Verifies that a footer link declaring no URL is rejected, which is the state a configuration entry
    ///     omitting the member binds to.
    /// </summary>
    [Test]
    public async Task Validate_Test_AbsentFooterLinkUrl_Fails()
    {
        // Arrange
        var options = new SiteOptions { FooterLinks = [new FooterLink { Label = "GitHub" }] };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains("FooterLinks[0].Url");
        }
    }

    /// <summary>
    ///     Verifies that a footer link with an absent or blank label is rejected, because the anchor would read
    ///     as nothing.
    /// </summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Validate_Test_BlankFooterLinkLabel_Fails(string? label)
    {
        // Arrange
        var link = new FooterLink { Label = label, Url = new Uri("https://github.com/octocat") };
        var options = new SiteOptions { FooterLinks = [link] };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains("FooterLinks[0].Label");
        }
    }

    /// <summary>
    ///     Verifies that a failure names the index of the broken entry and leaves the valid one beside it alone.
    /// </summary>
    [Test]
    public async Task Validate_Test_FooterLinkFailure_NamesTheIndex()
    {
        // Arrange
        var options = new SiteOptions
        {
            FooterLinks =
            [
                new FooterLink { Label = "GitHub", Url = new Uri("https://github.com/octocat") },
                new FooterLink { Label = "Broken", Url = new Uri("/relative", UriKind.Relative) },
            ],
        };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.FailureMessage).Contains("FooterLinks[1].Url");
            await Assert.That(result.FailureMessage).DoesNotContain("FooterLinks[0]");
        }
    }

    /// <summary>
    ///     Verifies that every setting the tool cannot work without is rejected when it is blank, under a
    ///     message naming the setting at fault.
    /// </summary>
    /// <remarks>
    ///     An empty version addresses no package, since it reaches the URL as <c>katex@/dist</c>; an empty
    ///     language yields a <c>lang</c> attribute worse than none; and an empty directory would leave the
    ///     settings resolving against the working directory. The setting is named and never assigned, so
    ///     that a property renamed on the options type takes this table with it.
    /// </remarks>
    [Test]
    [Arguments(nameof(SiteOptions.Title), "")]
    [Arguments(nameof(SiteOptions.Title), "   ")]
    [Arguments(nameof(SiteOptions.Language), "")]
    [Arguments(nameof(SiteOptions.Language), "   ")]
    [Arguments(nameof(SiteOptions.ContentDirectory), "")]
    [Arguments(nameof(SiteOptions.ContentDirectory), "   ")]
    [Arguments(nameof(SiteOptions.OutputDirectory), "")]
    [Arguments(nameof(SiteOptions.OutputDirectory), "   ")]
    [Arguments(nameof(SiteOptions.KaTeXVersion), "")]
    [Arguments(nameof(SiteOptions.KaTeXVersion), "   ")]
    [Arguments(nameof(SiteOptions.MermaidVersion), "")]
    [Arguments(nameof(SiteOptions.MermaidVersion), "   ")]
    [Arguments(nameof(SiteOptions.ShikiVersion), "")]
    [Arguments(nameof(SiteOptions.ShikiVersion), "   ")]
    public async Task Validate_Test_BlankSetting_Fails(string setting, string value)
    {
        // Arrange
        SiteOptions options = Blank(setting, value);

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains(setting);
        }
    }

    /// <summary>
    ///     Verifies that leaving the base URL unset is valid, because a site served from the root of a domain
    ///     needs none.
    /// </summary>
    [Test]
    public async Task Validate_Test_AbsentBaseUrl_Succeeds()
    {
        // Arrange
        var options = new SiteOptions();

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.BaseUrl).IsNull();
            await Assert.That(result.Succeeded).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that an output directory resolving outside the content directory is accepted, including a
    ///     sibling whose name merely starts with it.
    /// </summary>
    [Test]
    [Arguments("content", "_site")]
    [Arguments("content", "content-old")]
    [Arguments("content", "../out")]
    public async Task Validate_Test_OutputDirectoryBesideContentDirectory_Succeeds(string content, string output)
    {
        // Arrange
        var options = new SiteOptions { ContentDirectory = content, OutputDirectory = output };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    /// <summary>
    ///     Verifies that an output directory resolving to or inside the content directory is rejected under a
    ///     message naming the setting at fault, because the build would otherwise publish its own output as
    ///     content.
    /// </summary>
    [Test]
    [Arguments("content", "content/_site")]
    [Arguments("content", "content")]
    [Arguments("content/", "content")]
    [Arguments(".", "_site")]
    public async Task Validate_Test_OutputDirectoryInsideContentDirectory_Fails(string content, string output)
    {
        // Arrange
        var options = new SiteOptions { ContentDirectory = content, OutputDirectory = output };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains(nameof(SiteOptions.OutputDirectory));
        }
    }

    /// <summary>
    ///     Verifies that an output directory containing the content directory is rejected, which is the
    ///     direction that matters to <c>gelyn clean</c>: removing the output would take the sources with it.
    /// </summary>
    [Test]
    [Arguments("content", ".")]
    [Arguments("content", "..")]
    [Arguments("content/pages", "content")]
    public async Task Validate_Test_OutputDirectoryAboveContentDirectory_Fails(string content, string output)
    {
        // Arrange
        var options = new SiteOptions { ContentDirectory = content, OutputDirectory = output };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains(nameof(SiteOptions.OutputDirectory));
        }
    }

    /// <summary>
    ///     Verifies that an output directory resolving to the content root is rejected even when the content
    ///     directory lies elsewhere, because a build deletes every file in it that it did not write and
    ///     <c>gelyn clean</c> deletes it whole.
    /// </summary>
    /// <remarks>
    ///     A directory above the content root would also hold the sibling content directory, so the boundary
    ///     check rejects it first. The last case differs from the content root in case alone, which is rejected
    ///     on every platform as the safe direction.
    /// </remarks>
    [Test]
    [Arguments(".")]
    [Arguments("../SITE")]
    public async Task Validate_Test_OutputDirectoryAtContentRoot_Fails(string output)
    {
        // Arrange
        var options = new SiteOptions { ContentDirectory = "../notes", OutputDirectory = output };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).IsEqualTo($"{nameof(SiteOptions.OutputDirectory)} must not resolve to or above the content root.");
        }
    }

    /// <summary>
    ///     Verifies that an output directory holding a configuration file that lies outside the content root is
    ///     rejected under a message naming the file, because the build would delete the file it just read.
    /// </summary>
    [Test]
    public async Task Validate_Test_OutputDirectoryHoldingTheConfigurationFile_Fails()
    {
        // Arrange
        var options = new SiteOptions { ContentDirectory = "../notes", OutputDirectory = "../shared" };
        string configuration = Path.GetFullPath("/shared/gelyn.json");

        // Act
        ValidateOptionsResult result = Validate(options, configuration);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).IsEqualTo($"{nameof(SiteOptions.OutputDirectory)} must not contain the configuration file '{configuration}'.");
        }
    }

    /// <summary>
    ///     Verifies that an output directory beside the content root is accepted when it holds neither the root
    ///     nor the configuration file, which is the layout of a site built from content kept elsewhere.
    /// </summary>
    [Test]
    public async Task Validate_Test_OutputDirectoryBesideContentRoot_Succeeds()
    {
        // Arrange
        var options = new SiteOptions { ContentDirectory = "../notes", OutputDirectory = "../www" };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    #endregion // Validate Tests

    #region Giscus Tests

    /// <summary>
    ///     Verifies that leaving the giscus backend unset is valid, because it is what renders no comments.
    /// </summary>
    [Test]
    public async Task Validate_Test_AbsentGiscus_Succeeds()
    {
        // Arrange
        var options = new SiteOptions();

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.Giscus).IsNull();
            await Assert.That(result.Succeeded).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that a giscus backend declaring all four members is accepted.
    /// </summary>
    [Test]
    public async Task Validate_Test_Giscus_Succeeds()
    {
        // Arrange
        var options = new SiteOptions { Giscus = Giscus() };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    /// <summary>
    ///     Verifies that an absent or blank id or category is rejected under a message naming the member at fault.
    /// </summary>
    [Test]
    [Arguments(nameof(GiscusOptions.RepoId), null)]
    [Arguments(nameof(GiscusOptions.RepoId), "")]
    [Arguments(nameof(GiscusOptions.RepoId), "   ")]
    [Arguments(nameof(GiscusOptions.Category), null)]
    [Arguments(nameof(GiscusOptions.Category), "")]
    [Arguments(nameof(GiscusOptions.Category), "   ")]
    [Arguments(nameof(GiscusOptions.CategoryId), null)]
    [Arguments(nameof(GiscusOptions.CategoryId), "")]
    [Arguments(nameof(GiscusOptions.CategoryId), "   ")]
    public async Task Validate_Test_BlankGiscusSetting_Fails(string setting, string? value)
    {
        // Arrange
        var options = new SiteOptions { Giscus = Giscus(setting, value) };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains($"Giscus.{setting}");
        }
    }

    /// <summary>
    ///     Verifies that an absent repository, or one not written as <c>owner/name</c>, is rejected under a
    ///     message naming the member at fault.
    /// </summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("octocat")]
    [Arguments("octocat/")]
    [Arguments("/blog")]
    [Arguments("octo cat/blog")]
    [Arguments("octocat/blog/extra")]
    [Arguments("octocat/blog\n")]
    public async Task Validate_Test_MalformedGiscusRepo_Fails(string? repo)
    {
        // Arrange
        var options = new SiteOptions { Giscus = Giscus(nameof(GiscusOptions.Repo), repo) };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains("Giscus.Repo");
        }
    }

    #endregion // Giscus Tests

    #region Theme Tests

    /// <summary>
    ///     Verifies that leaving the theme unset is valid, because it is what applies none.
    /// </summary>
    [Test]
    public async Task Validate_Test_AbsentTheme_Succeeds()
    {
        // Arrange
        var options = new SiteOptions();

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.Theme).IsNull();
            await Assert.That(result.Succeeded).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that every bundled theme is accepted.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(BundledThemes))]
    public async Task Validate_Test_BundledTheme_Succeeds(string theme)
    {
        // Arrange
        var options = new SiteOptions { Theme = theme };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    /// <summary>
    ///     Verifies that a name no bundled theme carries is rejected under a message listing the bundled ones,
    ///     including an empty name and a name that differs from a bundled one in case alone.
    /// </summary>
    [Test]
    [Arguments("plain")]
    [Arguments("Modern")]
    [Arguments("")]
    public async Task Validate_Test_ThemeNotBundled_Fails(string theme)
    {
        // Arrange
        var options = new SiteOptions { Theme = theme };
        string expected = $"{nameof(SiteOptions.Theme)} must name a bundled theme: {string.Join(", ", ThemeCatalog.Names)}.";

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains(expected);
        }
    }

    #endregion // Theme Tests

    #region Port Tests

    /// <summary>
    ///     Verifies that the default port and both ends of the range a listener can bind are accepted.
    /// </summary>
    [Test]
    [Arguments(9_624)]
    [Arguments(1)]
    [Arguments(65_535)]
    public async Task Validate_Test_PortInRange_Succeeds(int port)
    {
        // Arrange
        var options = new SiteOptions { Port = port };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    /// <summary>
    ///     Verifies that a port no listener can bind is rejected before the server starts, instead of
    ///     surfacing as an error code from the listener.
    /// </summary>
    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(65_536)]
    public async Task Validate_Test_PortOutOfRange_Fails(int port)
    {
        // Arrange
        var options = new SiteOptions { Port = port };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains($"{nameof(SiteOptions.Port)} must be between 1 and 65535.");
        }
    }

    #endregion // Port Tests

    #region Language Tests

    /// <summary>
    ///     Verifies that language tags of the BCP 47 shape are accepted, from a bare language to one with a
    ///     script and a region, and a private-use tag.
    /// </summary>
    [Test]
    [Arguments("en")]
    [Arguments("de-DE")]
    [Arguments("zh-Hant-TW")]
    [Arguments("x-klingon")]
    public async Task Validate_Test_LanguageTag_Succeeds(string language)
    {
        // Arrange
        var options = new SiteOptions { Language = language };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    /// <summary>
    ///     Verifies that a value outside the BCP 47 shape is rejected, such as the gettext value a Linux desktop
    ///     sets in <c>LANGUAGE</c>, which would otherwise reach the lang attribute of every page.
    /// </summary>
    [Test]
    [Arguments("de_DE:de")]
    [Arguments("en_US")]
    [Arguments("en-")]
    [Arguments("en US")]
    [Arguments("englishlanguage")]
    [Arguments("en\n")]
    public async Task Validate_Test_NotALanguageTag_Fails(string language)
    {
        // Arrange
        var options = new SiteOptions { Language = language };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains($"{nameof(SiteOptions.Language)} must be a BCP 47 language tag, such as 'en' or 'de-DE'.");
        }
    }

    #endregion // Language Tests

    #region Version Tests

    /// <summary>
    ///     Verifies that a version of three numbers is accepted for every library a page can load.
    /// </summary>
    [Test]
    [Arguments(nameof(SiteOptions.KaTeXVersion), "0.19.0")]
    [Arguments(nameof(SiteOptions.MermaidVersion), "12.1.0")]
    [Arguments(nameof(SiteOptions.ShikiVersion), "10.20.300")]
    public async Task Validate_Test_Version_Succeeds(string setting, string version)
    {
        // Arrange
        SiteOptions options = Blank(setting, version);

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    /// <summary>
    ///     Verifies that a value the schema rejects is rejected by the build as well, including the trailing
    ///     newline and the non-ASCII digits that the .NET regex dialect would otherwise accept.
    /// </summary>
    [Test]
    [Arguments(nameof(SiteOptions.KaTeXVersion), "latest")]
    [Arguments(nameof(SiteOptions.KaTeXVersion), "4.6.0 ")]
    [Arguments(nameof(SiteOptions.KaTeXVersion), "4.6,0")]
    [Arguments(nameof(SiteOptions.MermaidVersion), "12.1")]
    [Arguments(nameof(SiteOptions.MermaidVersion), "12.1.0-beta.1")]
    [Arguments(nameof(SiteOptions.ShikiVersion), "4.6.0\n")]
    [Arguments(nameof(SiteOptions.ShikiVersion), "\U00000664.6.0")]
    [Arguments(nameof(SiteOptions.ShikiVersion), "4.6.0\";alert(1);\"")]
    public async Task Validate_Test_NotAVersion_Fails(string setting, string version)
    {
        // Arrange
        SiteOptions options = Blank(setting, version);

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains($"{setting} must be a version such as");
        }
    }

    #endregion // Version Tests

    #region Exclude Pattern Tests

    /// <summary>
    ///     Verifies that a pattern listed twice is rejected, as the schema's <c>uniqueItems</c> rejects it.
    /// </summary>
    [Test]
    public async Task Validate_Test_DuplicateExcludePattern_Fails()
    {
        // Arrange
        var options = new SiteOptions { ExcludePatterns = ["*.tmp", "drafts", "*.tmp"] };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains($"{nameof(SiteOptions.ExcludePatterns)} must not list a pattern twice.");
        }
    }

    /// <summary>
    ///     Verifies that patterns differing in case alone are distinct, as they are to the schema.
    /// </summary>
    [Test]
    public async Task Validate_Test_ExcludePatternsDifferingInCase_Succeed()
    {
        // Arrange
        var options = new SiteOptions { ExcludePatterns = ["Drafts", "drafts"] };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    #endregion // Exclude Pattern Tests

    #region Helpers

    /// <summary>
    ///     The names of the bundled themes.
    /// </summary>
    public static IEnumerable<string> BundledThemes() => ThemeCatalog.Names;

    // An explicit content root is required: the stub defaults it to string.Empty, which would leave
    // GetFullPath resolving the relative settings against the working directory.
    private static ValidateOptionsResult Validate(SiteOptions options, string configuration = "/site/gelyn.json")
    {
        var environment = new HostEnvironmentStub { ContentRootPath = Path.GetFullPath("/site") };
        var file = new ConfigurationFileStatus { Path = Path.GetFullPath(configuration), Optional = true, Exists = true, Failure = null };

        return new SiteOptionsValidator(environment, file).Validate(name: null, options);
    }

    // Assigned by name, because the alternative is one test per setting; every setting under test is a
    // string, so no conversion is involved.
    private static SiteOptions Blank(string setting, string value)
    {
        var options = new SiteOptions();

        typeof(SiteOptions).GetProperty(setting)!.SetValue(options, value);

        return options;
    }

    // A valid backend, with the member named by setting replaced by value.
    private static GiscusOptions Giscus(string? setting = null, string? value = null) => new()
    {
        Repo = setting is nameof(GiscusOptions.Repo) ? value : "octocat/blog",
        RepoId = setting is nameof(GiscusOptions.RepoId) ? value : "R_kgDOxxxxxxx",
        Category = setting is nameof(GiscusOptions.Category) ? value : "Comments",
        CategoryId = setting is nameof(GiscusOptions.CategoryId) ? value : "DIC_kwDOxxxxxxxxxxxx",
    };

    #endregion
}
