using System.Net;

using Gelyn.Abstractions;
using Gelyn.Model.Options;

using Microsoft.Extensions.Options;

namespace Gelyn.Components;

/// <summary>
///     The block rendered at the bottom of every page.
/// </summary>
public sealed class FooterComponent : ComponentContract
{
    private readonly IOptions<SiteOptions> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FooterComponent"/> class.
    /// </summary>
    /// <param name="options">
    ///     Supplies the site title.
    /// </param>
    public FooterComponent(IOptions<SiteOptions> options)
    {
        this._options = options;
    }

    /// <inheritdoc/>
    public override string Render()
    {
        return $"""
            <footer>
              <p>{WebUtility.HtmlEncode(this._options.Value.Title)}</p>
            </footer>
        """;
    }
}