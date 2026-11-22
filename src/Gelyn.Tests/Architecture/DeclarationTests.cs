using ArchUnitNET.Fluent;
using ArchUnitNET.TUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the way the tool's types are declared.
/// </summary>
[Category(Categories.Architecture)]
public class DeclarationTests : ArchBase
{
    #region Declaration Tests

    /// <summary>
    ///     Verifies that every class the tool declares is sealed.
    /// </summary>
    /// <remarks>
    ///     A sealed class lets the AOT compiler dispatch calls directly, which
    ///     <c>OptimizationPreference=Speed</c> benefits from. A static class is emitted as abstract and sealed,
    ///     so it passes as declared, where an abstract class cannot.
    /// </remarks>
    [Test]
    public void Declaration_Test_EveryClassIsSealed()
    {
        // Arrange
        IArchRule rule = Classes()
            .That()
            .ResideInNamespaceOrBelow(RootNamespace)
            .And()
            .AreNotNested()
            .Should()
            .BeSealed();

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion // Declaration Tests
}
