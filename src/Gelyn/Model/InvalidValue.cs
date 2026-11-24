namespace Gelyn.Model;

/// <summary>
///     A value a front matter block declares that the member meant for it rejects, such as a date not written
///     as <c>yyyy-MM-dd</c>.
/// </summary>
internal sealed record InvalidValue
{
    /// <summary>
    ///     The key the value is declared under, as written in the block.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     The one-based line of the source file the value is declared on.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    ///     Why the value was rejected, as the member's converter reported it.
    /// </summary>
    public required string Message { get; init; }
}
