using System;
using System.Globalization;

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Gelyn.Internals;

/// <summary>
///     Converts the calendar dates a front matter block declares to and from <see cref="DateOnly"/>.
/// </summary>
/// <remarks>
///     A statically generated context resolves no scalar it was not told about, so without this converter a date
///     reaches the generated property setter as a <see cref="string"/> and fails to cast.
/// </remarks>
internal sealed class DateOnlyConverter : IYamlTypeConverter
{
    private const string Format = "yyyy-MM-dd";

    /// <inheritdoc/>
    public bool Accepts(Type type) => type == typeof(DateOnly) || type == typeof(DateOnly?);

    /// <inheritdoc/>
    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer) =>
        DateOnly.TryParse(parser.Consume<Scalar>().Value, CultureInfo.InvariantCulture, out DateOnly date)
            ? date
            : null;

    /// <inheritdoc/>
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
        emitter.Emit(new Scalar(((DateOnly?)value)?.ToString(Format, CultureInfo.InvariantCulture) ?? string.Empty));
}
