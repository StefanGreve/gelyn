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
using Gelyn.Extensions;
using Gelyn.Model;
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
    ///     Verifies that a terminal receives cyan headings, labels in the default color, faint values, and the file
    ///     state in the color of its meaning.
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
            await Assert.That(output).Contains(" \e[2m(not found, the defaults apply)\e[0m");
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
    ///     Verifies that the file line reports what the first load found, and that the option runs in each case.
    /// </summary>
    [Test]
    [Arguments("""{ "Title": "Site" }""", true, "read")]
    [Arguments(null, false, "not found, the defaults apply")]
    [Arguments(null, true, "not found")]
    public async Task Configuration_Test_FileState_IsReported(string? contents, bool requested, string state)
    {
        // Arrange
        using var fixture = DiskFixture.Create();

        if (contents is not null) fixture.WriteOutside(ConfigurationFile.DefaultFileName, contents);

        InfoOption option = Loaded(fixture, requested);

        // Act
        (int exitCode, string output, _) = Invoke(option, InfoOption.OptionName);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(ExitCodes.Success);
            await Assert.That(Value(output, "File")).EndsWith($"{Separator}{ConfigurationFile.DefaultFileName} ({state})");
        }
    }

    /// <summary>
    ///     Verifies that a file that does not parse is reported with the position the parser stopped at, and that
    ///     the option still runs.
    /// </summary>
    [Test]
    public async Task Configuration_Test_FileThatDoesNotParse_ShowsTheParserPosition()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside(ConfigurationFile.DefaultFileName, """{ "Title": """);

        InfoOption option = Loaded(fixture, requested: true);

        // Act
        (int exitCode, string output, _) = Invoke(option, InfoOption.OptionName);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(exitCode).IsEqualTo(ExitCodes.Success);
            await Assert.That(Value(output, "File")).Contains("(not readable: ").And.Contains("LineNumber");
        }
    }

    /// <summary>
    ///     Verifies that a failure whose message names a path below the home directory shows <c>~</c> in its place,
    ///     as the message of an I/O failure does from .NET 11 on.
    /// </summary>
    [Test]
    public async Task Configuration_Test_FailureMessage_ShortensTheHomeDirectory()
    {
        // Arrange
        string root = Path.Combine(Home, "site");
        string path = Path.Combine(root, ConfigurationFile.DefaultFileName);
        string shortened = $"~{Separator}site{Separator}{ConfigurationFile.DefaultFileName}";

        var failure = new IOException($"The process cannot access the file '{path}' because it is being used by another process.");
        InfoOption option = Create(file: new ConfigurationFileStatus { Exists = true, Failure = failure }, contentRoot: root);

        // Act
        string output = Report(option);

        // Assert
        await Assert.That(Value(output, "File")).IsEqualTo($"{shortened} (not readable: The process cannot access the file '{shortened}' because it is being used by another process.)");
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

    private static InfoOption Create(ConfigurationFileStatus? file = null, string? contentRoot = null, IFileSystem? fileSystem = null, IAnsiConsole? stdOut = null) =>
        new InfoOption(
            new ConfigurationBuilder().Build(),
            new HostEnvironmentStub { ContentRootPath = contentRoot ?? Path.GetFullPath("/site") },
            file ?? new ConfigurationFileStatus { Exists = false, Failure = null },
            fileSystem ?? new MockFileSystem(),
            stdOut ?? CapturedConsole.Create(TextWriter.Null));

    private static IAnsiConsole Terminal(bool isTerminal, ColorSystemSupport colors) =>
        AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = colors,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Out = new AnsiConsoleOutputStub(TextWriter.Null, isTerminal),
        });

    private static InfoOption Loaded(DiskFixture fixture, bool requested)
    {
        var configuration = new ConfigurationManager();
        configuration.SetBasePath(fixture.Root);

        if (requested)
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConfigurationFile.PathKey] = Path.Combine(fixture.Root, ConfigurationFile.DefaultFileName),
            });
        }

        ConfigurationFileStatus file = configuration.AddSiteConfiguration([]);

        return new InfoOption(configuration, new HostEnvironmentStub { ContentRootPath = fixture.Root }, file, new MockFileSystem(), CapturedConsole.Create(TextWriter.Null));
    }

    private static (int ExitCode, string Output, string Error) Invoke(InfoOption option, params string[] args)
    {
        var command = new RootCommand();
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
