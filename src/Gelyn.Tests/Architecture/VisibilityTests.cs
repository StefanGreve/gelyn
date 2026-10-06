using ArchUnitNET.Fluent;
using ArchUnitNET.TUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the accessibility of the types the tool declares.
/// </summary>
public class VisibilityTests : ArchBase
{
    private const string InternalsNamespace = "Gelyn.Internals";

    #region Internals Tests

    /// <summary>
    ///     Verifies that every type in the internals namespace is internal.
    /// </summary>
    [Test]
    public void Visibility_Test_EveryTypeInInternalsIsInternal()
    {
        // Arrange
        IArchRule rule = Types()
            .That()
            .ResideInNamespace(InternalsNamespace)
            .Should()
            .BeInternal();

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion
}
