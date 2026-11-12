using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Gelyn.Core;
using Gelyn.Core.Internals;
using Gelyn.Tests.Fixtures;
using Gelyn.Tests.Stubs;

using Microsoft.Extensions.Configuration;

using Spectre.Console;

using TUnit.Assertions.Enums;

namespace Gelyn.Tests.Core.Internals;

/// <summary>
///     Unit tests for the <see cref="InfoOption"/> class.
/// </summary>
public class InfoOptionTests
{
    private const string Undefined = "(undefined)";

    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static readonly char Separator = Path.DirectorySeparatorChar;

    #region Report Tests

    /// <summary>
    ///     Verifies that the report holds the three groups in order, with every value starting in one column.
    /// </summary>
    [Test]
    public async Task Report_Test_Groups_AppearInOrderWithAlignedValues()
    {
        // Act
        (int exitCode, string output, _) = Invoke(Create(), InfoOption.OptionName);

        string[] lines = Lines(output);
        string[] headings = [.. lines.Where(static line => line.Length > 0 && !line.StartsWith(' '))];
        int[] columns = [.. lines.Where(static line => line.StartsWith("  ", StringComparison.Ordinal)).Select(ValueColumn).Distinct()];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(ExitCodes.Success);
            await Assert.That(headings).IsEquivalentTo(["[Project]", "[Runtime Environment]", "[Configuration]"], CollectionOrdering.Matching);
            await Assert.That(columns).Count().IsEqualTo(1);
        }
    }

    /// <summary>
    ///     Verifies that the version is the tool's own and the bundled themes are the catalog's.
    /// </summary>
    [Test]
    public async Task Report_Test_VersionAndBundledThemes_MatchTheTool()
    {
        // Act
        string output = Report(Create());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Value(output, "Version")).IsEqualTo(Tool.Version);
            await Assert.That(Value(output, "Bundled Themes")).IsEqualTo(string.Join(", ", ThemeCatalog.Names));
        }
    }

    #endregion // Report Tests

    #region Color Tests

    /// <summary>
    ///     Verifies that a terminal receives cyan headings, labels in the default color, faint values, and a
    ///     yellow value where no configuration file applies.
    /// </summary>
    [Test]
    public async Task Color_Test_Terminal_ReceivesTheColoredReport()
    {
        // Act
        string output = Report(Create(stdOut: Terminal(isTerminal: true, ColorSystemSupport.Standard)));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(output).Contains("\e[36m[Project]\e[0m");
            await Assert.That(output).Contains($"  Version:        \e[2m{Tool.Version}\e[0m");
            await Assert.That(output).Contains($"  AppSettings:    \e[93m{Undefined}\e[0m");
        }
    }

    /// <summary>
    ///     Verifies that a redirected output and a terminal without colors, as <c>NO_COLOR</c> leaves it, both
    ///     receive plain text.
    /// </summary>
    [Test]
    [Arguments(false, ColorSystemSupport.Standard)]
    [Arguments(true, ColorSystemSupport.NoColors)]
    public async Task Color_Test_RedirectedOrColorless_ReceivesPlainText(bool isTerminal, ColorSystemSupport colors)
    {
        // Act
        string output = Report(Create(stdOut: Terminal(isTerminal, colors)));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(output).DoesNotContain("\e[");
            await Assert.That(output).StartsWith("[Project]");
        }
    }

    #endregion // Color Tests

    #region Runtime Environment Tests

    /// <summary>
    ///     Verifies that a Windows or macOS host is named as <c>dotnet --info</c> names it, beside the runtime
    ///     identifier and the SDK the tool was built with.
    /// </summary>
    [Test]
    public async Task RuntimeEnvironment_Test_WindowsOrMacOsHost_IsNamed()
    {
        // Arrange
        Skip.When(OperatingSystem.IsLinux(), "A Linux host is named from its os-release file, which a test of its own covers.");

        (string name, string platform) = OperatingSystem.IsWindows() ? ("Windows", "Windows") : ("macOS", "Darwin");

        // Act
        string output = Report(Create());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Value(output, "OS Name")).IsEqualTo(name);
            await Assert.That(Environment.OSVersion.Version.ToString()).StartsWith(Value(output, "OS Version"));
            await Assert.That(Value(output, "OS Platform")).IsEqualTo(platform);
            await Assert.That(Value(output, "RID")).IsEqualTo(RuntimeInformation.RuntimeIdentifier);
            await Assert.That(Value(output, "SDK")).IsEqualTo(Tool.SdkVersion);
        }
    }

    /// <summary>
    ///     Verifies that a Linux host is named by the distribution its os-release file names, read from the fallback
    ///     location as well, and by its platform alone without the file.
    /// </summary>
    [Test]
    [Arguments("/etc/os-release", "NAME=\"Ubuntu\"\nVERSION_ID=\"24.04\"\n", "Ubuntu", "24.04")]
    [Arguments("/usr/lib/os-release", "NAME=\"Fedora Linux\"\nVERSION_ID=41\n", "Fedora Linux", "41")]
    [Arguments(null, null, "Linux", "unknown")]
    public async Task RuntimeEnvironment_Test_LinuxHost_IsNamedByItsDistribution(string? path, string? contents, string name, string version)
    {
        // Arrange
        Skip.Unless(OperatingSystem.IsLinux(), "Only a Linux host reads its os-release file.");

        var fileSystem = new MockFileSystem();

        if (path is not null && contents is not null) fileSystem.AddFile(path, new MockFileData(contents));

        // Act
        string output = Report(Create(fileSystem: fileSystem));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Value(output, "OS Name")).IsEqualTo(name);
            await Assert.That(Value(output, "OS Version")).IsEqualTo(version);
            await Assert.That(Value(output, "OS Platform")).IsEqualTo("Linux");
        }
    }

    #endregion // Runtime Environment Tests

    #region Configuration Tests

    /// <summary>
    ///     Verifies that a working directory without a configuration file reports no path at all.
    /// </summary>
    [Test]
    public async Task Configuration_Test_WithoutAConfigurationFile_IsUndefined()
    {
        // Act
        (int exitCode, string output, _) = Invoke(Create(), InfoOption.OptionName);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(ExitCodes.Success);
            await Assert.That(Value(output, "AppSettings")).IsEqualTo(Undefined);
        }
    }

    /// <summary>
    ///     Verifies that the default file in the working directory is reported when no path was requested.
    /// </summary>
    [Test]
    public async Task Configuration_Test_DefaultFileInTheWorkingDirectory_IsReported()
    {
        // Arrange
        string root = Path.Combine(Home, "site");
        IFileSystem fileSystem = Holding(Path.Combine(root, ConfigurationFile.DefaultFileName));

        // Act
        string output = Report(Create(contentRoot: root, fileSystem: fileSystem));

        // Assert
        await Assert.That(Value(output, "AppSettings")).IsEqualTo($"~{Separator}site{Separator}{ConfigurationFile.DefaultFileName}");
    }

    /// <summary>
    ///     Verifies that a requested path that exists and names a JSON file outranks the default file, whether it
    ///     is given relative to the working directory or in full.
    /// </summary>
    [Test]
    [Arguments("site.json")]
    [Arguments(null)]
    public async Task Configuration_Test_RequestedJsonFile_OutranksTheDefaultFile(string? relativePath)
    {
        // Arrange
        string root = Path.Combine(Home, "site");
        string path = Path.Combine(root, "site.json");
        IFileSystem fileSystem = Holding(path, Path.Combine(root, ConfigurationFile.DefaultFileName));

        // Act
        string output = Report(Create(requested: relativePath ?? path, contentRoot: root, fileSystem: fileSystem));

        // Assert
        await Assert.That(Value(output, "AppSettings")).IsEqualTo($"~{Separator}site{Separator}site.json");
    }

    /// <summary>
    ///     Verifies that a requested path that is absent or names no JSON file is disregarded, which leaves the
    ///     default file when one is present and nothing when it is not.
    /// </summary>
    [Test]
    [Arguments("absent.json", false)]
    [Arguments("site.txt", false)]
    [Arguments("absent.json", true)]
    [Arguments("site.txt", true)]
    public async Task Configuration_Test_RequestedPathThatIsNoJsonFile_FallsBackToTheDefaultFile(string requested, bool hasDefault)
    {
        // Arrange
        string root = Path.Combine(Home, "site");
        string[] present = hasDefault
            ? [Path.Combine(root, "site.txt"), Path.Combine(root, ConfigurationFile.DefaultFileName)]
            : [Path.Combine(root, "site.txt")];

        // Act
        string output = Report(Create(requested: requested, contentRoot: root, fileSystem: Holding(present)));

        // Assert
        string expected = hasDefault ? $"~{Separator}site{Separator}{ConfigurationFile.DefaultFileName}" : Undefined;

        await Assert.That(Value(output, "AppSettings")).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a path written behind <c>--info</c> is still read, which the command line configuration
    ///     provider loses by pairing the valueless option with the token that follows it.
    /// </summary>
    [Test]
    public async Task Configuration_Test_RequestedPathBehindTheOption_IsStillRead()
    {
        // Arrange
        string root = Path.Combine(Home, "site");
        IFileSystem fileSystem = Holding(Path.Combine(root, "site.json"), Path.Combine(root, ConfigurationFile.DefaultFileName));
        InfoOption option = Create(contentRoot: root, fileSystem: fileSystem);

        // Act
        (_, string output, _) = Invoke(option, InfoOption.OptionName, ConfigurationFile.OptionName, "site.json");

        // Assert
        await Assert.That(Value(output, "AppSettings")).IsEqualTo($"~{Separator}site{Separator}site.json");
    }

    /// <summary>
    ///     Verifies that the extension is matched without regard to case, a file named <c>.JSON</c> being JSON on
    ///     any file system.
    /// </summary>
    [Test]
    public async Task Configuration_Test_RequestedJsonFileSpelledInCapitals_IsReported()
    {
        // Arrange
        string root = Path.Combine(Home, "site");

        // Act
        string output = Report(Create(requested: "Site.JSON", contentRoot: root, fileSystem: Holding(Path.Combine(root, "Site.JSON"))));

        // Assert
        await Assert.That(Value(output, "AppSettings")).IsEqualTo($"~{Separator}site{Separator}Site.JSON");
    }

    /// <summary>
    ///     Verifies that the home directory and a path below it are shortened to <c>~</c>.
    /// </summary>
    [Test]
    public async Task Configuration_Test_ContentRootBelowTheHomeDirectory_IsShortened()
    {
        // Act
        string atHome = Report(Create(contentRoot: Home));
        string belowHome = Report(Create(contentRoot: Path.Combine(Home, "site")));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Value(atHome, "Content root")).IsEqualTo("~");
            await Assert.That(Value(belowHome, "Content root")).IsEqualTo($"~{Separator}site");
        }
    }

    /// <summary>
    ///     Verifies that a path outside the home directory prints in full, including one whose name only starts like
    ///     the home directory.
    /// </summary>
    [Test]
    public async Task Configuration_Test_ContentRootOutsideTheHomeDirectory_IsPrintedInFull()
    {
        // Arrange
        string elsewhere = Path.GetFullPath("/elsewhere/site");
        string sibling = Path.Combine($"{Home}x", "site");

        // Act
        string outside = Report(Create(contentRoot: elsewhere));
        string alike = Report(Create(contentRoot: sibling));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Value(outside, "Content root")).IsEqualTo(elsewhere);
            await Assert.That(Value(alike, "Content root")).IsEqualTo(sibling);
        }
    }

    #endregion // Configuration Tests

    #region Invocation Tests

    /// <summary>
    ///     Verifies that a parse error is printed instead of the report, which would otherwise name the default file
    ///     in place of the one a misspelled <c>--config</c> meant.
    /// </summary>
    [Test]
    [Arguments("--info --conifg site.json")]
    [Arguments("--info true")]
    public async Task Invoke_Test_ParseError_PrintsTheErrorInsteadOfTheReport(string commandLine)
    {
        // Act
        (int exitCode, string output, string error) = Invoke(Create(), commandLine.Split(' '));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(ExitCodes.Error);
            await Assert.That(output).DoesNotContain("[Project]");
            await Assert.That(error).IsNotEmpty();
        }
    }

    #endregion // Invocation Tests

    #region Helpers

    private static InfoOption Create(string? requested = null, string? contentRoot = null, IFileSystem? fileSystem = null, IAnsiConsole? stdOut = null)
    {
        var builder = new ConfigurationBuilder();

        if (requested is not null)
        {
            builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationFile.PathKey] = requested,
            });
        }

        return new InfoOption(
            builder.Build(),
            new HostEnvironmentStub { ContentRootPath = contentRoot ?? Path.GetFullPath("/site") },
            fileSystem ?? new MockFileSystem(),
            stdOut ?? CapturedConsole.Create(TextWriter.Null));
    }

    private static IFileSystem Holding(params string[] paths)
    {
        var fileSystem = new MockFileSystem();

        foreach (string path in paths)
        {
            fileSystem.AddFile(path, new MockFileData("{}"));
        }

        return fileSystem;
    }

    private static IAnsiConsole Terminal(bool isTerminal, ColorSystemSupport colors) =>
        AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = colors,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Out = new AnsiConsoleOutputStub(TextWriter.Null, isTerminal),
        });

    private static (int ExitCode, string Output, string Error) Invoke(InfoOption option, params string[] args)
    {
        var command = new RootCommand();
        command.Options.Add(new Option<string>(ConfigurationFile.OptionName) { Recursive = true });
        command.Options.Add(option);

        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = command.Parse(args).Invoke(new InvocationConfiguration { Output = output, Error = error });

        return (exitCode, output.ToString(), error.ToString());
    }

    private static string Report(InfoOption option) =>
        Invoke(option, InfoOption.OptionName).Output;

    private static string[] Lines(string output) =>
        output.Split(Environment.NewLine);

    private static string Value(string output, string label) =>
        Lines(output).Single(line => line.StartsWith($"  {label}:", StringComparison.Ordinal))[(label.Length + 3)..].TrimStart(' ');

    private static int ValueColumn(string line) =>
        line.Length - line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].TrimStart(' ').Length;

    #endregion
}
