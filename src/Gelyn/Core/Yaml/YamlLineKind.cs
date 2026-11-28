namespace Gelyn.Core.Yaml;

/// <summary>
///     The kinds of line that a block is made of.
/// </summary>
internal enum YamlLineKind
{
    /// <summary>
    ///     No line is left.
    /// </summary>
    End,

    /// <summary>
    ///     A line of spaces and tabs alone.
    /// </summary>
    Blank,

    /// <summary>
    ///     A line that holds a comment alone.
    /// </summary>
    Comment,

    /// <summary>
    ///     A line of three dashes or three dots at the first column, which closes the block.
    /// </summary>
    Marker,

    /// <summary>
    ///     A line that holds a key, a value or an entry.
    /// </summary>
    Content,
}
