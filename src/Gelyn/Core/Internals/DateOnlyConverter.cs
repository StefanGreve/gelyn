using System;

using SharpYaml.Serialization;

namespace Gelyn.Core.Internals;

/// <summary>
///     Converts the calendar dates a front matter block declares to and from <see cref="DateOnly"/>.
/// </summary>
/// <remarks>
///     The source generator rejects a <see cref="DateOnly"/> member outright with <c>SHARPYAML002</c> rather than
///     falling back to reflection, so the type binds only through a converter the member names. Reading a value
///     the configured culture cannot parse yields <see langword="null"/>, which leaves the rest of the block
///     readable instead of failing it over one malformed date.
/// </remarks>
internal sealed class DateOnlyConverter : YamlConverter<DateOnly?>
{
    private const string Format = "yyyy-MM-dd";

    /// <inheritdoc/>
    public override DateOnly? Read(YamlReader reader)
    {
        string? value = reader.GetScalarValue();

        // The scalar is consumed here rather than by the caller, which would otherwise read the value again as
        // the next key and fault the mapping.
        reader.Read();

        return DateOnly.TryParse(value, Tool.Culture, out DateOnly date)
            ? date
            : null;
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
