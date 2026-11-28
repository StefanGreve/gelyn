using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;

using Gelyn.Core;
using Gelyn.Core.Internals;
using Gelyn.Core.Internals.Primitives;
using Gelyn.Core.Internals.Terminal;
using Gelyn.Model;

namespace Gelyn.Cli;

/// <summary>
///     Represents the <c>--info</c> option, which prints build, platform and configuration information for a bug
///     report.
/// </summary>
/// <remarks>
///     The action terminates the invocation, as the action of <c>--version</c> does. It probes the configuration
///     file and loads no setting, so it succeeds when the file is missing, does not parse or cannot be opened.
/// </remarks>
internal sealed class InfoOption : Option<bool>
{
    /// <summary>
    ///     The name of the option on the command line.
    /// </summary>
    internal const string OptionName = "--info";

    private const string Unknown = "unknown";

    private const string Undefined = "(undefined)";

    private const string HeadingCodes = AnsiCodes.DarkCyan;
    private const string ValueCodes = AnsiCodes.Dim;
    private const string UndefinedCodes = AnsiCodes.BrightYellow;
    private const string FailureCodes = AnsiCodes.BrightRed;

    // The os-release specification makes the second file the fallback for the first.
    private static readonly string[] OsReleasePaths = ["/etc/os-release", "/usr/lib/os-release"];

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoOption"/> class.
    /// </summary>
    /// <param name="open">
    ///     The factory that opens the session of a parsed command line.
    /// </param>
    public InfoOption(Func<ParseResult, Session> open)
        : base(OptionName)
    {
        this.Description = "Show build, platform and configuration information.";
        this.Arity = ArgumentArity.Zero;
        this.Action = new InfoAction(open);
    }

    // The default file systems of Windows and macOS ignore case, so the home directory may be spelled otherwise.
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    #region Helpers

    private static string Report(Session session)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        (string osName, string osVersion, string osPlatform) = HostOperatingSystem(session.FileSystem);
        (string appSettings, string appSettingsCodes) = AppSettings(session.Settings.Probe(), home);

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
            new("Configuration", "Content root", ShortenPath(session.ContentRoot, home)),
            new("Configuration", "AppSettings", appSettings, appSettingsCodes),
        ];

        string? group = null;
        int width = entries.Max(static entry => entry.Label.Length) + 2;
        ConsoleStream stdOut = session.StdOut;
        var report = new StringBuilder();

        foreach (Entry entry in entries)
        {
            if (entry.Group != group)
            {
                if (group is not null) report.AppendLine();

                report.AppendLine(stdOut.Paint($"[{entry.Group}]", HeadingCodes));
                group = entry.Group;
            }

            report.Append("  ").Append((entry.Label + ":").PadRight(width)).AppendLine(stdOut.Paint(entry.Value, entry.Codes ?? ValueCodes));
        }

        return report.ToString();
    }

    // Named as dotnet --info names them, except for macOS, which it still calls Mac OS X.
    private static (string Name, string Version, string Platform) HostOperatingSystem(IFileSystem fileSystem)
    {
        Version version = Environment.OSVersion.Version;
        string release = version.ToString(version.Build >= 0 ? 3 : 2);

        if (OperatingSystem.IsWindows()) return ("Windows", release, "Windows");
        if (OperatingSystem.IsMacOS()) return ("macOS", release, "Darwin");
        if (!OperatingSystem.IsLinux()) return (Unknown, release, Unknown);

        Dictionary<string, string> osRelease = ReadOsRelease(fileSystem);
        return (osRelease.GetValueOrDefault("NAME", "Linux"), osRelease.GetValueOrDefault("VERSION_ID", Unknown), "Linux");
    }

    private static Dictionary<string, string> ReadOsRelease(IFileSystem fileSystem)
    {
        Dictionary<string, string> fields = new(StringComparer.Ordinal);

        if (OsReleasePaths.FirstOrDefault(fileSystem.File.Exists) is not string path) return fields;

        foreach (string line in fileSystem.File.ReadLines(path))
        {
            int separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0) fields[line[..separator]] = line[(separator + 1)..].Trim('"', '\'');
        }

        return fields;
    }

    // Read from a probe of the run's own settings, so the line names the file the run applies and cannot disagree with it.
    private static (string Value, string Codes) AppSettings(ConfigurationFileStatus file, string home)
    {
        string path = ShortenPath(file.Path, home);

        return file switch
        {
            { Exists: false, Optional: false } => ($"{path} (not found)", FailureCodes),
            { Failure: Exception failure } => ($"{path} (not readable: {ShortenPaths(failure.GetBaseException().Message, home)})", FailureCodes),
            { Exists: true } => (path, ValueCodes),
            _ => (Undefined, UndefinedCodes),
        };
    }

    private static string ShortenPaths(string text, string home) =>
        home.Length == 0 ? text : text.Replace(home + Path.DirectorySeparatorChar, "~" + Path.DirectorySeparatorChar, PathComparison);

    private static string ShortenPath(string path, string home)
    {
        if (home.Length == 0) return path;
        if (path.Equals(home, PathComparison)) return "~";

        return StringUtils.StartsWithSegment(path, home, Path.DirectorySeparatorChar, PathComparison)
            ? $"~{path[home.Length..]}"
            : path;
    }

    #endregion

    private readonly record struct Entry(string Group, string Label, string Value, string? Codes = null);

    private sealed class InfoAction(Func<ParseResult, Session> open) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            // A terminating option runs despite parse errors, which would hide a misspelled --config.
            if (parseResult.Errors.Count > 0)
                return new ParseErrorAction().Invoke(parseResult);

            using Session session = open(parseResult);

            if (!session.StdOut.IsDiscarded)
                parseResult.InvocationConfiguration.Output.Write(Report(session));

            return ExitCodes.Success;
        }
    }
}
