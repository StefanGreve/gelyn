using System.Diagnostics.CodeAnalysis;
using System.Net;

using Gelyn.Abstractions;
using Gelyn.Internals;

using Microsoft.Extensions.Options;

namespace Gelyn.Components;

/// <summary>
///     The block rendered at the bottom of every page.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = Justifications.ByDesign)]
internal sealed class FooterComponent : ComponentContract
{
    private readonly SiteOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FooterComponent"/> class.
    /// </summary>
    /// <param name="options">
    ///     Supplies the site title.
    /// </param>
    public FooterComponent(IOptions<SiteOptions> options)
    {
        this._options = options.Value;
    }

    /// <inheritdoc/>
    public override string Render()
    {
        return $"""
            <footer>
              <p>{WebUtility.HtmlEncode(this._options.SiteTitle)}</p>
            </footer>
        """;
    }
}
