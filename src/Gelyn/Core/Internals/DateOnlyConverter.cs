using System.Globalization;

using Gelyn.Core.Yaml;

using SharpYaml.Serialization;

namespace Gelyn.Core.Internals;

/// <summary>
///     Converts the calendar dates a front matter block declares to and from <see cref="DateOnly"/>.
/// </summary>
/// <remarks>
///     The source generator rejects a <see cref="DateOnly"/> member outright with <c>SHARPYAML002</c> and never
///     falls back to reflection, so the type binds only through a converter the member names. A value that is
///     not a scalar written as <c>yyyy-MM-dd</c> is rejected before it is consumed, which lets
///     <see cref="FrontMatterParser"/> skip it and report it, so the rest of the block stays readable instead of
///     failing over one malformed date.
/// </remarks>
internal sealed class DateOnlyConverter : YamlConverter<DateOnly?>
{
    private const string Format = "yyyy-MM-dd";

    /// <inheritdoc/>
    public override DateOnly? Read(YamlReader reader)
    {
        // A key without a value is a key the author left blank, not a malformed date.
        if (YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        string? value = reader.GetScalarValue();

        if (!DateOnly.TryParseExact(value, Format, Tool.Culture, DateTimeStyles.None, out DateOnly date))
            throw reader.CreateException($"'{value}' is not a date written as {Format}");

        reader.Read();
        return date;
    }

    /// <inheritdoc/>
    public override void Write(YamlWriter writer, DateOnly? value)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteScalar(value.Value.ToString(Format, Tool.Culture));
    }
}
