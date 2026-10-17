using System.Net;

namespace Gelyn.Model;

/// <summary>
///     The resolution of one request against the output directory the preview server answers from.
/// </summary>
internal sealed record ServedFile
{
    /// <summary>
    ///     The absolute path of the file that answers the request, or <see langword="null"/> when the
    ///     response carries no body.
    /// </summary>
    public required string? Path { get; init; }

    /// <summary>
    ///     The media type the response declares, carrying a <c>charset</c> parameter for the types that
    ///     register one.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    ///     The status the response carries.
    /// </summary>
    public required HttpStatusCode StatusCode { get; init; }
}
