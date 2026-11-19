using System;
using System.Linq;

using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent;
using ArchUnitNET.TUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the order in which the build lists a directory.
/// </summary>
[Category(Categories.Architecture)]
public class ListingTests : ArchBase
{
    // Matched as an infix of the full name of each call target, so that every overload and every declaring type
    // is caught, the wrappers of System.IO.Abstractions included.
    private static readonly string[] Listings =
    [
        "::EnumerateFileSystemInfos(",
        "::EnumerateFiles(",
        "::EnumerateDirectories(",
        "::GetFileSystemInfos(",
        "::GetFiles(",
        "::GetDirectories(",
    ];

    #region Listing Tests

    /// <summary>
    ///     Verifies that no type outside <c>Gelyn.Extensions</c> lists a directory, so that every walk takes the
    ///     one order the scan, the prune and their reports share.
    /// </summary>
    /// <remarks>
    ///     The scan decides the order of the pages and the copies in the report, and the prune the order of the
    ///     removed lines. A walk sorting on its own, or not at all, would make them differ between file systems,
    ///     since each lists its entries in an order of its own. Only direct calls are seen.
    /// </remarks>
    [Test]
    public void Listing_Test_OnlyTheExtension_ListsADirectory()
    {
        // Arrange
        IArchRule rule = Types()
            .That()
            .DoNotResideInNamespace(ExtensionsNamespace)
            .Should()
            .FollowCustomCondition(
                static type => !type.Dependencies
                    .OfType<MethodCallDependency>()
                    .Any(static dependency => IsListing(dependency.TargetMember.FullName)),
                "list no directory",
                "lists a directory");

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion // Listing Tests

    #region Helpers

    private static bool IsListing(string fullName) =>
        Listings.Any(listing => fullName.Contains(listing, StringComparison.Ordinal));

    #endregion
}
