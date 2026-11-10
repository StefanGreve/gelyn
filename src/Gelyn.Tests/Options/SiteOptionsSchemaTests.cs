using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

using Gelyn.Core;
using Gelyn.Model.Options;

namespace Gelyn.Tests.Options;

/// <summary>
///     Unit tests that keep <c>gelyn.schema.json</c> in step with <see cref="SiteOptions"/>.
/// </summary>
public class SiteOptionsSchemaTests
{
    private const string SchemaFileName = "gelyn.schema.json";

    #region Schema Tests

    /// <summary>
    ///     Verifies that the schema documents exactly the properties the options type exposes.
    /// </summary>
    [Test]
    public async Task Schema_Test_DocumentsEveryOptionsProperty()
    {
        // Arrange
        IEnumerable<string> expected = typeof(SiteOptions)
            .GetProperties()
            .Select(property => property.Name)
            .Order();

        // Act
        IEnumerable<string> documented = ReadDocumentedProperties()
            .EnumerateObject()
            .Select(property => property.Name)
            .Where(name => !name.StartsWith('$'))
            .Order();

        // Assert
        await Assert.That(documented).IsEquivalentTo(expected);
    }

    /// <summary>
    ///     Verifies that every documented default matches the value the options type initializes to.
    /// </summary>
    /// <remarks>
    ///     Compared as serialized JSON and never as values, so that a default whose type changed is a
    ///     failure too. Every property is reported, because a schema that has fallen behind has usually
    ///     fallen behind in more than one place.
    /// </remarks>
    [Test]
    public async Task Schema_Test_DefaultsMatchTheOptionsType()
    {
        // Arrange
        var options = new SiteOptions();
        JsonElement documented = ReadDocumentedProperties();

        // Act and Assert
        using (Assert.Multiple())
        {
            foreach (PropertyInfo property in typeof(SiteOptions).GetProperties())
            {
                string initialized = JsonSerializer.Serialize(property.GetValue(options));
                string declared = documented
                    .GetProperty(property.Name)
                    .GetProperty("default")
                    .GetRawText();

                await Assert.That(declared).IsEqualTo(initialized);
            }
        }
    }

    /// <summary>
    ///     Verifies that the schema offers the bundled themes and <c>null</c> as the values of the theme, so that
    ///     an editor neither flags a name the tool accepts nor offers one the validator rejects.
    /// </summary>
    [Test]
    public async Task Schema_Test_ThemeEnumMatchesTheBundledThemes()
    {
        // Arrange
        string?[] expected = [.. ThemeCatalog.Names, null];

        // Act
        IEnumerable<string?> documented = ReadDocumentedProperties()
            .GetProperty(nameof(SiteOptions.Theme))
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString());

        // Assert
        await Assert.That(documented).IsEquivalentTo(expected);
    }

    /// <summary>
    ///     Verifies that every property the schema declares, at every depth, carries a description, which is
    ///     what an editor shows beside the key.
    /// </summary>
    [Test]
    public async Task Schema_Test_EveryPropertyCarriesADescription()
    {
        // Arrange
        JsonElement documented = ReadDocumentedProperties();

        // Act
        IEnumerable<string> undescribed = EnumerateProperties(documented, string.Empty)
            .Where(static property => !property.Schema.TryGetProperty("description", out JsonElement description) || string.IsNullOrWhiteSpace(description.GetString()))
            .Select(static property => property.Path);

        // Assert
        await Assert.That(undescribed).IsEmpty();
    }

    /// <summary>
    ///     Verifies that every setting offers at least one example, so that an editor can suggest a value.
    /// </summary>
    [Test]
    public async Task Schema_Test_EverySettingCarriesAnExample()
    {
        // Act
        IEnumerable<string> unexemplified = ReadDocumentedProperties()
            .EnumerateObject()
            .Where(static property => !property.Name.StartsWith('$'))
            .Where(static property => !property.Value.TryGetProperty("examples", out JsonElement examples) || examples.GetArrayLength() == 0)
            .Select(static property => property.Name);

        // Assert
        await Assert.That(unexemplified).IsEmpty();
    }

    #endregion // Schema Tests

    #region Helpers

    // Descends into nested objects and into the objects a list holds, which is where FooterLinks keeps its keys.
    private static IEnumerable<(string Path, JsonElement Schema)> EnumerateProperties(JsonElement properties, string prefix)
    {
        foreach (JsonProperty property in properties.EnumerateObject())
        {
            string path = $"{prefix}{property.Name}";

            yield return (path, property.Value);

            JsonElement schema = property.Value.TryGetProperty("items", out JsonElement items) ? items : property.Value;

            if (!schema.TryGetProperty("properties", out JsonElement nested))
                continue;

            foreach ((string Path, JsonElement Schema) member in EnumerateProperties(nested, $"{path}."))
                yield return member;
        }
    }

    private static JsonElement ReadDocumentedProperties()
    {
        string path = Path.Combine(AppContext.BaseDirectory, SchemaFileName);

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        // Cloned because every JsonElement is invalidated once the owning document is disposed.
        return document.RootElement
            .GetProperty("properties")
            .Clone();
    }

    #endregion
}
