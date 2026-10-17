using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Core;
using Gelyn.Core.Internals;
using Gelyn.Model;

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
    private const string AllowHeader = "Allow";

    /// <summary>
    ///     The methods the server answers, in the form the <c>Allow</c> header of a refusal takes.
    /// </summary>
    private const string ReadMethods = WebRequestMethods.Http.Get + ", " + WebRequestMethods.Http.Head;

    private const string LoopbackAddress = "127.0.0.1";
    private const string LoopbackName = "localhost";

    private readonly StaticFileResolver _resolver;
    private readonly IFileSystem _fileSystem;
    private readonly IAnsiConsole _console;
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
    /// <param name="console">
    ///     The console the request log is written to.
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
        IAnsiConsole console,
        TimeProvider timeProvider,
        ILogger<FileServer> logger)
    {
        this._resolver = resolver;
        this._fileSystem = fileSystem;
        this._console = console;
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
    ///     A request is routed by matching its <c>Host</c> header against the host of a prefix rather than by
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);

        if (!HttpListener.IsSupported)
            throw new PlatformNotSupportedException($"{nameof(HttpListener)} is not available on this platform.");

        string address = Address(port);

        using HttpListener listener = new();

        foreach (string prefix in Addresses(port))
            listener.Prefixes.Add(prefix);

        listener.Start();
        this.Write(CommonLogFormat.BuildHeader(LoopbackAddress, port, address));
        this._logger.LogServerStarted(outputDirectory, address);

        if (launchBrowser && !Browser.TryLaunch(address, out string? errorMessage))
            this._console.MarkupLineInterpolated($"[yellow]warning:[/] no browser was opened: {errorMessage}");

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

            await this.AnswerAsync(context, outputDirectory, cancellationToken).ConfigureAwait(false);
        }
    }

    #region Helpers

    private async Task AnswerAsync(
        HttpListenerContext context,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        string method = request.HttpMethod;
        string path = request.Url?.AbsolutePath ?? "/";

        try
        {
            if (!IsSupportedMethod(method))
            {
                // RFC 9110 requires a 405 to name the methods the target does support.
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                response.AddHeader(AllowHeader, ReadMethods);
                response.ContentLength64 = 0;

                this.Report(request, HttpStatusCode.MethodNotAllowed, written: 0);
            }
            else
            {
                ServedFile file = this._resolver.Resolve(outputDirectory, path);

                response.StatusCode = (int)file.StatusCode;
                response.ContentType = file.ContentType;

                long written = await this
                    .WriteBodyAsync(response, file, method, cancellationToken)
                    .ConfigureAwait(false);

                this.Report(request, file.StatusCode, written);
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
    ///     count is the body alone, as a number rather than the hyphen Apache's <c>%b</c> writes for an
    ///     empty body, which is what nginx reports and what makes a page published empty visible at a glance.
    /// </remarks>
    /// <param name="request">
    ///     The request that was answered.
    /// </param>
    /// <param name="status">
    ///     The status it was answered under.
    /// </param>
    /// <param name="written">
    ///     The number of bytes written to the body.
    /// </param>
    private void Report(HttpListenerRequest request, HttpStatusCode status, long written)
    {
        this.Write(CommonLogFormat.BuildMessage(
            client: request.RemoteEndPoint?.Address.ToString(),
            identity: null,
            user: null,
            timestamp: this._timeProvider.GetLocalNow(),
            method: request.HttpMethod,
            target: request.RawUrl,
            protocol: request.ProtocolVersion,
            status: (int)status,
            bytes: written));
    }

    // Bypasses the console, which would wrap the line and read a bracket in a request path as markup.
    private void Write(string line) =>
        this._console.Profile.Out.Writer.WriteLine(line);

    private static bool IsSupportedMethod(string method) =>
        method.Equals(WebRequestMethods.Http.Get, StringComparison.Ordinal)
        || method.Equals(WebRequestMethods.Http.Head, StringComparison.Ordinal);

    #endregion
}
