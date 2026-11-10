using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Gelyn.Schema;

/// <summary>
///     The XML documentation the compiler writes beside an assembly, read for the summaries and examples that
///     <c>gelyn.schema.json</c> shows in an editor.
/// </summary>
internal sealed partial class DocumentationFile
{
    private readonly Dictionary<string, XElement> _members;

    private DocumentationFile(Dictionary<string, XElement> members) => this._members = members;

    /// <summary>
    ///     Loads the documentation file that sits beside an assembly under the same name.
    /// </summary>
    /// <param name="assembly">
    ///     The assembly whose members are documented.
    /// </param>
    /// <returns>
    ///     The members of the file, keyed by the identifiers the compiler assigns them.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     No documentation file sits beside <paramref name="assembly"/>.
    /// </exception>
    internal static DocumentationFile Load(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        string path = Path.ChangeExtension(assembly.Location, ".xml");

        if (!File.Exists(path))
            throw new InvalidOperationException($"No documentation file was found at {path}.");

        var members = new Dictionary<string, XElement>(StringComparer.Ordinal);

        foreach (XElement member in XDocument.Load(path).Descendants("member"))
        {
            if (member.Attribute("name") is { Value: string name })
                members[name] = member;
        }

        return new DocumentationFile(members);
    }

    /// <summary>
    ///     Reads the summary of a property as plain text.
    /// </summary>
    /// <param name="property">
    ///     The documented property.
    /// </param>
    /// <returns>
    ///     The summary with every run of whitespace collapsed to one space. <c>c</c> keeps its text, a
    ///     <c>see langword</c> its keyword, and a <c>see cref</c> the simple name of the member it names.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     <paramref name="property"/> has no summary, or its summary holds another element.
    /// </exception>
    internal string Summary(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        XElement summary = this.Member(property)?.Element("summary")
            ?? throw new InvalidOperationException($"{NameOf(property)} has no <summary>.");

        var text = new StringBuilder();

        foreach (XNode node in summary.Nodes())
            text.Append(Flatten(node, property));

        return Whitespace().Replace(text.ToString(), " ").Trim();
    }

    /// <summary>
    ///     Reads the examples of a property as written.
    /// </summary>
    /// <param name="property">
    ///     The documented property.
    /// </param>
    /// <returns>
    ///     The trimmed text of each <c>example</c>, in declaration order, or an empty list when the property
    ///     declares none.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     An example of <paramref name="property"/> holds an element.
    /// </exception>
    internal IReadOnlyList<string> Examples(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        IEnumerable<XElement> examples = this.Member(property)?.Elements("example") ?? [];

        return [.. examples.Select(example => example.HasElements
            ? throw new InvalidOperationException($"An <example> of {NameOf(property)} holds markup.")
            : example.Value.Trim())];
    }

    #region Helpers

    private XElement? Member(PropertyInfo property) =>
        this._members.GetValueOrDefault($"P:{property.DeclaringType?.FullName}.{property.Name}");

    private static string Flatten(XNode node, PropertyInfo property) => node switch
    {
        XText text => text.Value,
        XElement { Name.LocalName: "c" } code => code.Value,
        XElement { Name.LocalName: "see" } see when see.Attribute("langword") is { } langword => langword.Value,
        XElement { Name.LocalName: "see" } see when see.Attribute("cref") is { } cref => SimpleName(cref.Value),
        XElement element => throw new InvalidOperationException($"The <summary> of {NameOf(property)} holds <{element.Name.LocalName}>, which has no plain text."),
        _ => string.Empty,
    };

    // A cref arrives resolved, such as P:Gelyn.Model.Options.SiteOptions.BaseUrl.
    private static string SimpleName(string cref) => cref[(cref.LastIndexOf('.') + 1)..];

    private static string NameOf(PropertyInfo property) => $"{property.DeclaringType?.Name}.{property.Name}";

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    #endregion
}
