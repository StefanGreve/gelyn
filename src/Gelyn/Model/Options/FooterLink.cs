namespace Gelyn.Model.Options;

/// <summary>
///     One labelled link in the footer of every page.
/// </summary>
/// <remarks>
///     A member the configuration omits stays <see langword="null"/>, which is not a usable value.
/// </remarks>
/// <seealso cref="SiteOptions.FooterLinks"/>
internal sealed class FooterLink
{
    /// <summary>
    ///     The text the link reads as.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    ///     The absolute http or https URL the link addresses.
    /// </summary>
    public Uri? Url { get; set; }
}
