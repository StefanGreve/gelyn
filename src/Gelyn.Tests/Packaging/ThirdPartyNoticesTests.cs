using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gelyn.Tests.Packaging;

/// <summary>
///     Unit tests that keep <c>THIRD-PARTY-NOTICES.TXT</c> in step with the packages the tool ships.
/// </summary>
/// <remarks>
///     Every package in the lock file is classified by hand, either as compiled into the binary under a named
///     notice or as used at build time alone. The lock file lists a <c>PrivateAssets</c> package such as MinVer
///     as a direct dependency like any other, so it cannot tell the two apart, and an unclassified package is
///     a decision still to be made.
/// </remarks>
public class ThirdPartyNoticesTests
{
    private const string NoticesFileName = "THIRD-PARTY-NOTICES.TXT";
    private const string LockFileName = "Gelyn.packages.lock.json";
    private const string SectionPrefix = "License notice for ";

    // Matched against package ids with * as the only wildcard, ignoring case like NuGet does.
    private static readonly (string Pattern, string Notice)[] Shipped =
    [
        ("Markdig", "Markdig"),
        ("Microsoft.Extensions.*", ".NET Runtime and the Microsoft.Extensions libraries"),
        ("System.Diagnostics.EventLog", ".NET Runtime and the Microsoft.Extensions libraries"),
        ("SharpYaml", "SharpYaml"),
        ("Spectre.Console", "Spectre.Console"),
        ("Spectre.Console.Ansi", "Spectre.Console"),
        ("System.CommandLine", "System.CommandLine"),
        ("TestableIO.System.IO.Abstractions.Wrappers", "System.IO.Abstractions"),
        ("Testably.Abstractions.FileSystem.Interface", "Testably.Abstractions"),
    ];

    // The compiler packages carry the runtime, whose notices are packed from them as a file of their own.
    private static readonly string[] BuildTime =
    [
        "MinVer",
        "Microsoft.DotNet.ILCompiler",
        "runtime.*.Microsoft.DotNet.ILCompiler",
        "Microsoft.NET.ILLink.Tasks",
    ];

    // The themes embed icon path data, so these notices are claimed by no package.
    private static readonly string[] Embedded =
    [
        "Feather",
        "Octicons",
    ];

    #region Notice Tests

    /// <summary>
    ///     Verifies that every package the tool depends on is either used at build time alone or covered by a
    ///     notice the file carries.
    /// </summary>
    [Test]
    public async Task Notices_Test_EveryShippedPackage_HasANotice()
    {
        // Arrange
        HashSet<string> notices = ReadNotices();

        // Act
        IEnumerable<string> uncovered = ReadPackages()
            .Where(package => !BuildTime.Any(pattern => Matches(pattern, package)))
            .Where(package => Shipped.FirstOrDefault(entry => Matches(entry.Pattern, package)).Notice is not string notice
                || !notices.Contains(notice));

        // Assert
        await Assert.That(uncovered).IsEmpty();
    }

    /// <summary>
    ///     Verifies that every notice the file carries is claimed by a shipped package or by an embedded asset,
    ///     so that a notice outliving its dependency is removed with it.
    /// </summary>
    [Test]
    public async Task Notices_Test_EveryNotice_IsClaimed()
    {
        // Arrange
        string[] packages = ReadPackages();

        HashSet<string> claimed =
        [
            .. Shipped
                .Where(entry => packages.Any(package => Matches(entry.Pattern, package)))
                .Select(entry => entry.Notice),
            .. Embedded,
        ];

        // Act
        IEnumerable<string> unclaimed = ReadNotices().Where(notice => !claimed.Contains(notice));

        // Assert
        await Assert.That(unclaimed).IsEmpty();
    }

    /// <summary>
    ///     Verifies that every classification matches a package in the lock file, so that a dependency removed
    ///     from the project takes its entry here with it.
    /// </summary>
    [Test]
    public async Task Notices_Test_EveryClassification_MatchesAPackage()
    {
        // Arrange
        string[] packages = ReadPackages();

        // Act
        IEnumerable<string> stale = Shipped
            .Select(entry => entry.Pattern)
            .Concat(BuildTime)
            .Where(pattern => !packages.Any(package => Matches(pattern, package)));

        // Assert
        await Assert.That(stale).IsEmpty();
    }

    #endregion // Notice Tests

    #region Helpers

    private static bool Matches(string pattern, string package) =>
        FileSystemName.MatchesSimpleExpression(pattern, package, ignoreCase: true);

    // Every target is read, because the compiler package of each runtime appears under that runtime alone.
    private static string[] ReadPackages()
    {
        string path = Path.Combine(AppContext.BaseDirectory, LockFileName);

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return
        [
            .. document.RootElement
                .GetProperty("dependencies")
                .EnumerateObject()
                .SelectMany(target => target.Value.EnumerateObject())
                .Select(package => package.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
    }

    private static HashSet<string> ReadNotices()
    {
        string path = Path.Combine(AppContext.BaseDirectory, NoticesFileName);

        return
        [
            .. File.ReadLines(path)
                .Where(line => line.StartsWith(SectionPrefix, StringComparison.Ordinal))
                .Select(line => line[SectionPrefix.Length..]),
        ];
    }

    #endregion
}
