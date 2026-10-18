using System.Text.Json.Serialization;

using Gelyn.Model;

using SharpYaml.Serialization;

namespace Gelyn.Core;

/// <summary>
///     The statically generated serialization context for the types read out of a front matter block.
/// </summary>
[YamlSerializable(typeof(FrontMatter))]
[YamlSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class YamlContext : YamlSerializerContext;
