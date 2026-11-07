namespace Gelyn.Model.Options;

/// <summary>
///     One labelled link in the footer of every page.
/// </summary>
/// <seealso cref="SiteOptions.FooterLinks"/>
internal sealed class FooterLink
{
    /// <summary>
    ///     The text the link reads as, or <see langword="null"/> for a link whose configuration declares none,
    ///     which is not a usable value.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    ///     The absolute http or https URL the link addresses, or <see langword="null"/> for a link whose
    ///     configuration declares none, which is not a usable value.
    /// </summary>
    public Uri? Url { get; set; }
}
