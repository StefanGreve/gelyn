using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the symbols that <c>src/Gelyn/BannedSymbols.txt</c> bans.
/// </summary>
/// <remarks>
///     The banned API analyzer bans each overload on its own and ignores an entry that matches no symbol, so a
///     misspelled entry, or an overload that a later version of .NET or Testably adds, would leave a gap without a
///     warning.
/// </remarks>
[Category(Categories.Architecture)]
public class BannedSymbolsTests
{
    private const string FileName = "BannedSymbols.txt";

    // Banned with all of their members, so that the disk is reached through IFileSystem alone.
    private static readonly Type[] BannedTypes = [typeof(Directory), typeof(DirectoryInfo), typeof(File), typeof(FileInfo)];

    // Banned in every overload, each replaced by an extension in Gelyn.Extensions.
    private static readonly (Type Type, string Name)[] BannedMethods =
    [
        (typeof(IFile), nameof(IFile.Copy)),
        (typeof(IFile), nameof(IFile.ReadAllText)),
        (typeof(IDirectory), nameof(IDirectory.EnumerateDirectories)),
        (typeof(IDirectory), nameof(IDirectory.EnumerateFiles)),
        (typeof(IDirectory), nameof(IDirectory.GetDirectories)),
        (typeof(IDirectory), nameof(IDirectory.GetFiles)),
        (typeof(IDirectoryInfo), nameof(IDirectoryInfo.EnumerateDirectories)),
        (typeof(IDirectoryInfo), nameof(IDirectoryInfo.EnumerateFileSystemInfos)),
        (typeof(IDirectoryInfo), nameof(IDirectoryInfo.EnumerateFiles)),
        (typeof(IDirectoryInfo), nameof(IDirectoryInfo.GetDirectories)),
        (typeof(IDirectoryInfo), nameof(IDirectoryInfo.GetFileSystemInfos)),
        (typeof(IDirectoryInfo), nameof(IDirectoryInfo.GetFiles)),
    ];

    #region BannedSymbols Tests

    /// <summary>
    ///     Verifies that the list names every banned type and every overload of every banned method, and nothing
    ///     else, so that a misspelled entry or a new overload fails here instead of leaving a gap.
    /// </summary>
    [Test]
    public async Task BannedSymbols_Test_List_NamesEveryOverloadAndNothingElse()
    {
        // Arrange
        string[] expected =
        [
            .. BannedTypes.Select(static type => $"T:{type.FullName}"),
            .. BannedMethods.SelectMany(static banned => banned.Type.GetMethods()
                .Where(method => method.Name == banned.Name)
                .Select(method => ToDocumentationId(banned.Type, method))),
        ];

        string[] listed =
        [
            .. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, FileName))
                .Select(static line => line.Trim())
                .Where(static line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal))
                .Select(static line => line.Split(';')[0]),
        ];

        // Act
        IEnumerable<string> missing = expected.Except(listed, StringComparer.Ordinal);
        IEnumerable<string> unmatched = listed.Except(expected, StringComparer.Ordinal);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(missing).IsEmpty();
            await Assert.That(unmatched).IsEmpty();
        }
    }

    #endregion // BannedSymbols Tests

    #region Helpers

    // The parameter types in parentheses, and none for a method without parameters. The banned methods take no
    // generic or by-reference parameters, whose IDs would need more than the full type names.
    private static string ToDocumentationId(Type type, MethodInfo method)
    {
        string name = $"M:{type.FullName}.{method.Name}";
        ParameterInfo[] parameters = method.GetParameters();

        return parameters.Length == 0 ? name : $"{name}({string.Join(',', parameters.Select(static parameter => parameter.ParameterType.FullName))})";
    }

    #endregion
}
