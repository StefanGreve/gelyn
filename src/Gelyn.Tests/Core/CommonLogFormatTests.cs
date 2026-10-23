using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Gelyn.Core;
using Gelyn.Core.Internals;

namespace Gelyn.Tests.Core;

/// <summary>
///     Unit tests for the <see cref="CommonLogFormat"/> class.
/// </summary>
/// <remarks>
///     The entries are checked against the grammar rather than against literals written out here, because a
///     literal only proves the composition has not changed while the grammar proves a log analyzer can read
///     it. <see cref="Entry"/> is the <c>COMMONAPACHELOG</c> pattern every such tool is built on, which is
///     what makes a drift away from the format a failure here rather than a surprise downstream.
/// </remarks>
public partial class CommonLogFormatTests
{
    #region Compliance Tests

    /// <summary>
    ///     Verifies that a fully populated entry satisfies the grammar and that every field lands in the
    ///     position the grammar assigns it.
    /// </summary>
    [Test]
    public async Task BuildMessage_Test_PopulatedEntry_SatisfiesTheGrammar()
    {
        // Act
        string entry = Build(identity: "ident", user: "stefan", bytes: 1_385);

        // Assert
        Match match = Entry().Match(entry);

        using (Assert.Multiple())
        {
            await Assert.That(match.Success).IsTrue();
            await Assert.That(match.Groups["client"].Value).IsEqualTo("127.0.0.1");
            await Assert.That(match.Groups["identity"].Value).IsEqualTo("ident");
            await Assert.That(match.Groups["user"].Value).IsEqualTo("stefan");
            await Assert.That(match.Groups["timestamp"].Value).IsEqualTo("17/Oct/2026:13:06:06 +0000");
            await Assert.That(match.Groups["method"].Value).IsEqualTo("GET");
            await Assert.That(match.Groups["target"].Value).IsEqualTo("/index.html");
            await Assert.That(match.Groups["protocol"].Value).IsEqualTo("1.1");
            await Assert.That(match.Groups["status"].Value).IsEqualTo("200");
            await Assert.That(match.Groups["bytes"].Value).IsEqualTo("1385");
        }
    }

    /// <summary>
    ///     Verifies that an entry whose every optional field is absent still satisfies the grammar, because a
    ///     hyphen is a value the format accepts rather than a column the caller may leave out.
    /// </summary>
    [Test]
    public async Task BuildMessage_Test_AbsentFields_AreWrittenAsHyphens()
    {
        // Act
        string entry = Build(client: null, identity: null, user: null, target: null, bytes: null);

        // Assert
        Match match = Entry().Match(entry);

        using (Assert.Multiple())
        {
            await Assert.That(match.Success).IsTrue();
            await Assert.That(match.Groups["client"].Value).IsEqualTo("-");
            await Assert.That(match.Groups["identity"].Value).IsEqualTo("-");
            await Assert.That(match.Groups["user"].Value).IsEqualTo("-");
            await Assert.That(match.Groups["target"].Value).IsEqualTo("-");
            await Assert.That(match.Groups["bytes"].Value).IsEqualTo("-");
        }
    }

    /// <summary>
    ///     Verifies that both conventions for an empty body satisfy the grammar, so that the choice between
    ///     Apache's hyphen and the zero nginx reports stays the caller's.
    /// </summary>
    [Test]
    [Arguments(null, "-")]
    [Arguments(0L, "0")]
    [Arguments(1L, "1")]
    public async Task BuildMessage_Test_EmptyBody_IsAcceptedEitherWay(long? bytes, string expected)
    {
        // Act
        string entry = Build(bytes: bytes);

        // Assert
        Match match = Entry().Match(entry);

        using (Assert.Multiple())
        {
            await Assert.That(match.Success).IsTrue();
            await Assert.That(match.Groups["bytes"].Value).IsEqualTo(expected);
        }
    }

