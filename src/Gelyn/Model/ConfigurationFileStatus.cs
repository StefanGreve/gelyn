namespace Gelyn.Model;

/// <summary>
///     What the first load of the configuration file found.
/// </summary>
internal sealed record ConfigurationFileStatus
{
    /// <summary>
    ///     The full path of the file the loader looked for, resolved against the directory it reads relative
    ///     paths from, whether the file exists or not.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///     Indicates whether the file may be absent, which is the case when no path was given and the defaults
    ///     apply without it.
    /// </summary>
    public required bool Optional { get; init; }

    /// <summary>
    ///     Indicates whether the file exists, as the file provider the loader reads it through answers, so a
    ///     file the loader cannot reach counts as absent.
    /// </summary>
    public required bool Exists { get; init; }

    /// <summary>
    ///     The exception the first load raised and held back, or <see langword="null"/> when the file loaded
    ///     or was optional and absent.
    /// </summary>
    public required Exception? Failure { get; init; }
}
