using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO.Abstractions;
using System.Runtime.InteropServices;

using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Spectre.Console;

namespace Gelyn.Core.Internals;

/// <summary>
///     The option that prints build, platform and configuration information for a bug report.
/// </summary>
/// <remarks>
///     Its action replaces the command's, as the action of <c>--version</c> does, and reads no
///     <see cref="SiteOptions"/>, so it runs while the configuration file fails to load.
/// </remarks>
internal sealed class InfoOption : Option<bool>
{
    /// <summary>
    ///     The name of the option on the command line.
    /// </summary>
    internal const string OptionName = "--info";

    private const string Unknown = "unknown";

    private static readonly Style HeadingStyle = new(foreground: Color.Teal);
    private static readonly Style ValueStyle = new(decoration: Decoration.Dim);
    private static readonly Style SuccessStyle = new(foreground: Color.Green);
    private static readonly Style FailureStyle = new(foreground: Color.Red);

    // The os-release specification makes the second file the fallback for the first.
    private static readonly string[] OsReleasePaths = ["/etc/os-release", "/usr/lib/os-release"];

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ConfigurationFileStatus _file;
    private readonly IFileSystem _fileSystem;
    private readonly IAnsiConsole _stdOut;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoOption"/> class.
    /// </summary>
    /// <param name="configuration">
    ///     The configuration that names the configuration file, read as written.
    /// </param>
    /// <param name="environment">
    ///     The host environment whose content root the configuration file is resolved against.
    /// </param>
    /// <param name="file">
    ///     What the first load of the configuration file found.
    /// </param>
    /// <param name="fileSystem">
    ///     Reads the name and the version of a Linux distribution from its os-release file.
    /// </param>
    /// <param name="stdOut">
    ///     The console on standard output, whose capabilities decide whether the report is colored.
    /// </param>
    public InfoOption(IConfiguration configuration, IHostEnvironment environment, ConfigurationFileStatus file, IFileSystem fileSystem, [FromKeyedServices(ConsoleTarget.StdOut)] IAnsiConsole stdOut)
        : base(OptionName)
    {
        this._configuration = configuration;
        this._environment = environment;
        this._file = file;
        this._fileSystem = fileSystem;
        this._stdOut = stdOut;

        this.Description = "Show build, platform and configuration information.";
        this.Arity = ArgumentArity.Zero;
        this.Action = new InfoAction(this);
    }

    // The default file systems of Windows and macOS ignore case, so the home directory may be spelled otherwise.
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // Only a terminal gets color, so a redirected report stays plain text, and NO_COLOR clears the color system.
    private bool UsesColor =>
        this._stdOut.Profile is { Out.IsTerminal: true, Capabilities: { Ansi: true, ColorSystem: not ColorSystem.NoColors } };

    #region Helpers

    private string Report(bool color)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        (string osName, string osVersion, string osPlatform) = this.HostOperatingSystem();
        (string path, string state, Style stateStyle) = this.ConfigurationFileState(home);

        Entry[] entries =
        [
            new("Project", "Author", Tool.Author ?? Unknown),
            new("Project", "Version", Tool.Version),
            new("Project", "License", Tool.License ?? Unknown),
            new("Project", "Repository", Tool.RepositoryUrl ?? Unknown),
            new("Project", "Revision", Tool.Revision ?? Unknown),
            new("Runtime Environment", "OS Name", osName),
            new("Runtime Environment", "OS Version", osVersion),
            new("Runtime Environment", "OS Platform", osPlatform),
            new("Runtime Environment", "RID", RuntimeInformation.RuntimeIdentifier),
            new("Runtime Environment", "SDK", Tool.SdkVersion ?? Unknown),
            new("Configuration", "Bundled Themes", string.Join(", ", ThemeCatalog.Names)),
            new("Configuration", "Content root", ShortenPath(this._environment.ContentRootPath, home)),
            new("Configuration", "File", path, (state, stateStyle)),
        ];

        string? group = null;
        int width = entries.Max(static entry => entry.Label.Length) + 2;
        using var report = new StringWriter(Tool.Culture);

        // Standard emits the 16-color codes, which every ANSI terminal maps to its scheme.
        var writer = new AnsiWriter(
            report,
            new AnsiCapabilities
            {
                Ansi = color,
                ColorSystem = ColorSystem.Standard
            }
        );

        foreach (Entry entry in entries)
        {
            if (entry.Group != group)
            {
                if (group is not null) writer.WriteLine();

                writer.Write($"[{entry.Group}]", HeadingStyle, null).WriteLine();
                group = entry.Group;
            }

            writer.Write("  ").Write((entry.Label + ":").PadRight(width)).Write(entry.Value, ValueStyle, null);

            if (entry.State is (string text, Style style)) writer.Write(" ").Write($"({text})", style, null);

            writer.WriteLine();
        }

        return report.ToString();
    }

    // Named as dotnet --info names them, except for macOS, which it still calls Mac OS X.
    private (string Name, string Version, string Platform) HostOperatingSystem()
    {
        Version version = Environment.OSVersion.Version;
        string release = version.ToString(version.Build >= 0 ? 3 : 2);

        if (OperatingSystem.IsWindows()) return ("Windows", release, "Windows");
        if (OperatingSystem.IsMacOS()) return ("macOS", release, "Darwin");
        if (!OperatingSystem.IsLinux()) return (Unknown, release, Unknown);

        Dictionary<string, string> osRelease = this.ReadOsRelease();

        return (osRelease.GetValueOrDefault("NAME", "Linux"), osRelease.GetValueOrDefault("VERSION_ID", Unknown), "Linux");
    }

    private Dictionary<string, string> ReadOsRelease()
    {
        Dictionary<string, string> fields = new(StringComparer.Ordinal);

        if (OsReleasePaths.FirstOrDefault(this._fileSystem.File.Exists) is not string path) return fields;

        foreach (string line in this._fileSystem.File.ReadLines(path))
        {
            int separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0) fields[line[..separator]] = line[(separator + 1)..].Trim('"', '\'');
        }

        return fields;
    }

    private (string Path, string State, Style Style) ConfigurationFileState(string home)
    {
        string path = ShortenPath(ConfigurationFile.Resolve(this._configuration, this._environment.ContentRootPath), home);

        (string state, Style style) = this._file switch
        {
            { Failure: FileNotFoundException } => ("not found", FailureStyle),
            { Failure: Exception failure } => ($"not readable: {ShortenPaths(failure.GetBaseException().Message, home)}", FailureStyle),
            { Exists: true } => ("read", SuccessStyle),
            _ => ("not found, the defaults apply", ValueStyle),
        };

        return (path, state, style);
    }

    private static string ShortenPath(string path, string home)
    {
        if (home.Length == 0) return path;
        if (path.Equals(home, PathComparison)) return "~";

        return StringUtils.StartsWithSegment(path, home, Path.DirectorySeparatorChar, PathComparison)
            ? $"~{path[home.Length..]}"
            : path;
    }

    private static string ShortenPaths(string text, string home) =>
        home.Length == 0 ? text : text.Replace(home + Path.DirectorySeparatorChar, "~" + Path.DirectorySeparatorChar, PathComparison);

    #endregion

    private readonly record struct Entry(string Group, string Label, string Value, (string Text, Style Style)? State = null);

    private sealed class InfoAction(InfoOption option) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            // A terminating option runs despite parse errors, which would hide a misspelled --config.
            if (parseResult.Errors.Count > 0)
                return new ParseErrorAction().Invoke(parseResult);

            parseResult.InvocationConfiguration.Output.Write(option.Report(option.UsesColor));

            return ExitCodes.Success;
        }
    }
}
