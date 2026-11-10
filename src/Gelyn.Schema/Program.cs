using System;
using System.IO;

using Gelyn.Model.Options;

namespace Gelyn.Schema;

/// <summary>
///     Writes <c>gelyn.schema.json</c>, or checks that the file on disk is current.
/// </summary>
internal static class Program
{
    private const string VerifyFlag = "--verify";

    /// <summary>
    ///     Generates the schema and compares it with the file at the given path.
    /// </summary>
    /// <param name="args">
    ///     The path of the schema file, optionally followed by <c>--verify</c>, which reports a difference instead
    ///     of writing the file.
    /// </param>
    /// <returns>
    ///     <c>0</c> when the file is current or has been written, and <c>1</c> when it is out of date under
    ///     <c>--verify</c> or the schema could not be generated.
    /// </returns>
    internal static int Main(string[] args)
    {
        if (args is not [string path, .. string[] flags] || flags is not ([] or [VerifyFlag]))
        {
            Console.Error.WriteLine($"Usage: Gelyn.Schema <path> [{VerifyFlag}]");
            return 1;
        }

        byte[] generated;

        try
        {
            generated = ConfigurationSchema.Generate(DocumentationFile.Load(typeof(SiteOptions).Assembly));
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine($"{path}: {exception.Message}");
            return 1;
        }

        // Written only on a difference, so a build that changes nothing leaves the timestamp alone.
        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(generated))
            return 0;

        if (flags is [VerifyFlag])
        {
            Console.Error.WriteLine($"{path}: out of date. Build the solution and commit the regenerated file.");
            return 1;
        }

        File.WriteAllBytes(path, generated);

        return 0;
    }
}
