namespace Gelyn.Attributes;

/// <summary>
///     Marks a list setting whose items <c>gelyn.schema.json</c> declares unique.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class UniqueItemsAttribute : Attribute;
