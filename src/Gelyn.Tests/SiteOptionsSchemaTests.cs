using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

using Gelyn.Model.Options;

namespace Gelyn.Tests;

/// <summary>
///     Unit tests that keep <c>gelyn.schema.json</c> in step with <see cref="SiteOptions"/>.
/// </summary>
public partial class SiteOptionsSchemaTests
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
    [Test]
    public async Task Schema_Test_DefaultsMatchTheOptionsType()
    {
        // Arrange
        SiteOptions options = new();
        JsonElement documented = ReadDocumentedProperties();

        foreach (PropertyInfo property in typeof(SiteOptions).GetProperties())
        {
            // Act
            string initialized = JsonSerializer.Serialize(property.GetValue(options));
            string declared = documented
                .GetProperty(property.Name)
                .GetProperty("default")
                .GetRawText();

            // Assert
            await Assert.That(declared).IsEqualTo(initialized);
        }
    }

    #endregion

    #region Helpers

    private static JsonElement ReadDocumentedProperties()
    {
        string path = Path.Combine(AppContext.BaseDirectory, SchemaFileName);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        // Cloned because every JsonElement is invalidated once the owning document is disposed.
        return document.RootElement
            .GetProperty("properties")
            .Clone();
    }

    #endregion
}
