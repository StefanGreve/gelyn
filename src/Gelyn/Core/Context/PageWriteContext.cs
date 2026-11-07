namespace Gelyn.Core.Context;

/// <summary>
///     What one pass over the rendered pages produced.
/// </summary>
/// <remarks>
///     The pass composes and writes pages on several threads at once, so composing has no wall time of its
///     own. <see cref="ComposeTicks"/> is the share of the pass's wall time that matches the share of thread
///     time spent composing. The caller attributes the rest of the pass to writing.
/// </remarks>
internal sealed record PageWriteContext
{
    /// <summary>
    ///     The output-relative path of each page that was written, in the order the pages were passed in.
    /// </summary>
    public required IReadOnlyList<string> Written { get; init; }

    /// <summary>
    ///     The ticks attributed to composing the documents.
    /// </summary>
    public required long ComposeTicks { get; init; }
}
