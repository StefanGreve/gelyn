namespace Gelyn.Model;

/// <summary>
///     A stylesheet or a script of a theme bundled with the tool.
/// </summary>
internal sealed record ThemeFile
{
    /// <summary>
    ///     The name the file is published under at the root of the output directory, such as <c>modern.css</c>.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    ///     The classification of the file, which is <see cref="ContentKind.Stylesheet"/> for a <c>.css</c> file
    ///     and <see cref="ContentKind.Script"/> for a <c>.js</c> file.
    /// </summary>
    public required ContentKind Kind { get; init; }

    /// <summary>
    ///     The name of the manifest resource that holds the bytes of the file, such as <c>themes/modern.css</c>.
    /// </summary>
    public required string ResourceName { get; init; }
}
