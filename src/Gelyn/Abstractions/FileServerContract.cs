using System.Net;

namespace Gelyn.Abstractions;

/// <summary>
///     Answers HTTP requests for the files one build produced.
/// </summary>
internal abstract class FileServerContract
{
    /// <summary>
    ///     Serves one directory until the operation is cancelled.
    /// </summary>
    /// <param name="outputDirectory">
    ///     The directory the generated site was written to.
    /// </param>
    /// <param name="port">
    ///     The port to serve on.
    /// </param>
    /// <param name="launchBrowser">
    ///     Indicates whether the announced address is opened in the default browser.
    /// </param>
    /// <param name="refreshSeconds">
    ///     The interval an HTML response asks the browser to reload itself after, or <see langword="null"/>
    ///     when no response carries the header.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     A task that completes once the server has stopped.
    /// </returns>
    /// <exception cref="PlatformNotSupportedException">
    ///     The platform carries no usable <see cref="HttpListener"/> implementation.
    /// </exception>
    public abstract Task ServeAsync(
        string outputDirectory,
        int port,
        bool launchBrowser,
        int? refreshSeconds,
        CancellationToken cancellationToken);
}
