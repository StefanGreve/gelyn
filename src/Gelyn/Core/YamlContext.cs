using Gelyn.Model;

using YamlDotNet.Serialization;

namespace Gelyn.Core;

/// <summary>
///     The statically generated serialization context for the types read out of a front matter block.
/// </summary>
/// <remarks>
///     The source generator emits the other half of this class as <c>public</c>, so the accessibility declared here
///     cannot be narrowed without a <c>CS0262</c>.
/// </remarks>
[YamlStaticContext]
[YamlSerializable(typeof(FrontMatter))]
public partial class YamlContext : StaticContext;
