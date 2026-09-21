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
    private readonly IOptionsMonitor<SiteOptions> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FooterComponent"/> class.
    /// </summary>
    /// <param name="options">
    ///     Supplies the site title.
    /// </param>
    public FooterComponent(IOptionsMonitor<SiteOptions> options)
    {
        this._options = options;
    }

    /// <inheritdoc/>
    public override string Render()
    {
        SiteOptions options = this._options.CurrentValue;

        return $"""
            <footer>
              <p>{WebUtility.HtmlEncode(options.Title)}</p>
            </footer>
        """;
    }
}