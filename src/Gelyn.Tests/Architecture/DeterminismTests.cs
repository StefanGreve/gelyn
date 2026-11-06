using System;
using System.Linq;

using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent;
using ArchUnitNET.TUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the inputs the rendered output is allowed to depend on.
/// </summary>
public class DeterminismTests : ArchBase
{
    // Matched by suffix against the full name of each call target. A file's last write time is listed beside the
    // clock because a checkout sets it, so a page derived from it differs between two clones of the same commit.
    private static readonly string[] TimeSources =
    [
        "System.DateTime::get_Now()",
        "System.DateTime::get_UtcNow()",
        "System.DateTime::get_Today()",
        "System.DateTimeOffset::get_Now()",
        "System.DateTimeOffset::get_UtcNow()",
        "System.TimeProvider::GetUtcNow()",
        "System.TimeProvider::GetLocalNow()",
        "System.Diagnostics.Stopwatch::GetTimestamp()",
        "::get_LastWriteTime()",
        "::get_LastWriteTimeUtc()",
    ];

    #region Determinism Tests

    /// <summary>
    ///     Verifies that no type taking part in rendering reads the current time.
    /// </summary>
    /// <remarks>
    ///     A page's bytes depend on its own sources and the tool version alone, which
    ///     <c>SiteBuilderTests.BuildAsync_Test_SameTree_ProducesIdenticalBytes</c> asserts by building one tree
    ///     several times. Those builds fall on the same day, so a date read off the clock passes that test, and
    ///     this rule is what catches it. Only direct calls are seen, which leaves <c>Gelyn.Extensions</c> as the
    ///     one route around it; the services are no route, because the components may not depend on them.
    /// </remarks>
    [Test]
    [Arguments(ComponentsNamespace)]
    [Arguments(CoreNamespace)]
    [Arguments(ModelNamespace)]
    public void Determinism_Test_RenderingNamespace_DoesNotReadTheTime(string namespaceName)
    {
        // Arrange
        // A custom condition, because NotCallAny matches only members of the loaded assembly and so never one
        // of the framework's, which would leave the rule passing whatever the code calls.
        IArchRule rule = Types()
            .That()
            .ResideInNamespaceOrBelow(namespaceName)
            .Should()
            .FollowCustomCondition(
                static type => !type.Dependencies
                    .OfType<MethodCallDependency>()
                    .Any(static dependency => IsTimeSource(dependency.TargetMember.FullName)),
                "not read the time",
                "reads the time");

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion // Determinism Tests

    #region Helpers

    private static bool IsTimeSource(string fullName) =>
        TimeSources.Any(source => fullName.EndsWith(source, StringComparison.Ordinal));

    #endregion
}
