using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Gelyn.Core;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Tests.Core;

/// <summary>
///     Unit tests for the <see cref="ThemeCatalog"/> class.
/// </summary>
public class ThemeCatalogTests
{
    private const string ThemeDirectoryName = "themes";
    private const string ConfigurationPageName = "configuration.md";

    #region Names Tests

    /// <summary>
    ///     Verifies that the bundled themes are the distinct base names of the stylesheets and scripts in the
    ///     theme folder, in ordinal order.
    /// </summary>
    [Test]
    public async Task Names_Test_ThemeFolder_YieldsItsDistinctBaseNames()
    {
        // Arrange
        // Joined instead of compared as a sequence, because IsEquivalentTo disregards the order under test.
        string expected = string.Join(',', SourceFiles()
            .Select(static path => Path.GetFileNameWithoutExtension(path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));

        // Act
        string names = string.Join(',', ThemeCatalog.Names);

        // Assert
        await Assert.That(names).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that the theme list of the configuration page names every bundled theme and no other, so
    ///     that a stray stylesheet in the theme folder fails, and a new theme fails until it is documented.
    /// </summary>
    [Test]
    public async Task Names_Test_ConfigurationPage_DocumentsEveryBundledTheme()
    {
        // Arrange
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, ConfigurationPageName));

        // Act
        IEnumerable<string> documented = lines
            .SkipWhile(static line => line != "## Themes")
            .Skip(1)
            .TakeWhile(static line => !line.StartsWith("## ", StringComparison.Ordinal))
            .Where(static line => line.StartsWith("- `", StringComparison.Ordinal))
            .Select(static line => line[3..line.IndexOf('`', 3)]);

        // Assert
        await Assert.That(documented).IsEquivalentTo(ThemeCatalog.Names);
    }

    /// <summary>
    ///     Verifies that the sample error of the configuration page lists the bundled themes as the validator
    ///     reports them.
    /// </summary>
    [Test]
    public async Task Names_Test_ConfigurationPage_SampleErrorListsEveryBundledTheme()
    {
        // Arrange
        string expected = $"error: {nameof(SiteOptions.Theme)} must name a bundled theme: {string.Join(", ", ThemeCatalog.Names)}.";

        // Act
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, ConfigurationPageName));

        // Assert
        await Assert.That(lines).Contains(expected);
    }

    #endregion // Names Tests

    #region Files Tests

    /// <summary>
    ///     Verifies that a theme lists its stylesheet before its script, as the ordinal order of their names
    ///     puts them.
    /// </summary>
    [Test]
    public async Task Files_Test_Modern_ListsTheStylesheetBeforeTheScript()
    {
        // Act
        string files = Describe(ThemeCatalog.Files("modern"));

        // Assert
        await Assert.That(files).IsEqualTo("modern.css:Stylesheet,modern.js:Script");
    }

    /// <summary>
    ///     Verifies that a name no bundled theme carries lists no files, instead of failing the lookup.
    /// </summary>
    [Test]
    public async Task Files_Test_NameNotBundled_IsEmpty()
    {
        // Act and Assert
        await Assert.That(ThemeCatalog.Files("plain")).IsEmpty();
    }

    #endregion // Files Tests

    #region Read Tests

    /// <summary>
    ///     Verifies that a stylesheet or a script in the theme folder is embedded byte for byte, so that a file
    ///     the build leaves out of the assembly, such as one moved into a satellite assembly, fails by name.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(SourceFileNames))]
    public async Task Read_Test_ThemeFile_MatchesTheSourceByteForByte(string fileName)
    {
        // Arrange
        string path = Path.Combine(AppContext.BaseDirectory, ThemeDirectoryName, fileName);
        ThemeFile? embedded = ThemeCatalog.Names
            .SelectMany(ThemeCatalog.Files)
            .FirstOrDefault(file => file.FileName.Equals(fileName, StringComparison.Ordinal));

        // Act
        // Compared as base64, which is sensitive to the order of the bytes where IsEquivalentTo is not.
        string expected = Convert.ToBase64String(File.ReadAllBytes(path));
        string? actual = embedded is null ? null : Convert.ToBase64String(ThemeCatalog.Read(embedded));

        // Assert
        await Assert.That(actual).IsEqualTo(expected);
    }

    #endregion // Read Tests

    #region Helpers

    /// <summary>
    ///     The names of the stylesheets and scripts in the theme folder.
    /// </summary>
    public static IEnumerable<string> SourceFileNames() =>
        SourceFiles().Select(static path => Path.GetFileName(path));

    // Joined instead of compared as a sequence, because IsEquivalentTo disregards the order under test.
    private static string Describe(IEnumerable<ThemeFile> files) =>
        string.Join(',', files.Select(static file => $"{file.FileName}:{file.Kind}"));

    // The copy of the theme folder in the output directory, of which the build embeds the stylesheets and
    // scripts alone.
    private static IEnumerable<string> SourceFiles() =>
        Directory
            .EnumerateFiles(Path.Combine(AppContext.BaseDirectory, ThemeDirectoryName))
            .Where(static path => Path.GetExtension(path) is ".css" or ".js");

    #endregion
}
