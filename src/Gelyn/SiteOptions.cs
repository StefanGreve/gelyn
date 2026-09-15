using System.Diagnostics.CodeAnalysis;
using System.IO;

using Gelyn.Internals;

using Microsoft.Extensions.Hosting;

namespace Gelyn;

/// <summary>
///     Locations and metadata the generator works with, resolved from the working directory.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = Justifications.ByDesign)]
internal sealed class SiteOptions
{
    private const string ContentDirectoryName = "content";
    private const string OutputDirectoryName = "_site";

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteOptions"/> class.
    /// </summary>
    /// <param name="environment">
    ///     Supplies the content root, which the host sets to the current working directory.
    /// </param>
    public SiteOptions(IHostEnvironment environment)
    {
        this.ContentRoot = Path.Combine(environment.ContentRootPath, ContentDirectoryName);
        this.OutputRoot = Path.Combine(environment.ContentRootPath, OutputDirectoryName);
    }

    /// <summary>
    ///     The name shown in the header and used when a page declares no title.
    /// </summary>
    public string SiteTitle { get; } = "Gelyn";

    /// <summary>
    ///     The directory holding the Markdown sources.
    /// </summary>
    public string ContentRoot { get; }

    /// <summary>
    ///     The directory the generated site is written to.
    /// </summary>
    public string OutputRoot { get; }
}
