namespace Gelyn.Model.Options;

/// <summary>
///     Locations and metadata the generator works with, bound from the <c>Site</c> configuration section.
/// </summary>
public sealed class SiteOptions
{
    /// <summary>
    ///     The name shown in the header and used when a page declares no title.
    /// </summary>
    public string SiteTitle { get; set; } = "Gelyn";

    /// <summary>
    ///     The directory holding the Markdown sources. Relative values are resolved against the working directory.
    /// </summary>
    public string ContentDirectory { get; set; } = "content";

    /// <summary>
    ///     The directory the generated site is written to. Relative values are resolved against the working directory.
    /// </summary>
    public string OutputDirectory { get; set; } = "_site";
}
