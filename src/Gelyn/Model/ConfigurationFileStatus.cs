namespace Gelyn.Model;

/// <summary>
///     What the first load of the configuration file found.
/// </summary>
internal sealed record ConfigurationFileStatus
{
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