    /// <summary>
    ///     Verifies that a status reaches the entry as three bare digits, whether or not it is the one a
    ///     successful request is answered under.
    /// </summary>
    [Test]
    [Arguments(200)]
    [Arguments(404)]
    public async Task BuildMessage_Test_Status_SatisfiesTheGrammar(int status)
    {
        // Act
        string entry = Build(status: status);

        // Assert
        Match match = Entry().Match(entry);

        using (Assert.Multiple())
        {
            await Assert.That(match.Success).IsTrue();
            await Assert.That(match.Groups["status"].Value).IsEqualTo(status.ToString(Tool.Culture));
        }
    }

    #endregion // Compliance Tests

    #region Timestamp Tests

    /// <summary>
    ///     Verifies that the timestamp separates the date from the time with a colon and closes with a
    ///     four-digit UTC offset, which is the field <c>HTTPDATE</c> matches and which a space-separated,
    ///     offsetless timestamp does not satisfy.
    /// </summary>
    [Test]
    [Arguments(0, 0, "17/Oct/2026:13:06:06 +0000")]
    [Arguments(2, 0, "17/Oct/2026:13:06:06 +0200")]
    [Arguments(-4, 0, "17/Oct/2026:13:06:06 -0400")]
    [Arguments(5, 30, "17/Oct/2026:13:06:06 +0530")]
    [Arguments(-3, -30, "17/Oct/2026:13:06:06 -0330")]
    [Arguments(14, 0, "17/Oct/2026:13:06:06 +1400")]
    public async Task BuildMessage_Test_Timestamp_CarriesAColonAndAFourDigitOffset(
        int offsetHours,
        int offsetMinutes,
        string expected)
    {
        // Arrange
        TimeSpan offset = new(offsetHours, offsetMinutes, 0);
        DateTimeOffset timestamp = new(2026, 10, 17, 13, 6, 6, offset);

        // Act
        string entry = Build(timestamp: timestamp);

        // Assert
        Match match = Entry().Match(entry);

        using (Assert.Multiple())
        {
            await Assert.That(match.Success).IsTrue();
            await Assert.That(match.Groups["timestamp"].Value).IsEqualTo(expected);
        }
    }

    /// <summary>
    ///     Verifies that the month is abbreviated in English, because the format fixes those three letters
    ///     rather than leaving them to the locale of the machine the server runs on.
    /// </summary>
    [Test]
    [Arguments(1, "Jan")]
    [Arguments(3, "Mar")]
    [Arguments(5, "May")]
    [Arguments(10, "Oct")]
    [Arguments(12, "Dec")]
    public async Task BuildMessage_Test_Month_IsAbbreviatedInEnglish(int month, string expected)
    {
        // Act
        string entry = Build(timestamp: new DateTimeOffset(2026, month, 17, 13, 6, 6, TimeSpan.Zero));

        // Assert
        await Assert.That(Entry().Match(entry).Groups["timestamp"].Value).StartsWith($"17/{expected}/2026:");
    }

    #endregion // Timestamp Tests

    #region Target Tests

    /// <summary>
    ///     Verifies that a target carrying a query string, an encoded separator or a trailing slash reaches
    ///     the entry unchanged, because the log records what was asked for rather than what it resolved to.
    /// </summary>
    [Test]
    [Arguments("/")]
    [Arguments("/index.html")]
    [Arguments("/blog/")]
    [Arguments("/about?v=2")]
    [Arguments("/a%20b.html")]
    [Arguments("/..%2fetc%2fpasswd")]
    public async Task BuildMessage_Test_Target_ReachesTheEntryUnchanged(string target)
    {
        // Act
        string entry = Build(target: target);

        // Assert
        Match match = Entry().Match(entry);

        using (Assert.Multiple())
        {
            await Assert.That(match.Success).IsTrue();
            await Assert.That(match.Groups["target"].Value).IsEqualTo(target);
        }
    }

    #endregion // Target Tests

    #region Protocol Tests

