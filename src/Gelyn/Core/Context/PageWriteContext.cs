namespace Gelyn.Core.Context;

/// <summary>
///     What one pass over the rendered pages produced.
/// </summary>
/// <remarks>
///     The two durations are separate because the pass interleaves composing and writing per page, so
///     neither can be measured by timing the pass as a whole.
/// </remarks>
internal sealed record PageWriteContext
{
    /// <summary>
    ///     The output-relative path of each page that was written, in the order it was written.
    /// </summary>
    public required IReadOnlyList<string> Written { get; init; }

    /// <summary>
    ///     The ticks spent composing the documents.
    /// </summary>
    public required long ComposeTicks { get; init; }

    /// <summary>
    ///     The ticks spent writing them.
    /// </summary>
    public required long WriteTicks { get; init; }
}
