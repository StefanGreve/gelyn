using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Core.Internals;
using Gelyn.Services;
using Gelyn.Tests.Fixtures;

using Microsoft.Extensions.Logging.Testing;

namespace Gelyn.Tests.Services;

/// <summary>
///     Unit tests for the <see cref="FileServer"/> class.
/// </summary>
/// <remarks>
///     Driven against a real <see cref="System.Net.HttpListener"/> over an in-memory output tree: what is
///     under test is the transport, which no mock of the file system reaches.
/// </remarks>
public class FileServerTests
{
    #region Address Tests

    /// <summary>
    ///     Verifies that every bound address is a loopback spelling carrying the trailing slash the listener
    ///     requires, and that the address printed to the reader is one of them rather than a fourth spelling.
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
        await using ServerHarness harness = ServerHarness.Start(fixture);

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
    /// <param name="host">
    ///     The loopback spelling to address.
    /// </param>
    [Test]
    [Arguments("localhost")]
    [Arguments("127.0.0.1")]
    public async Task ServeAsync_Test_EitherLoopbackSpelling_IsServed(string host)
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create().Write("index.html", "<p>Home</p>");
        await using ServerHarness harness = ServerHarness.Start(fixture);

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
    ///     Verifies that a binary file survives the response byte for byte, which is what streaming the file
    ///     rather than reading it as text buys.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_BinaryFile_IsServedByteForByte()
    {
        // Arrange
        byte[] expected = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0xFF];

        OutputFixture fixture = OutputFixture.Create("index.html");
        fixture.FileSystem.AddFile(fixture.Locate("logo.png"), new MockFileData(expected));

        await using ServerHarness harness = ServerHarness.Start(fixture);

        // Act
        using HttpResponseMessage response = await harness.Client.GetAsync(harness.Url("/logo.png"), CancellationToken.None);
        byte[] body = await response.Content.ReadAsByteArrayAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(response.Content.Headers.ContentType?.ToString()).IsEqualTo("image/png");
            await Assert.That(body).IsEquivalentTo(expected);
            await Assert.That(response.Content.Headers.ContentLength).IsEqualTo(expected.Length);
        }
    }

    /// <summary>
    ///     Verifies that a miss is answered with the declared page under a 404, so that the status the
    ///     resolver chose reaches the client rather than being overwritten by the body it carries.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_Miss_IsAnsweredWithTheNotFoundPage()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html").Write("404.html", "<p>Gone</p>");
        await using ServerHarness harness = ServerHarness.Start(fixture);

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
        await using ServerHarness harness = ServerHarness.Start(fixture);

        using HttpRequestMessage request = new(HttpMethod.Head, harness.Url("/"));

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
    /// <param name="method">
    ///     A method a preview server has no business answering.
    /// </param>
    [Test]
    [Arguments("POST")]
    [Arguments("PUT")]
    [Arguments("DELETE")]
    public async Task ServeAsync_Test_UnsupportedMethod_IsRefusedUnderTheAllowedOnes(string method)
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html");
        await using ServerHarness harness = ServerHarness.Start(fixture);

        using HttpRequestMessage request = new(new HttpMethod(method), harness.Url("/"));

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
        await using ServerHarness harness = ServerHarness.Start(fixture);

        using HttpRequestMessage refused = new(HttpMethod.Post, harness.Url("/"));

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

    #region Shutdown Tests

    /// <summary>
    ///     Verifies that cancelling stops the server rather than leaving it blocked on the next request,
    ///     which is the whole of what Ctrl+C has to do and the one part of it no terminal test can cover.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_Cancellation_StopsTheServer()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html");
        await using ServerHarness harness = ServerHarness.Start(fixture);

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
        OutputFixture fixture = OutputFixture.Create("index.html");

        // Act
        await using ServerHarness harness = ServerHarness.Start(fixture);

        // Assert
        await Assert.That(harness.Output.TrimEnd())
            .IsEqualTo($"Serving HTTP on 127.0.0.1 port {harness.Port} (http://127.0.0.1:{harness.Port}/) ...");
    }

    /// <summary>
    ///     Verifies that each answered request is logged with the client, the stamped timestamp, the request
    ///     line quoted verbatim, the status and the number of body bytes, and that a response carrying no
    ///     body reports zero rather than a hyphen.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RequestLog_ReportsTheRequestLineStatusAndSize()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html", "about.html");
        await using ServerHarness harness = ServerHarness.Start(fixture);

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
    ///     Verifies that each entry is stamped with the time the request was answered rather than the time the
    ///     server started, and that the query string survives into the logged request line.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RequestLog_StampsEachEntryAndKeepsTheQueryString()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html");
        await using ServerHarness harness = ServerHarness.Start(fixture);

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
    ///     Verifies that a <c>HEAD</c> is logged with a size of zero rather than the length it declared,
    ///     because the trailing field counts the bytes the body carried and a <c>HEAD</c> carries none.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RequestLog_CountsTheBodyAHeadDoesNotSend()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create().Write("index.html", "<p>Home</p>");
        await using ServerHarness harness = ServerHarness.Start(fixture);

        using HttpRequestMessage request = new(HttpMethod.Head, harness.Url("/"));

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
    ///     request the server answered rather than for the successful ones alone.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_RequestLog_CoversARefusedMethod()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html");
        await using ServerHarness harness = ServerHarness.Start(fixture);

        using HttpRequestMessage request = new(HttpMethod.Post, harness.Url("/")) { Content = new StringContent(string.Empty) };

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
    ///     does not state, so that the two are complementary rather than one repeating the other.
    /// </summary>
    [Test]
    public async Task ServeAsync_Test_Diagnostics_ReportTheServedDirectory()
    {
        // Arrange
        OutputFixture fixture = OutputFixture.Create("index.html");
        await using ServerHarness harness = ServerHarness.Start(fixture);

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

    #endregion // Diagnostic Tests
}
