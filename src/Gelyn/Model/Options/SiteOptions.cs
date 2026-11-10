using System.ComponentModel.DataAnnotations;
using System.Net;

using Gelyn.Attributes;
using Gelyn.Core.Internals;

namespace Gelyn.Model.Options;

/// <summary>
///     Locations and metadata the generator works with, bound from the root of the configuration.
/// </summary>
/// <seealso cref="Validators.SiteOptionsValidator"/>
internal sealed class SiteOptions
{
    /// <summary>
    ///     The name of the site.
    /// </summary>
    /// <example>
    ///     Gelyn
    /// </example>
    /// <example>
    ///     My Notebook
    /// </example>
    public string Title { get; set; } = Tool.Name;

    /// <summary>
    ///     The language of the generated pages, written to the <c>lang</c> attribute of every document as a
    ///     BCP 47 tag.
    /// </summary>
    /// <example>
    ///     en
    /// </example>
    /// <example>
    ///     en-US
    /// </example>
    /// <example>
    ///     de-DE
    /// </example>
    public string Language { get; set; } = "en";

    /// <summary>
    ///     The name written to the author meta tag, or <see langword="null"/> to leave the tag out of every
    ///     document.
    /// </summary>
    /// <example>
    ///     Stefan Greve
    /// </example>
    public string? Author { get; set; }

    /// <summary>
    ///     The description written to the description meta tag by any page whose front matter declares none,
    ///     or <see langword="null"/> to leave the tag out of such pages.
    /// </summary>
    /// <example>
    ///     Notes on software engineering.
    /// </example>
    public string? Description { get; set; }

    /// <summary>
    ///     The absolute http or https URL the finished site is served from, such as
    ///     <c>https://example.com/blog</c>, or <see langword="null"/> when no absolute URL is known. Its path
    ///     prefixes every link, and is empty for <see langword="null"/> and for the root of a domain. Its origin
    ///     gives each page a canonical URL.
    /// </summary>
    /// <example>
    ///     https://example.com
    /// </example>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    ///     Indicates whether the build writes a syndication feed of the pages whose front matter declares
    ///     <c>enableFeed</c>. Every entry carries an absolute URL, so the feed requires <see cref="BaseUrl"/>
    ///     and is not written while it is <see langword="null"/>.
    /// </summary>
    /// <example>
    ///     true
    /// </example>
    public bool EnableFeed { get; set; }

    /// <summary>
    ///     Indicates whether the build writes a sitemap of the pages it publishes. Every location in it is an
    ///     absolute URL, so the sitemap requires <see cref="BaseUrl"/> and is not written while it is
    ///     <see langword="null"/>.
    /// </summary>
    /// <example>
    ///     true
    /// </example>
    public bool EnableSitemap { get; set; }

    /// <summary>
    ///     The links listed in the footer of every page, in the order declared here. The feed and the sitemap
    ///     follow them whenever the build publishes those documents.
    /// </summary>
    /// <example>
    ///     [
    ///       { "Label": "GitHub", "Url": "https://github.com/StefanGreve" },
    ///       { "Label": "LinkedIn", "Url": "https://www.linkedin.com/in/example" }
    ///     ]
    /// </example>
    public IReadOnlyList<FooterLink> FooterLinks { get; set; } = [];

    /// <summary>
    ///     The name of the bundled theme every page is styled with, or <see langword="null"/> to apply none.
    ///     Its stylesheet and script are written to the root of the output directory, and every page links them
    ///     ahead of the site's own.
    /// </summary>
    /// <example>
    ///     modern
    /// </example>
    public string? Theme { get; set; }

    /// <summary>
    ///     The version of KaTeX that a page whose front matter sets <c>enableMathRendering</c> requests from
    ///     jsDelivr, such as <c>0.19.0</c>.
    /// </summary>
    /// <example>
    ///     0.19.0
    /// </example>
    [RegularExpression(@"^\d+\.\d+\.\d+$")]
    public string KaTeXVersion { get; set; } = "0.19.0";

    /// <summary>
    ///     The version of Mermaid that a page whose front matter sets <c>enableDiagramRendering</c> requests from
    ///     jsDelivr, such as <c>12.1.0</c>.
    /// </summary>
    /// <example>
    ///     12.1.0
    /// </example>
    [RegularExpression(@"^\d+\.\d+\.\d+$")]
    public string MermaidVersion { get; set; } = "12.1.0";

    /// <summary>
    ///     The version of Shiki that a page whose front matter sets <c>enableSyntaxHighlighting</c> requests from
    ///     jsDelivr, such as <c>4.6.0</c>.
    /// </summary>
    /// <example>
    ///     4.6.0
    /// </example>
    [RegularExpression(@"^\d+\.\d+\.\d+$")]
    public string ShikiVersion { get; set; } = "4.6.0";

    /// <summary>
    ///     The giscus backend a page declaring <c>enableComments</c> renders a comment thread from, or
    ///     <see langword="null"/> to render none, whatever a page declares. Each thread is a GitHub discussion in
    ///     the repository <see cref="GiscusOptions.Repo"/> names.
    /// </summary>
    /// <example>
    ///     {
    ///       "Repo": "octocat/blog",
    ///       "RepoId": "R_kgDOxxxxxxx",
    ///       "Category": "Comments",
    ///       "CategoryId": "DIC_kwDOxxxxxxxxxxxx"
    ///     }
    /// </example>
    public GiscusOptions? Giscus { get; set; }

    /// <summary>
    ///     The directory the site is built from, holding the Markdown sources and anything published beside
    ///     them. Relative values are resolved against the content root, which is the working directory unless
    ///     <c>DOTNET_CONTENTROOT</c> points elsewhere.
    /// </summary>
    /// <example>
    ///     content
    /// </example>
    /// <example>
    ///     src/pages
    /// </example>
    public string ContentDirectory { get; set; } = "content";

    /// <summary>
    ///     The directory the generated site is written to. Relative values are resolved against the content
    ///     root, as for <see cref="ContentDirectory"/>. Each build overwrites the files it publishes there and
    ///     deletes the ones no source accounts for.
    /// </summary>
    /// <example>
    ///     _site
    /// </example>
    /// <example>
    ///     public
    /// </example>
    public string OutputDirectory { get; set; } = "_site";

    /// <summary>
    ///     Patterns naming the entries the build leaves out, matched case insensitively against the name of
    ///     each file and directory, never against its path, with <c>*</c> and <c>?</c> as the only
    ///     wildcards. A directory that matches is not descended into.
    /// </summary>
    /// <example>
    ///     ["*.tmp", "drafts", "*.xcf"]
    /// </example>
    [UniqueItems]
    public IReadOnlyList<string> ExcludePatterns { get; set; } = [];

    /// <summary>
    ///     The loopback port <c>gelyn serve</c> listens on, read once when the server starts.
    /// </summary>
    /// <remarks>
    ///     The default has to bind without privileges, which rules out everything below 1024, and stay clear
    ///     of 49152-65535, where the kernel draws the ephemeral ports it hands to outbound connections and
    ///     could take this one first.
    /// </remarks>
    /// <example>
    ///     8080
    /// </example>
    [Range(IPEndPoint.MinPort + 1, IPEndPoint.MaxPort)]
    public int Port { get; set; } = 9_624;
}
