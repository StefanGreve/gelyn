using System.Diagnostics.CodeAnalysis;
using System.Net;

using Gelyn.Abstractions;
using Gelyn.Internals;

namespace Gelyn.Components;

/// <summary>
///     The banner rendered at the top of every page.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = Justifications.ByDesign)]
internal sealed class HeaderComponent : ComponentContract
{
    private readonly SiteOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HeaderComponent"/> class.
    /// </summary>
    /// <param name="options">
    ///     Supplies the site title.
    /// </param>
    public HeaderComponent(SiteOptions options)
    {
        this._options = options;
    }

    /// <inheritdoc/>
    public override string Render()
    {
        return $"""
            <header>
              <a href="/">{WebUtility.HtmlEncode(this._options.SiteTitle)}</a>
            </header>
        """;
    }
}
