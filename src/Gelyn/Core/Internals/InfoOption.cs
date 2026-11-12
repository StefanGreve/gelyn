using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO.Abstractions;
using System.Runtime.InteropServices;

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

    private const string Undefined = "(undefined)";

    private static readonly Style HeadingStyle = new(foreground: Color.Teal);
    private static readonly Style ValueStyle = new(decoration: Decoration.Dim);
    private static readonly Style UndefinedStyle = new(foreground: Color.Yellow);

    // The os-release specification makes the second file the fallback for the first.
    private static readonly string[] OsReleasePaths = ["/etc/os-release", "/usr/lib/os-release"];

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
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
    /// <param name="fileSystem">
    ///     Tells whether a configuration file is present, and reads the name and the version of a Linux
    ///     distribution from its os-release file.
    /// </param>
    /// <param name="stdOut">
    ///     The console on standard output, whose capabilities decide whether the report is colored.
    /// </param>
    public InfoOption(IConfiguration configuration, IHostEnvironment environment, IFileSystem fileSystem, [FromKeyedServices(ConsoleTarget.StdOut)] IAnsiConsole stdOut)
        : base(OptionName)
    {
        this._configuration = configuration;
        this._environment = environment;
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

    private string Report(ParseResult parseResult, bool color)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        (string osName, string osVersion, string osPlatform) = this.HostOperatingSystem();
        string appSettings = this.AppSettings(parseResult, home);
        Style appSettingsStyle = appSettings == Undefined ? UndefinedStyle : ValueStyle;

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
            new("Configuration", "AppSettings", appSettings, appSettingsStyle),
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

            writer.Write("  ").Write((entry.Label + ":").PadRight(width)).Write(entry.Value, entry.Style ?? ValueStyle, null).WriteLine();
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

    // An explicit path outranks the default file, and one that names no JSON file falls back to it, so the
    // report names the file a reader can open instead of the one the loader was pointed at.
    private string AppSettings(ParseResult parseResult, string home)
    {
        string root = this._environment.ContentRootPath;

        if ((RequestedPath(parseResult) ?? this._configuration[ConfigurationFile.PathKey]) is string requested)
        {
            string path = PathBoundary.Resolve(root, requested);

            if (this.IsJsonFile(path)) return ShortenPath(path, home);
        }

        string file = PathBoundary.Resolve(root, ConfigurationFile.DefaultFileName);
        return this._fileSystem.File.Exists(file) ? ShortenPath(file, home) : Undefined;
    }

    // The extension is compared without case everywhere, a file named .JSON being JSON on any file system.
    private bool IsJsonFile(string path) =>
        this._fileSystem.File.Exists(path) && Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase);

    // The parsed value is read first, because the command line configuration provider pairs a valueless option
    // such as --info with the token behind it, which swallows a --config written after it.
    private static string? RequestedPath(ParseResult parseResult) =>
        parseResult.RootCommandResult.Command.Options.FirstOrDefault(IsConfigurationOption) is Option<string> option
            ? parseResult.GetValue(option)
            : null;

    private static bool IsConfigurationOption(Option option) =>
        option.Name.Equals(ConfigurationFile.OptionName, StringComparison.Ordinal);

    private static string ShortenPath(string path, string home)
    {
        if (home.Length == 0) return path;
        if (path.Equals(home, PathComparison)) return "~";

        return StringUtils.StartsWithSegment(path, home, Path.DirectorySeparatorChar, PathComparison)
            ? $"~{path[home.Length..]}"
            : path;
    }

    #endregion

    private readonly record struct Entry(string Group, string Label, string Value, Style? Style = null);

    private sealed class InfoAction(InfoOption option) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            // A terminating option runs despite parse errors, which would hide a misspelled --config.
            if (parseResult.Errors.Count > 0)
                return new ParseErrorAction().Invoke(parseResult);

            parseResult.InvocationConfiguration.Output.Write(option.Report(parseResult, option.UsesColor));

            return ExitCodes.Success;
        }
    }
}
