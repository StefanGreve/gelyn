using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Services;

using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

using Spectre.Console;

namespace Gelyn.Tests.Fixtures;

/// <summary>
///     A <see cref="FileServer"/> listening on a free loopback port, together with the client that talks to
///     it.
/// </summary>
/// <remarks>
///     The listener is real while the files it answers from are not, so a test covers the transport without
///     reaching a disk. Disposal cancels the server the way Ctrl+C does and waits for it, so a shutdown that
///     never completes fails the test that started it rather than leaking a socket into the next one.
/// </remarks>
internal sealed class ServerHarness : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _serving;

    private readonly StringWriter _output = new();

    private ServerHarness(OutputFixture fixture, DateTimeOffset now)
    {
        this.Port = FreePort();
        this.Logger = new FakeLogger<FileServer>();
        this.TimeProvider = new FakeTimeProvider(now);

        // Written to a StringWriter rather than to a TestConsole, which would need a package the suite does
        // not otherwise want. NoColors keeps the captured text free of escape sequences.
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(this._output),
        });

        FileServer server = new(fixture.Resolver, fixture.FileSystem, console, this.TimeProvider, this.Logger);

        // ServeAsync binds the listener before reaching its first await, so the task it returns is already
        // listening and the request below needs no readiness poll. No test asks for a browser: the suite runs
        // headless on CI, and a launch would open one on the machine running it.
        this._serving = server.ServeAsync(fixture.Root, this.Port, launchBrowser: false, this._cancellation.Token);
    }

    /// <summary>
    ///     The port the server is listening on.
    /// </summary>
    public int Port { get; }

    /// <summary>
    ///     The records the server has written.
    /// </summary>
    public FakeLogger<FileServer> Logger { get; }

    /// <summary>
    ///     The clock each request is stamped against, fixed unless a test advances it.
    /// </summary>
    public FakeTimeProvider TimeProvider { get; }

    /// <summary>
    ///     Everything the server has written to its console, banner included.
    /// </summary>
    public string Output => this._output.ToString();

    /// <summary>
    ///     The client the requests are sent through.
    /// </summary>
    public HttpClient Client { get; } = new();

    /// <summary>
    ///     Indicates whether the server has stopped.
    /// </summary>
    public bool Stopped => this._serving.IsCompleted;

    /// <summary>
    ///     Starts a server over one output tree.
    /// </summary>
    /// <param name="fixture">
    ///     The output tree to serve.
    /// </param>
    /// <param name="now">
    ///     The instant the clock reports, which every request is stamped with.
    /// </param>
    /// <returns>
    ///     The new harness.
    /// </returns>
    public static ServerHarness Start(OutputFixture fixture, DateTimeOffset? now = null) =>
        new(fixture, now ?? new DateTimeOffset(2026, 10, 17, 13, 6, 6, TimeSpan.Zero));

    /// <summary>
    ///     Composes the absolute URL one request path addresses.
    /// </summary>
    /// <remarks>
    ///     The default is the literal rather than the name, because <c>localhost</c> resolves to <c>::1</c> on
    ///     some machines, and the client field of a log entry reports the socket the request arrived on.
    /// </remarks>
    /// <param name="requestPath">
    ///     The path of the request, opening with a slash.
    /// </param>
    /// <param name="host">
    ///     The loopback spelling to address, which the client sends as the <c>Host</c> header and the
    ///     listener routes on.
    /// </param>
    /// <returns>
    ///     The URL.
    /// </returns>
    public Uri Url(string requestPath, string host = "127.0.0.1") =>
        new($"http://{host}:{this.Port}{requestPath}");

    /// <summary>
    ///     Cancels the server, waits for it to stop, and releases the client.
    /// </summary>
    /// <returns>
    ///     A task that completes once the server has stopped.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        await this._cancellation.CancelAsync().ConfigureAwait(false);
        await this._serving.ConfigureAwait(false);

        this._cancellation.Dispose();
        this.Client.Dispose();
        this._output.Dispose();
    }

    /// <summary>
    ///     Stops the server without disposing the harness, so that a test can assert what cancelling does.
    /// </summary>
    /// <returns>
    ///     A task that completes once the server has stopped.
    /// </returns>
    public async Task StopAsync()
    {
        await this._cancellation.CancelAsync().ConfigureAwait(false);
        await this._serving.ConfigureAwait(false);
    }

    #region Helpers

    // HttpListener cannot bind port 0, so the kernel assigns one to a probe socket and the number is handed
    // on. The window between the probe closing and the listener binding is what that costs.
    private static int FreePort()
    {
        using TcpListener probe = new(IPAddress.Loopback, 0);

        probe.Start();

        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    #endregion
}
