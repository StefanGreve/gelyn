using System.Text;

using Gelyn.Core.Internals;

namespace Gelyn.Core;

/// <summary>
///     Defines methods that emit Common Log Format output.
/// </summary>
/// <remarks>
///     One entry is Apache's <c>%h %l %u %t "%r" %&gt;s %b</c>, which is the shape every log analyzer reads.
///     A field the caller has no value for is passed as <see langword="null"/> and written as a hyphen,
///     which is the convention the format reserves for exactly that.
/// </remarks>
/// <seealso href="https://www.w3.org/Daemon/User/Config/Logging.html#common-logfile-format">
///     The common logfile format.
/// </seealso>
internal static class CommonLogFormat
{
    /// <summary>
    ///     The date and time of one entry, less the UTC offset that follows it.
    /// </summary>
    private const string TimestampFormat = "dd/MMM/yyyy:HH:mm:ss";

    /// <summary>
    ///     The two-digit form the hours and the minutes of the UTC offset each take.
    /// </summary>
    private const string OffsetComponentFormat = "D2";

    /// <summary>
    ///     The hyphen a field takes when the caller has no value for it.
    /// </summary>
    private const string Unavailable = "-";

    /// <summary>
    ///     Composes the line announcing that the server is ready.
    /// </summary>
    /// <remarks>
    ///     Strictly speaking, this line is <i>not</i> part of the CLF specification; its wording was borrowed
    ///     from Python's <c>http.server</c> module.
    /// </remarks>
    /// <param name="host">
    ///     The host the server is listening on.
    /// </param>
    /// <param name="port">
    ///     The port it is listening on.
    /// </param>
    /// <param name="address">
    ///     The absolute address <paramref name="host"/> and <paramref name="port"/> resolve to. The caller
    ///     passes it in, so that the line cannot announce an address that was never bound.
    /// </param>
    /// <returns>
    ///     The line, without a trailing newline.
    /// </returns>
    internal static string BuildHeader(string host, int port, string address)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(address);

        return string.Create(Tool.Culture, $"Serving HTTP on {host} port {port} ({address}) ...");
    }

    /// <summary>
    ///     Composes one Common Log Format entry.
    /// </summary>
    /// <param name="client">
    ///     The address of the client that made the request.
    /// </param>
    /// <param name="identity">
    ///     The identity the client reported, which no server has received since <c>identd</c> fell out of
    ///     use.
    /// </param>
    /// <param name="user">
    ///     The user the request authenticated as.
    /// </param>
    /// <param name="timestamp">
    ///     The local time the request was answered. The offset it carries is part of the field, which is why
    ///     the parameter is not a <see cref="DateTime"/>.
    /// </param>
    /// <param name="method">
    ///     The request method.
    /// </param>
    /// <param name="target">
    ///     The request target, as the client sent it and including any query string.
    /// </param>
    /// <param name="protocol">
    ///     The HTTP version the request was made under. Only its major and minor component reach the entry.
    /// </param>
    /// <param name="status">
    ///     The status the response carried.
    /// </param>
    /// <param name="bytes">
    ///     The size of the response body, excluding the headers. Apache writes a hyphen for an empty body and
    ///     calls that the CLF form, while nginx writes a zero, so both are accepted here and the caller
    ///     chooses by passing <see langword="null"/> or the count.
    /// </param>
    /// <returns>
    ///     The entry, without a trailing newline.
    /// </returns>
    internal static string BuildMessage(
        string? client,
        string? identity,
        string? user,
        DateTimeOffset timestamp,
        string method,
        string? target,
        Version protocol,
        int status,
        long? bytes)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(protocol);

        var entry = new StringBuilder();

        entry.Append(client ?? Unavailable);
        entry.Append(' ').Append(identity ?? Unavailable);
        entry.Append(' ').Append(user ?? Unavailable);

        TimeSpan offset = timestamp.Offset;
        TimeSpan magnitude = offset.Duration();

        // Written out by hand, because no custom specifier yields the colonless offset the format asks for:
        // zzz renders +02:00 where this field needs +0200.
        entry.Append(" [");
        entry.Append(timestamp.ToString(TimestampFormat, Tool.Culture));
        entry.Append(' ');
        entry.Append(offset < TimeSpan.Zero ? '-' : '+');
        entry.Append(magnitude.Hours.ToString(OffsetComponentFormat, Tool.Culture));
        entry.Append(magnitude.Minutes.ToString(OffsetComponentFormat, Tool.Culture));
        entry.Append(']');

        // The request line is one field, which is what the quotes delimit.
        entry.Append(" \"").Append(method);
        entry.Append(' ').Append(target ?? Unavailable);
        entry.Append(" HTTP/").Append(protocol.ToString(2)).Append('"');

        entry.Append(' ').Append(status.ToString(Tool.Culture));
        entry.Append(' ').Append(bytes?.ToString(Tool.Culture) ?? Unavailable);

        return entry.ToString();
    }
}
