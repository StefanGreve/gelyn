namespace Gelyn.Abstractions;

/// <summary>
///     Rebuilds the site whenever a source file or the configuration file changes.
/// </summary>
internal abstract class SiteWatcherContract
{
    /// <summary>
    ///     Watches the content tree and the configuration file until the operation is cancelled, rebuilding
    ///     the site once per burst of changes.
    /// </summary>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     A task that completes once the loop has stopped.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     The content directory or the directory holding the configuration file does not exist.
    /// </exception>
    public abstract Task WatchAsync(CancellationToken cancellationToken);
}
