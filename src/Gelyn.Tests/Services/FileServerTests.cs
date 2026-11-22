using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Diagnostics;
using Gelyn.Services;
using Gelyn.Tests.Fixtures;

using Microsoft.Extensions.Logging.Testing;

using Testably.Abstractions.Testing.FileSystem;

using TUnit.Assertions.Enums;

namespace Gelyn.Tests.Services;

/// <summary>
///     Unit tests for the <see cref="FileServer"/> class.
/// </summary>
/// <remarks>
///     Driven against a real <see cref="System.Net.HttpListener"/> over an in-memory output tree: what is
///     under test is the transport, which no mock of the file system reaches.
/// </remarks>
[Category(Categories.Integration)]
public class FileServerTests
{
    #region Address Tests

    /// <summary>
    ///     Verifies that every bound address is a loopback spelling carrying the trailing slash the listener
    ///     requires, and that the address printed to the reader is one of them and not a fourth spelling.
    /// </summary>
    [Test]
    public async Task Addresses_Test_CoverEveryLoopbackSpellingTheListenerAccepts()
    {
        // Act
        IReadOnlyList<string> addresses = FileServer.Addresses(9_624);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(addresses).IsEquivalentTo(["http://localhost:9624/", "http://127.0.0.1:9624/"]);
            await Assert.That(FileServer.Address(9_624)).IsEqualTo(addresses[0]);
        }
    }

    #endregion // Address Tests

    #region Request Tests

    /// <summary>
    ///     Verifies that a page is served with its contents, its media type and a success status.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_Page_IsServedWithItsContentsAndMediaType()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html").Write("index.html", "<p>Home</p>");
        await using var harness = ServerHarness.Start(fixture);

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url("/"), CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(response.Content.Headers.ContentType?.ToString()).IsEqualTo("text/html; charset=utf-8");
            await Assert.That(body).IsEqualTo("<p>Home</p>");
        }
    }

    /// <summary>
    ///     Verifies that the site is served under either loopback spelling, because the listener routes on the
    ///     <c>Host</c> header and a reader who types the one the server does not bind gets a 404 from the
    ///     listener itself, without the request ever reaching the resolver.
    /// </summary>
    [Test]
    [Arguments("localhost")]
    [Arguments("127.0.0.1")]
    public async Task ServeAsync_Test_EitherLoopbackSpelling_IsServed(string host)
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create().Write("index.html", "<p>Home</p>");
        await using var harness = ServerHarness.Start(fixture);

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url("/", host), CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(body).IsEqualTo("<p>Home</p>");
        }
    }

    /// <summary>
    ///     Verifies that a binary file survives the response byte for byte, which streaming the file
    ///     guarantees and reading it as text would not.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_BinaryFile_IsServedByteForByte()
    {
        // Arrange
        byte[] expected = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0xFF];

        var fixture = OutputFixture.Create("index.html");
        fixture.FileSystem.AddFile(fixture.Locate("logo.png"), expected);

        await using var harness = ServerHarness.Start(fixture);

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url("/logo.png"), CancellationToken.None);
        byte[] body = await response.Content.ReadAsByteArrayAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.Content.Headers.ContentType?.ToString()).IsEqualTo("image/png");
            await Assert.That(body).IsEquivalentTo(expected, CollectionOrdering.Matching);
            await Assert.That(response.Content.Headers.ContentLength).IsEqualTo(expected.Length);
        }
    }

    /// <summary>
    ///     Verifies that a miss is answered with the declared page under a 404, so that the status the
    ///     resolver chose reaches the client and is not overwritten by the body it carries.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_Miss_IsAnsweredWithTheNotFoundPage()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html").Write("404.html", "<p>Gone</p>");
        await using var harness = ServerHarness.Start(fixture);

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url("/nope"), CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(body).IsEqualTo("<p>Gone</p>");
        }
    }

    /// <summary>
    ///     Verifies that a HEAD response declares the length the matching GET would send and sends none of
    ///     it, which is what makes the method worth answering at all.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_Head_DeclaresTheLengthAndSendsNoBody()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create().Write("index.html", "<p>Home</p>");
        await using var harness = ServerHarness.Start(fixture);

        using var request = new HttpRequestMessage(HttpMethod.Head, harness.Url("/"));

        // Act
        using HttpResponseMessage response = await harness.Client.SendAsync(request, CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(response.Content.Headers.ContentLength).IsEqualTo("<p>Home</p>".Length);
            await Assert.That(body).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that a method the server does not answer is refused under a 405 naming the two it does,
    ///     which is what RFC 9110 requires of such a response.
    /// </summary>
    [Test]
    [Arguments("POST")]
    [Arguments("PUT")]
    [Arguments("DELETE")]
    public async Task ServeAsync_Test_UnsupportedMethod_IsRefusedUnderTheAllowedOnes(string method)
    {
        // Arrange
        var fixture = OutputFixture.Create("index.html");
        await using var harness = ServerHarness.Start(fixture);

        using var request = new HttpRequestMessage(new HttpMethod(method), harness.Url("/"));

        // Act
        using HttpResponseMessage response = await harness.Client.SendAsync(request, CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.MethodNotAllowed);
            await Assert.That(string.Join(", ", response.Content.Headers.Allow)).IsEqualTo("GET, HEAD");
        }
    }

    /// <summary>
    ///     Verifies that the server keeps answering after a request it refused, so that one bad request does
    ///     not take the session with it.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RefusedRequest_LeavesTheServerAnswering()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create().Write("index.html", "<p>Home</p>");
        await using var harness = ServerHarness.Start(fixture);

        using var refused = new HttpRequestMessage(HttpMethod.Post, harness.Url("/"));

        // Act
        using HttpResponseMessage first = await harness.Client.SendAsync(refused, CancellationToken.None);
        using HttpResponseMessage second = await harness.Client.GetAsync(harness.Url("/"), CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.MethodNotAllowed);
            await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(harness.Stopped).IsFalse();
        }
    }

    #endregion // Request Tests

    #region Base Path Tests

    /// <summary>
    ///     Verifies that a site with a base path is served under it, with or without the trailing slash after
    ///     the path, the way its own links address it.
    /// </summary>
    [Test]
    [Arguments("/docs/", "<p>Home</p>")]
    [Arguments("/docs", "<p>Home</p>")]
    [Arguments("/docs/guide/", "<p>Guide</p>")]
    [Arguments("/docs/about", "<p>About</p>")]
    public async Task ServeAsync_Test_BasePath_ServesTheSiteUnderIt(string requestPath, string expected)
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create()
            .Write("index.html", "<p>Home</p>")
            .Write("guide/index.html", "<p>Guide</p>")
            .Write("about.html", "<p>About</p>");

        await using var harness = ServerHarness.Start(fixture, basePath: "/docs");

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url(requestPath), CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(body).IsEqualTo(expected);
        }
    }

    /// <summary>
    ///     Verifies that the root redirects to the base path under a temporary status, so that no browser
    ///     keeps the redirect for the next site served on the port.
    /// </summary>
    [Test]
    [Arguments("GET")]
    [Arguments("HEAD")]
    public async Task ServeAsync_Test_BasePath_RootRedirectsToIt(string method)
    {
        // Arrange
        var fixture = OutputFixture.Create("index.html");
        await using var harness = ServerHarness.Start(fixture, basePath: "/docs");

        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(new HttpMethod(method), harness.Url("/"));

        // Act
        using HttpResponseMessage response = await client.SendAsync(request, CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Found);
            await Assert.That(response.Headers.Location?.OriginalString).IsEqualTo("/docs/");
        }
    }

    /// <summary>
    ///     Verifies that a path outside the base path is answered with the not-found page, even where a file
    ///     in the output directory matches it, and that a sibling sharing the base path's prefix is outside.
    /// </summary>
    [Test]
    [Arguments("/index.html")]
    [Arguments("/docs-old/")]
    [Arguments("/other/page")]
    public async Task ServeAsync_Test_BasePath_LeavesEveryOtherPathToTheNotFoundPage(string requestPath)
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create()
            .Write("index.html", "<p>Home</p>")
            .Write("404.html", "<p>Gone</p>");

        await using var harness = ServerHarness.Start(fixture, basePath: "/docs");

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url(requestPath), CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(body).IsEqualTo("<p>Gone</p>");
        }
    }

    /// <summary>
    ///     Verifies that the banner points the reader at the base path, which is where the site starts.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_BasePath_IsNamedByTheBanner()
    {
        // Arrange
        var fixture = OutputFixture.Create("index.html");

        // Act
        await using var harness = ServerHarness.Start(fixture, basePath: "/docs");

        // Assert
        await Assert.That(harness.Output.TrimEnd())
            .IsEqualTo($"Serving HTTP on 127.0.0.1 port {harness.Port} (http://127.0.0.1:{harness.Port}/docs/) ...");
    }

    #endregion // Base Path Tests

    #region Refresh Header Tests

    /// <summary>
    ///     Verifies that a page declares the interval the session was started with, which is what reloads the
    ///     browser without the reader touching it.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RefreshInterval_IsDeclaredOnAPage()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create().Write("index.html", "<p>Home</p>");
        await using var harness = ServerHarness.Start(fixture, refreshSeconds: 2);

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url("/"), CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(ReadRefresh(response)).IsEqualTo("2");
        }
    }

    /// <summary>
    ///     Verifies that a subresource carries no interval, so that one page reload is one pass, and not one
    ///     per file the page pulls in.
    /// </summary>
    [Test]
    [Arguments("styles.css")]
    [Arguments("app.js")]
    [Arguments("logo.png")]
    [Arguments("robots.txt")]
    public async Task ServeAsync_Test_RefreshInterval_IsAbsentFromASubresource(string relativePath)
    {
        // Arrange
        var fixture = OutputFixture.Create(relativePath);
        await using var harness = ServerHarness.Start(fixture, refreshSeconds: 2);

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(
            harness.Url($"/{relativePath}"),
            CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(ReadRefresh(response)).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that a session started without the option declares no interval at all, so that the served
    ///     bytes and headers are what a deployment would carry.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_NoRefreshInterval_LeavesTheHeaderOff()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create().Write("index.html", "<p>Home</p>");
        await using var harness = ServerHarness.Start(fixture);

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url("/"), CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(ReadRefresh(response)).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that a declared 404 page carries the interval as any other page does, a reader who landed
    ///     on one waiting for the file to appear.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RefreshInterval_ReachesTheNotFoundPage()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html").Write("404.html", "<p>Gone</p>");
        await using var harness = ServerHarness.Start(fixture, refreshSeconds: 2);

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url("/nope"), CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(ReadRefresh(response)).IsEqualTo("2");
        }
    }

    /// <summary>
    ///     Verifies that a 404 answered without a page carries no interval, the fallback being a bodyless
    ///     plain-text refusal and not something a browser would reload into.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RefreshInterval_IsAbsentFromABodylessNotFound()
    {
        // Arrange
        var fixture = OutputFixture.Create("index.html");
        await using var harness = ServerHarness.Start(fixture, refreshSeconds: 2);

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url("/nope"), CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(ReadRefresh(response)).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that a HEAD response declares the interval the matching GET would, the two differing in
    ///     the body alone.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RefreshInterval_IsDeclaredOnAHeadResponse()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create().Write("index.html", "<p>Home</p>");
        await using var harness = ServerHarness.Start(fixture, refreshSeconds: 2);

        using var request = new HttpRequestMessage(HttpMethod.Head, harness.Url("/"));

        // Act
        using HttpResponseMessage response = await harness.Client.SendAsync(request, CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(ReadRefresh(response)).IsEqualTo("2");
        }
    }

    /// <summary>
    ///     Reads the <c>Refresh</c> header off one response.
    /// </summary>
    /// <remarks>
    ///     Read by name, because no typed accessor exists for it: the field is absent from
    ///     <see cref="System.Net.Http.Headers.HttpResponseHeaders"/> for the same reason it is absent from
    ///     <see cref="HttpResponseHeader"/>, which is that HTTP never defined it.
    /// </remarks>
    /// <param name="response">
    ///     The response to read.
    /// </param>
    /// <returns>
    ///     The interval the response declares, or <see langword="null"/> when it declares none.
    /// </returns>
    private static string? ReadRefresh(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Refresh", out IEnumerable<string>? values)
            ? string.Join(", ", values)
            : null;

    #endregion // Refresh Header Tests

    #region Shutdown Tests

    /// <summary>
    ///     Verifies that canceling stops the server and does not leave it blocked on the next request,
    ///     which is the whole of what Ctrl+C has to do and the one part of it no terminal test can cover.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_Cancellation_StopsTheServer()
    {
        // Arrange
        var fixture = OutputFixture.Create("index.html");
        await using var harness = ServerHarness.Start(fixture);

        using HttpResponseMessage served = await harness.Client.GetAsync(harness.Url("/"), CancellationToken.None);

        // Act
        await harness.StopAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(served.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(harness.Stopped).IsTrue();
        }
    }

    #endregion // Shutdown Tests

    #region Request Log Tests

    /// <summary>
    ///     Verifies that the banner names the loopback address, the port and the URL, in the wording
    ///     <c>python3 -m http.server</c> uses, because that is the command this one replaced.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_Banner_NamesTheAddressThePortAndTheUrl()
    {
        // Arrange
        var fixture = OutputFixture.Create("index.html");

        // Act
        await using var harness = ServerHarness.Start(fixture);

        // Assert
        await Assert.That(harness.Output.TrimEnd())
            .IsEqualTo($"Serving HTTP on 127.0.0.1 port {harness.Port} (http://127.0.0.1:{harness.Port}/) ...");
    }

    /// <summary>
    ///     Verifies that each answered request is logged with the client, the stamped timestamp, the request
    ///     line quoted verbatim, the status and the number of body bytes, and that a response carrying no
    ///     body reports zero and never a hyphen.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RequestLog_ReportsTheRequestLineStatusAndSize()
    {
        // Arrange
        var fixture = OutputFixture.Create("index.html", "about.html");
        await using var harness = ServerHarness.Start(fixture);

        long size = fixture.FileSystem.FileInfo.New(fixture.Locate("index.html")).Length;

        // Act
        using HttpResponseMessage page = await harness.Client.GetAsync(harness.Url("/index.html"), CancellationToken.None);
        using HttpResponseMessage miss = await harness.Client.GetAsync(harness.Url("/nope"), CancellationToken.None);
        await harness.WaitForEntriesAsync(2);
        await harness.StopAsync();

        // Assert: the banner is line one, so the entries start at line two.
        string[] lines = harness.Lines;

        using (Assert.Multiple())
        {
            // Pinned first, so that a size the server failed to measure could not satisfy the entry below.
            await Assert.That(size).IsGreaterThan(0);

            await Assert.That(lines[1])
                .IsEqualTo($"""127.0.0.1 - - [17/Oct/2026:13:06:06 +0000] "GET /index.html HTTP/1.1" 200 {size}""");

            await Assert.That(lines[2])
                .IsEqualTo("""127.0.0.1 - - [17/Oct/2026:13:06:06 +0000] "GET /nope HTTP/1.1" 404 0""");

            await Assert.That(page.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(miss.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    /// <summary>
    ///     Verifies that each entry is stamped with the time the request was answered and not the time the
    ///     server started, and that the query string survives into the logged request line.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RequestLog_StampsEachEntryAndKeepsTheQueryString()
    {
        // Arrange
        var fixture = OutputFixture.Create("index.html");
        await using var harness = ServerHarness.Start(fixture);

        long size = fixture.FileSystem.FileInfo.New(fixture.Locate("index.html")).Length;

        // Act
        using HttpResponseMessage first = await harness.Client.GetAsync(harness.Url("/"), CancellationToken.None);

        // Advanced once the first entry is stamped, which the response it describes does not wait for.
        await harness.WaitForEntriesAsync(1);
        harness.TimeProvider.Advance(TimeSpan.FromSeconds(4));

        using HttpResponseMessage second = await harness.Client.GetAsync(harness.Url("/?v=2"), CancellationToken.None);
        await harness.WaitForEntriesAsync(2);
        await harness.StopAsync();

        // Assert
        string[] lines = harness.Lines;

        using (Assert.Multiple())
        {
            await Assert.That(lines[1]).IsEqualTo($"""127.0.0.1 - - [17/Oct/2026:13:06:06 +0000] "GET / HTTP/1.1" 200 {size}""");
            await Assert.That(lines[2]).IsEqualTo($"""127.0.0.1 - - [17/Oct/2026:13:06:10 +0000] "GET /?v=2 HTTP/1.1" 200 {size}""");
            await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    /// <summary>
    ///     Verifies that a <c>HEAD</c> is logged with a size of zero and never the length it declared,
    ///     because the trailing field counts the bytes the body carried and a <c>HEAD</c> carries none.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RequestLog_CountsTheBodyAHeadDoesNotSend()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create().Write("index.html", "<p>Home</p>");
        await using var harness = ServerHarness.Start(fixture);

        using var request = new HttpRequestMessage(HttpMethod.Head, harness.Url("/"));

        // Act
        using HttpResponseMessage response = await harness.Client.SendAsync(request, CancellationToken.None);
        await harness.WaitForEntriesAsync(1);
        await harness.StopAsync();

        // Assert
        string[] lines = harness.Lines;

        using (Assert.Multiple())
        {
            await Assert.That(lines[1]).IsEqualTo("""127.0.0.1 - - [17/Oct/2026:13:06:06 +0000] "HEAD / HTTP/1.1" 200 0""");

            // The declared length still reaches the client, which is what the logged zero is not denying.
            await Assert.That(response.Content.Headers.ContentLength).IsEqualTo("<p>Home</p>".Length);
        }
    }

    /// <summary>
    ///     Verifies that a refused method is logged like any other request, so that the log accounts for every
    ///     request the server answered, and not for the successful ones alone.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RequestLog_CoversARefusedMethod()
    {
        // Arrange
        var fixture = OutputFixture.Create("index.html");
        await using var harness = ServerHarness.Start(fixture);

        using var request = new HttpRequestMessage(HttpMethod.Post, harness.Url("/")) { Content = new StringContent(string.Empty) };

        // Act
        using HttpResponseMessage response = await harness.Client.SendAsync(request, CancellationToken.None);
        await harness.WaitForEntriesAsync(1);
        await harness.StopAsync();

        // Assert
        string[] lines = harness.Lines;

        using (Assert.Multiple())
        {
            await Assert.That(lines[1]).IsEqualTo("""127.0.0.1 - - [17/Oct/2026:13:06:06 +0000] "POST / HTTP/1.1" 405 0""");
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.MethodNotAllowed);
        }
    }

    #endregion // Request Log Tests

    #region Diagnostic Tests

    /// <summary>
    ///     Verifies that the server reports the directory it answers from at debug level, which the banner
    ///     does not state, so that the two are complementary and neither repeats the other.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_Diagnostics_ReportTheServedDirectory()
    {
        // Arrange
        var fixture = OutputFixture.Create("index.html");
        await using var harness = ServerHarness.Start(fixture);

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url("/"), CancellationToken.None);
        await harness.StopAsync();

        // Assert
        FakeLogRecord[] records = [.. harness.Logger.Collector.GetSnapshot()];

        using (Assert.Multiple())
        {
            await Assert.That(records.Select(static record => record.Id.Id)).IsEquivalentTo([EventIds.SERVER_STARTED]);
            await Assert.That(records[0].GetStructuredStateValue("Directory")).IsEqualTo(fixture.Root);
            await Assert.That(records[0].GetStructuredStateValue("Address")).IsEqualTo(FileServer.Address(harness.Port));
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    /// <summary>
    ///     Verifies that an answer failing for a reason the server does not name is reported, answered with a
    ///     500 on every platform, and survived, so that the next request is answered as if nothing had happened.
    /// </summary>
    /// <remarks>
    ///     A 500 and not an abort, because the managed listener sends the headers it has on an abort, which
    ///     would reach the browser as a 200 with no body, while HTTP.sys resets the connection.
    /// </remarks>
    [Test]
    public async Task ServeAsync_Test_FailedAnswer_IsReportedAndTheNextOneIsAnswered()
    {
        // Arrange
        var fixture = OutputFixture.Create("index.html");
        bool denied = false;

        // The first open of the page is denied, which the server does not name, and every later one granted.
        fixture.FileSystem.WithAccessControlStrategy(new DefaultAccessControlStrategy((_, _, _) =>
        {
            if (denied) return true;
            denied = true;
            return false;
        }));
        await using var harness = ServerHarness.Start(fixture);

        // Act
        using HttpResponseMessage failed = await harness.Client.GetAsync(harness.Url("/"), CancellationToken.None);
        using HttpResponseMessage answered = await harness.Client.GetAsync(harness.Url("/"), CancellationToken.None);
        await harness.WaitForEntriesAsync(2);
        await harness.StopAsync();

        // Assert
        FakeLogRecord[] records = [.. harness.Logger.Collector.GetSnapshot()];

        using (Assert.Multiple())
        {
            await Assert.That(records.Select(static record => record.Id.Id)).IsEquivalentTo([EventIds.SERVER_STARTED, EventIds.ANSWER_FAILED]);
            await Assert.That(records[1].Exception).IsTypeOf<UnauthorizedAccessException>();
            await Assert.That(records[1].GetStructuredStateValue("Method")).IsEqualTo("GET");
            await Assert.That(records[1].GetStructuredStateValue("Path")).IsEqualTo("/");
            await Assert.That(harness.Lines[1]).IsEqualTo("""127.0.0.1 - - [17/Oct/2026:13:06:06 +0000] "GET / HTTP/1.1" 500 0""");
            await Assert.That(failed.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
            await Assert.That(answered.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    #endregion // Diagnostic Tests
}
