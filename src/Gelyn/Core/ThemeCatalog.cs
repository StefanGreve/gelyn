using Gelyn.Model;

namespace Gelyn.Core;

/// <summary>
///     The themes bundled with the tool, which <c>Gelyn.csproj</c> embeds in the assembly from
///     <c>plugins/gelyn/skills/bootstrap/themes</c>.
/// </summary>
/// <remarks>
///     A theme is the set of files that share a base name, such as <c>modern.css</c> and <c>modern.js</c>. The
///     catalog reads them from the manifest of the assembly, because the folder does not exist where the tool is
///     installed.
/// </remarks>
internal static class ThemeCatalog
{
    private const string ResourcePrefix = "themes/";
    private const string StylesheetExtension = ".css";
    private const string ScriptExtension = ".js";

    private static readonly Dictionary<string, ThemeFile[]> Themes = CollectThemes();

    /// <summary>
    ///     The names of the bundled themes, in ordinal order.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = [.. Themes.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    ///     The files of one bundled theme.
    /// </summary>
    /// <param name="name">
    ///     The name of the theme, compared ordinally.
    /// </param>
    /// <returns>
    ///     The files of the theme in ordinal order of their names, which puts <c>modern.css</c> ahead of
    ///     <c>modern.js</c>, or an empty list when no theme of that name is bundled.
    /// </returns>
    public static IReadOnlyList<ThemeFile> Files(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Themes.TryGetValue(name, out ThemeFile[]? files) ? files : [];
    }

    /// <summary>
    ///     Reads one file of a bundled theme.
    /// </summary>
    /// <param name="file">
    ///     The file, as <see cref="Files"/> lists it.
    /// </param>
    /// <returns>
    ///     The bytes of the file as the build embedded them.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     <paramref name="file"/> names a resource the assembly does not hold.
    /// </exception>
    public static byte[] Read(ThemeFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        using Stream stream = typeof(ThemeCatalog).Assembly.GetManifestResourceStream(file.ResourceName)
            ?? throw new ArgumentException($"No theme file is embedded as '{file.ResourceName}'.", nameof(file));

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }

    #region Helpers

    private static Dictionary<string, ThemeFile[]> CollectThemes() =>
        typeof(ThemeCatalog).Assembly
            .GetManifestResourceNames()
            .Where(static name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(static name => new ThemeFile
            {
                FileName = name[ResourcePrefix.Length..],
                Kind = Classify(name),
                ResourceName = name,
            })
            .GroupBy(static file => Path.GetFileNameWithoutExtension(file.FileName), StringComparer.Ordinal)
            .ToDictionary(
                static theme => theme.Key,
                static theme => theme.OrderBy(static file => file.FileName, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

    private static ContentKind Classify(string name)
    {
        string extension = Path.GetExtension(name);

        if (extension.Equals(StylesheetExtension, StringComparison.OrdinalIgnoreCase)) return ContentKind.Stylesheet;
        if (extension.Equals(ScriptExtension, StringComparison.OrdinalIgnoreCase)) return ContentKind.Script;

        throw new InvalidOperationException("A bundled theme file is always a stylesheet or a script.");
    }

    #endregion
}
