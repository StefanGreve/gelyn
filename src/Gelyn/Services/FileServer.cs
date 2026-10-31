using System.IO.Abstractions;
using System.Net;
using System.Net.Mime;

using Gelyn.Core;
using Gelyn.Core.Internals;
using Gelyn.Model;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Spectre.Console;

namespace Gelyn.Services;

/// <summary>
///     Answers HTTP requests for the files one build produced.
/// </summary>
/// <remarks>
///     Written with local development in mind. Do not use this class in a production environment: it binds
///     loopback and answers one request at a time. This narrow scope is what makes <see cref="HttpListener"/> a
///     suitable transport here, since none of the cautions in its reference applies to a preview server.
/// </remarks>
internal sealed class FileServer
{
    /// <summary>
    ///     The name of the <c>Refresh</c> header, spelled out because <see cref="HttpResponseHeader"/> covers
    ///     the fields HTTP defines and this one arrived through Netscape instead. No RFC specifies it: IANA
    ///     registers it as permanent against the HTML standard, where one algorithm reads both this header and
    ///     the matching <c>meta</c> pragma.
    /// </summary>
    /// <seealso href="https://html.spec.whatwg.org/multipage/semantics.html#shared-declarative-refresh-steps">
    ///     Shared declarative refresh steps.
    /// </seealso>
    private const string RefreshHeader = "Refresh";

    /// <summary>
    ///     The methods the server answers, in the form the <c>Allow</c> header of a refusal takes.
    /// </summary>
    private const string ReadMethods = WebRequestMethods.Http.Get + ", " + WebRequestMethods.Http.Head;

    private const string LoopbackAddress = "127.0.0.1";
    private const string LoopbackName = "localhost";

