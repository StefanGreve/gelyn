using Gelyn.Model;

namespace Gelyn.Abstractions;

/// <summary>
///     Decides which file in the output directory answers a given request path.
/// </summary>
internal abstract class StaticFileResolverContract
{
    /// <summary>
    ///     Resolves one request path against the output directory.
    /// </summary>
    /// <param name="outputDirectory">
    ///     The directory the generated site was written to, which is the only directory a request can reach.
    /// </param>
    /// <param name="requestPath">
    ///     The path of the requested URL, percent-encoded as the client sent it.
    /// </param>
    /// <returns>
    ///     The file that answers the request together with the status and media type to answer under, or a
    ///     404 when nothing in the output directory does.
    /// </returns>
    public abstract ServedFile Resolve(string outputDirectory, string requestPath);
}
