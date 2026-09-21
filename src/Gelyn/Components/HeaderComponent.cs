using System.Net;

using Gelyn.Abstractions;
using Gelyn.Model.Options;

using Microsoft.Extensions.Options;

namespace Gelyn.Components;

/// <summary>
///     The banner rendered at the top of every page.
/// </summary>
public sealed class HeaderComponent : ComponentContract
{
    private readonly IOptions<SiteOptions> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HeaderComponent"/> class.
    /// </summary>
    /// <param name="options">
    ///     Supplies the site title.
    /// </param>
    public HeaderComponent(IOptions<SiteOptions> options)
    {
        this._options = options;
    }

    /// <inheritdoc/>
    public override string Render()
    {
        return $"""
            <header>
              <a href="/">{WebUtility.HtmlEncode(this._options.Value.Title)}</a>
            </header>
        """;
    }
}