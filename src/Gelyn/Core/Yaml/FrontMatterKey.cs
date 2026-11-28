using Gelyn.Model;

namespace Gelyn.Core.Yaml;

/// <summary>
///     The keys of a front matter block, one for each member of <see cref="FrontMatter"/>.
/// </summary>
internal enum FrontMatterKey
{
    /// <summary>
    ///     A key that no member reads.
    /// </summary>
    Unknown,

    /// <summary>
    ///     The key <c>title</c>.
    /// </summary>
    Title,

    /// <summary>
    ///     The key <c>date</c>.
    /// </summary>
    Date,

    /// <summary>
    ///     The key <c>revised</c>.
    /// </summary>
    Revised,

    /// <summary>
    ///     The key <c>description</c>.
    /// </summary>
    Description,

    /// <summary>
    ///     The key <c>enableMathRendering</c>.
    /// </summary>
    EnableMathRendering,

    /// <summary>
    ///     The key <c>enableDiagramRendering</c>.
    /// </summary>
    EnableDiagramRendering,

    /// <summary>
    ///     The key <c>enableSyntaxHighlighting</c>.
    /// </summary>
    EnableSyntaxHighlighting,

    /// <summary>
    ///     The key <c>enableComments</c>.
    /// </summary>
    EnableComments,

    /// <summary>
    ///     The key <c>enableFeed</c>.
    /// </summary>
    EnableFeed,

    /// <summary>
    ///     The key <c>stylesheets</c>.
    /// </summary>
    Stylesheets,

    /// <summary>
    ///     The key <c>scripts</c>.
    /// </summary>
    Scripts,
}
