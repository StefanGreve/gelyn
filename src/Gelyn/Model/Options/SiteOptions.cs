using System;
using System.Collections.Generic;

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
    public string Title { get; set; } = Tool.Name;

    /// <summary>
    ///     The language of the generated pages, written to the <c>lang</c> attribute of every document.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    ///     The name written to the author meta tag, or <see langword="null"/> to write no author at all.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    ///     The description written to the description meta tag by any page whose front matter declares none,
    ///     or <see langword="null"/> to leave such pages without a description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     The absolute URL the finished site is served from, such as <c>https://example.com/blog</c>, or
    ///     <see langword="null"/> when it is served from the root of a domain.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    ///     The version of KaTeX requested from jsDelivr, such as <c>0.19.0</c>.
    /// </summary>
    public string KaTeXVersion { get; set; } = "0.19.0";

    /// <summary>
    ///     The directory the site is built from, holding the Markdown sources and anything published beside
    ///     them. Relative values are resolved against the content root, which is the working directory unless
    ///     <c>DOTNET_CONTENTROOT</c> points somewhere else.
    /// </summary>
    public string ContentDirectory { get; set; } = "content";

    /// <summary>
    ///     The directory the generated site is written to. Relative values are resolved against the content
    ///     root, in the same way as <see cref="ContentDirectory"/>.
    /// </summary>
    public string OutputDirectory { get; set; } = "_site";

    /// <summary>
    ///     Patterns naming the entries the build leaves out, matched case insensitively against the name of
    ///     each file and directory rather than against its path, with <c>*</c> and <c>?</c> as the only
    ///     wildcards. A directory that matches is not descended into.
    /// </summary>
    public IReadOnlyList<string> ExcludePatterns { get; set; } = [];
}
