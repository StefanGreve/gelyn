using ArchUnitNET.Fluent;
using ArchUnitNET.TUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the accessibility of the types the tool declares.
/// </summary>
public class VisibilityTests : ArchBase
{
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

    #region Model Tests

    /// <summary>
    ///     Verifies that every type in the model namespace is public.
    /// </summary>
    /// <remarks>
    ///     <c>ResideInNamespace</c> matches a namespace by its full name rather than as a prefix, so the
    ///     options namespace has to be named in its own right to be covered at all.
    /// </remarks>
    [Test]
    public void Visibility_Test_EveryTypeInModelIsPublic()
    {
        // Arrange
        IArchRule rule = Types()
            .That()
            .ResideInNamespace(ModelNamespace)
            .Or()
            .ResideInNamespace(OptionsNamespace)
            .Should()
            .BePublic();

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion

    #region Services Tests

    /// <summary>
    ///     Verifies that every type in the services namespace is public.
    /// </summary>
    [Test]
    public void Visibility_Test_EveryTypeInServicesIsPublic()
    {
        // Arrange
        IArchRule rule = Types()
            .That()
            .ResideInNamespace(ServicesNamespace)
            .Should()
            .BePublic();

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion
}