    private readonly StaticFileResolver _resolver;
    private readonly IFileSystem _fileSystem;
    private readonly IAnsiConsole _stdOut;
    private readonly IAnsiConsole _stdErr;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FileServer> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FileServer"/> class.
    /// </summary>
    /// <param name="resolver">
    ///     Resolves each request path to the file that answers it.
    /// </param>
    /// <param name="fileSystem">
    ///     The file system to work against.
    /// </param>
    /// <param name="stdOut">
    ///     The console the request log is written to.
    /// </param>
    /// <param name="stdErr">
    ///     The console the server writes its warnings to.
    /// </param>
    /// <param name="timeProvider">
    ///     Supplies the local time each request is stamped with.
    /// </param>
    /// <param name="logger">
    ///     Receives the records the server writes.
    /// </param>
    public FileServer(
        StaticFileResolver resolver,
        IFileSystem fileSystem,
        [FromKeyedServices(ConsoleTarget.StdOut)] IAnsiConsole stdOut,
        [FromKeyedServices(ConsoleTarget.StdErr)] IAnsiConsole stdErr,
        TimeProvider timeProvider,
        ILogger<FileServer> logger)
    {
        this._resolver = resolver;
        this._fileSystem = fileSystem;
        this._stdOut = stdOut;
        this._stdErr = stdErr;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <summary>
    ///     The address a reader is pointed at, which is the first of <see cref="Addresses"/>.
    /// </summary>
    /// <param name="port">
    ///     The port to serve on.
    /// </param>
    /// <returns>
    ///     The prefix, in the trailing-slash form <see cref="HttpListener.Prefixes"/> requires.
    /// </returns>
    internal static string Address(int port) =>
        string.Create(Tool.Culture, $"http://{LoopbackAddress}:{port}/");

    /// <summary>
    ///     Every address the server binds, all of them loopback.
    /// </summary>
    /// <remarks>
    ///     A request is routed by matching its <c>Host</c> header against the host of a prefix, and never by
    ///     the socket it arrived on, so loopback needs one prefix per spelling a reader might type. A request
    ///     matching none of them is answered by the listener itself, with a 404. <c>[::1]</c> is left out
    ///     because the managed implementation used outside Windows rejects a bracketed IPv6 literal from
    ///     <see cref="HttpListener.Start"/>, which would take the other prefixes down with it. A wildcard host
    ///     would make the header irrelevant at the cost of binding every interface.
    /// </remarks>
    /// <param name="port">
    ///     The port to serve on.
    /// </param>
    /// <returns>
    ///     The prefixes, in the trailing-slash form <see cref="HttpListener.Prefixes"/> requires.
    /// </returns>
    internal static IReadOnlyList<string> Addresses(int port) =>
    [
        Address(port),
        string.Create(Tool.Culture, $"http://{LoopbackName}:{port}/"),
    ];

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
    public async Task ServeAsync(
        string outputDirectory,
        int port,
        bool launchBrowser,
        int? refreshSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);

        if (!HttpListener.IsSupported)
            throw new PlatformNotSupportedException($"{nameof(HttpListener)} is not available on this platform.");

        string address = Address(port);
        string? refresh = refreshSeconds?.ToString(Tool.Culture);

        using var listener = new HttpListener();

        foreach (string prefix in Addresses(port))
            listener.Prefixes.Add(prefix);

        listener.Start();
        this.Write(CommonLogFormat.BuildHeader(LoopbackAddress, port, address));
        this._logger.LogServerStarted(outputDirectory, address);

        if (launchBrowser && !Browser.TryLaunch(address, out string? errorMessage))
            this._stdErr.MarkupLineInterpolated($"[yellow]warning:[/] no browser was opened: {errorMessage}");

        // GetContextAsync accepts no token, so tearing the listener down is what unblocks a pending accept.
        using CancellationTokenRegistration registration = cancellationToken.Register(listener.Stop);

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await this.AnswerAsync(context, outputDirectory, refresh, cancellationToken).ConfigureAwait(false);
        }
    }

    #region Helpers

    private async Task AnswerAsync(
        HttpListenerContext context,
        string outputDirectory,
        string? refresh,
        CancellationToken cancellationToken)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        string method = request.HttpMethod;
        string path = request.Url?.AbsolutePath ?? "/";
        string? client = ReadClient(request);

        try
        {
            if (!IsSupportedMethod(method))
            {
                // RFC 9110 requires a 405 to name the methods the target does support.
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                response.Headers[HttpResponseHeader.Allow] = ReadMethods;
                response.ContentLength64 = 0;

                this.Report(request, client, HttpStatusCode.MethodNotAllowed, written: 0);
            }
            else
            {
                ServedFile file = this._resolver.Resolve(outputDirectory, path);

                response.StatusCode = (int)file.StatusCode;
                response.ContentType = file.ContentType;

                if (refresh is not null && file.ContentType.StartsWith(MediaTypeNames.Text.Html, StringComparison.Ordinal))
                    response.AddHeader(RefreshHeader, refresh);

                long written = await this
                    .WriteBodyAsync(response, file, method, cancellationToken)
                    .ConfigureAwait(false);

                this.Report(request, client, file.StatusCode, written);
            }

            response.Close();
        }
        catch (Exception exception) when (exception is HttpListenerException or IOException or ObjectDisposedException)
        {
            // The browser hung up before the response was written, which a reload mid-response does
            // routinely, or the build replaced the file under the read. Neither is the server's to report.
            response.Abort();
        }
    }

    /// <returns>
    ///     The number of bytes written to the body, which is zero for a response that carries none and for a
    ///     <c>HEAD</c> whose declared length stands for a body it does not send.
    /// </returns>
    private async Task<long> WriteBodyAsync(
        HttpListenerResponse response,
        ServedFile file,
        string method,
        CancellationToken cancellationToken)
    {
        if (file.Path is null)
        {
            response.ContentLength64 = 0;
            return 0;
        }

        using Stream source = this._fileSystem.File.OpenRead(file.Path);
        response.ContentLength64 = source.Length;

        // A HEAD response declares the length the matching GET would send and sends none of it.
        if (method.Equals(WebRequestMethods.Http.Head, StringComparison.Ordinal))
            return 0;

        await source.CopyToAsync(response.OutputStream, cancellationToken).ConfigureAwait(false);

        return source.Length;
    }

    /// <summary>
    ///     Writes one request to the log in the Common Log Format.
    /// </summary>
    /// <remarks>
    ///     The identity and authenticated-user fields are always hyphens, because a loopback preview
    ///     identifies no client, and nginx hard-codes the first of them for the same reason. The trailing
    ///     count is the body alone, always as a number. Apache's <c>%b</c> writes a hyphen for an empty body,
    ///     while nginx reports the zero, which is what makes a page published empty visible at a glance.
    /// </remarks>
    /// <param name="request">
    ///     The request that was answered.
    /// </param>
    /// <param name="client">
    ///     The address the request came from, or <see langword="null"/> when it could not be read.
    /// </param>
    /// <param name="status">
    ///     The status it was answered under.
    /// </param>
    /// <param name="written">
    ///     The number of bytes written to the body.
    /// </param>
    private void Report(HttpListenerRequest request, string? client, HttpStatusCode status, long written)
    {
        this.Write(CommonLogFormat.BuildMessage(
            client: client,
            identity: null,
            user: null,
            timestamp: this._timeProvider.GetLocalNow(),
            method: request.HttpMethod,
            target: request.RawUrl,
            protocol: request.ProtocolVersion,
            status: (int)status,
            bytes: written));
    }

    // Read while the request is still connected, because the endpoint reaches through to the connection that
    // Stop disposes, and the getter throws from under the null check once it has.
    private static string? ReadClient(HttpListenerRequest request)
    {
        try
        {
            return request.RemoteEndPoint?.Address.ToString();
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    // Bypasses the console, which would wrap the line and read a bracket in a request path as markup.
    private void Write(string line) =>
        this._stdOut.Profile.Out.Writer.WriteLine(line);

    private static bool IsSupportedMethod(string method) =>
        method.Equals(WebRequestMethods.Http.Get, StringComparison.Ordinal)
        || method.Equals(WebRequestMethods.Http.Head, StringComparison.Ordinal);

    #endregion
}
