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
    private const int EntryTimeoutMilliseconds = 10_000;
    private const int PollMilliseconds = 5;

    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _serving;

    private readonly StringWriter _output = new();
    private readonly TextWriter _writer;

    private ServerHarness(OutputFixture fixture, DateTimeOffset now, int? refreshSeconds)
    {
        this.Port = FreePort();
        this.Logger = new FakeLogger<FileServer>();
        this.TimeProvider = new FakeTimeProvider(now);

        // Synchronized because the serving task appends while a test reads, which a bare StringWriter does not order.
        this._writer = TextWriter.Synchronized(this._output);

        // A StringWriter rather than a TestConsole, which would need a package the suite does not otherwise want.
        IAnsiConsole console = CapturedConsole.Create(this._writer);

        FileServer server = new(fixture.Resolver, fixture.FileSystem, console, this.TimeProvider, this.Logger);

        // ServeAsync binds the listener before reaching its first await, so the task it returns is already
        // listening and the request below needs no readiness poll.
        this._serving = server.ServeAsync(
            fixture.Root,
            this.Port,
            launchBrowser: false,
            refreshSeconds,
            this._cancellation.Token);
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
    public string Output
    {
        get
        {
            // A synchronized writer locks on itself, so the same lock here orders a read against a write.
            lock (this._writer)
            {
                return this._output.ToString();
            }
        }
    }

    /// <summary>
    ///     What the server has written, one element per line, the banner first.
    /// </summary>
    public string[] Lines => this.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

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
    /// <param name="refreshSeconds">
    ///     The interval an HTML response asks the browser to reload itself after, or <see langword="null"/>
    ///     when no response carries the header.
    /// </param>
    /// <returns>
    ///     The new harness.
    /// </returns>
    public static ServerHarness Start(
        OutputFixture fixture,
        DateTimeOffset? now = null,
        int? refreshSeconds = null) =>
        new(fixture, now ?? new DateTimeOffset(2026, 10, 17, 13, 6, 6, TimeSpan.Zero), refreshSeconds);

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

    /// <summary>
    ///     Waits until the log holds a given number of entries.
    /// </summary>
    /// <remarks>
    ///     An entry is written after the response it describes has gone out, so a request that has returned
    ///     is no guarantee that its line can be read or that its stamp has been taken yet.
    /// </remarks>
    /// <param name="count">
    ///     The number of entries to wait for, the banner excluded.
    /// </param>
    /// <returns>
    ///     A task that completes once that many entries can be read.
    /// </returns>
    /// <exception cref="TimeoutException">
    ///     The entries did not arrive in the time a loopback request needs many times over.
    /// </exception>
    public async Task WaitForEntriesAsync(int count)
    {
        long deadline = Environment.TickCount64 + EntryTimeoutMilliseconds;

        for (string[] lines = this.Lines; lines.Length <= count; lines = this.Lines)
        {
            if (Environment.TickCount64 > deadline)
                throw new TimeoutException($"Waited for {count} log entries, read {lines.Length - 1}.");

            await Task.Delay(PollMilliseconds).ConfigureAwait(false);
        }
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
