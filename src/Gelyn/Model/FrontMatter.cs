using Gelyn.Core.Internals;

using SharpYaml.Serialization;

namespace Gelyn.Model;

/// <summary>
///     Front matter declared in the block at the top of a content file.
/// </summary>
/// <remarks>
///     A property is <see langword="null"/> only when the block declares no value for it. A value declared as an
///     empty string is kept as one, so the two cases stay distinguishable. The properties carry a setter rather
///     than an <c>init</c> accessor because the generated deserialization code assigns them from an ordinary
///     statement, which C# permits only inside an object initializer or a constructor.
/// </remarks>
internal sealed record FrontMatter
{
    /// <summary>
    ///     The page title, or <see langword="null"/> when the block omits it.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     The publication date, or <see langword="null"/> when the block omits it.
    /// </summary>
    [YamlConverter(typeof(DateOnlyConverter))]
    public DateOnly? Date { get; set; }

    /// <summary>
    ///     The date the page was last revised, or <see langword="null"/> when the block omits it, which is the
    ///     case for a page that has not been revised since it was published.
    /// </summary>
    [YamlConverter(typeof(DateOnlyConverter))]
    public DateOnly? Revised { get; set; }

    /// <summary>
    ///     The page description, or <see langword="null"/> when the block omits it.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Indicates whether the page loads a math renderer, or <see langword="null"/> when the block omits it.
    /// </summary>
    public bool? EnableMathRendering { get; set; }

    /// <summary>
    ///     Indicates whether the page loads a diagram renderer, or <see langword="null"/> when the block omits
    ///     it.
    /// </summary>
    public bool? EnableDiagramRendering { get; set; }

    /// <summary>
    ///     Indicates whether the feed publishes the page, or <see langword="null"/> when the block omits it.
    /// </summary>
    public bool? EnableFeed { get; set; }
}
