using System;
using System.Linq;

using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent;
using ArchUnitNET.TUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the file operations the build replaces with its own.
/// </summary>
[Category(Categories.Architecture)]
public class FileContentTests : ArchBase
{
    // Matched as an infix of the full name of each call target. "File::Copy(" names the declaring type, because
    // Array has a Copy of its own.
    private static readonly string[] Replaced =
    [
        "::ReadAllText(",
        "File::Copy(",
    ];

    #region File Content Tests

    /// <summary>
    ///     Verifies that no type outside <c>Gelyn.Extensions</c> calls a file operation the extension replaces, so
    ///     that every read of a source takes the pooled path and no copy carries the attributes of its source.
    /// </summary>
    [Test]
    public void FileContent_Test_OnlyTheExtension_ReadsOrCopiesAFile()
    {
        // Arrange
        IArchRule rule = Types()
            .That()
            .DoNotResideInNamespace(ExtensionsNamespace)
            .Should()
            .FollowCustomCondition(
                static type => !type.Dependencies
                    .OfType<MethodCallDependency>()
                    .Any(static dependency => IsReplaced(dependency.TargetMember.FullName)),
                "call no replaced file operation",
                "calls a replaced file operation");

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion // File Content Tests

    #region Helpers

    private static bool IsReplaced(string fullName) =>
        Replaced.Any(replaced => fullName.Contains(replaced, StringComparison.Ordinal));

    #endregion
}