    /// <summary>
    ///     Verifies that the request line carries the major and minor version alone, whatever else the
    ///     version it was given holds.
    /// </summary>
    /// <remarks>
    ///     A <see cref="Version"/> renders every component it carries, so a four-part one would write
    ///     <c>HTTP/1.1.0.0</c> and take the entry outside the grammar a log analyzer reads.
    /// </remarks>
    [Test]
    public async Task BuildMessage_Test_Protocol_CarriesTheMajorAndMinorOnly()
    {
        // Act
        string entry = Build(protocol: new Version(1, 1, 0, 0));

        // Assert
        Match match = Entry().Match(entry);

        using (Assert.Multiple())
        {
            await Assert.That(match.Success).IsTrue();
            await Assert.That(match.Groups["protocol"].Value).IsEqualTo("1.1");
        }
    }

    #endregion // Protocol Tests

    #region BuildHeader Tests

    /// <summary>
    ///     Verifies that the header names the host, the port and the address, in the wording
    ///     <c>python3 -m http.server</c> uses.
    /// </summary>
    [Test]
    public async Task BuildHeader_Test_NamesTheHostThePortAndTheAddress()
    {
        // Act
        string header = CommonLogFormat.BuildHeader("127.0.0.1", 9_624, "http://127.0.0.1:9624/");

        // Assert
        await Assert.That(header).IsEqualTo("Serving HTTP on 127.0.0.1 port 9624 (http://127.0.0.1:9624/) ...");
    }

    /// <summary>
    ///     Verifies that the port is written without a group separator, which a culture that formats
    ///     thousands would otherwise introduce into an address.
    /// </summary>
    [Test]
    public async Task BuildHeader_Test_Port_CarriesNoGroupSeparator()
    {
        // Act
        string header = CommonLogFormat.BuildHeader("127.0.0.1", 60_000, "http://127.0.0.1:60000/");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(header).Contains("port 60000");
            await Assert.That(header).DoesNotContain("60,000");
        }
    }

    /// <summary>
    ///     Verifies that a missing host or address is refused rather than written into the banner, which is
    ///     the line a reader copies to reach the site.
    /// </summary>
    [Test]
    public async Task BuildHeader_Test_MissingArgument_Throws()
    {
        // Act and Assert
        using (Assert.Multiple())
        {
            await Assert.That(() => CommonLogFormat.BuildHeader(null!, 9_624, "http://127.0.0.1:9624/"))
                .Throws<ArgumentNullException>();

            await Assert.That(() => CommonLogFormat.BuildHeader("127.0.0.1", 9_624, null!))
                .Throws<ArgumentNullException>();
        }
    }

    #endregion // BuildHeader Tests

    #region Helpers

    // The COMMONAPACHELOG grammar, anchored: client, identity, user, a bracketed HTTPDATE, the quoted
    // request line, the status and the body size. A log analyzer reads an entry with this shape or not at all.
    [GeneratedRegex("""
        ^(?<client>\S+)[ ](?<identity>\S+)[ ](?<user>\S+)[ ]
        \[(?<timestamp>\d{2}/[A-Z][a-z]{2}/\d{4}:\d{2}:\d{2}:\d{2}[ ][+-]\d{4})\][ ]
        "(?<method>[A-Z]+)[ ](?<target>\S+)[ ]HTTP/(?<protocol>\d\.\d)"[ ]
        (?<status>\d{3})[ ](?<bytes>\d+|-)$
        """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex Entry();

    // Every parameter defaulted, so that each test states only the field it is about.
    private static string Build(
        string? client = "127.0.0.1",
        string? identity = null,
        string? user = null,
        DateTimeOffset? timestamp = null,
        string method = "GET",
        string? target = "/index.html",
        Version? protocol = null,
        int status = 200,
        long? bytes = 1_385) => CommonLogFormat.BuildMessage(
            client,
            identity,
            user,
            timestamp ?? new DateTimeOffset(2026, 10, 17, 13, 6, 6, TimeSpan.Zero),
            method,
            target,
            protocol ?? new Version(1, 1),
            status,
            bytes);

    #endregion // Helpers
}
