using Gelyn.Core.Internals;
using Gelyn.Model;

namespace Gelyn.Extensions;

/// <summary>
///     Provides extension methods for <see cref="ContentKind"/>.
/// </summary>
internal static class ContentKindExtensions
{
    /// <summary>
    ///     Defines extensions for the kind a file is classified as.
    /// </summary>
    extension(ContentKind)
    {
        /// <summary>
        ///     Classifies a file by the extension of its name, without regard to case.
        /// </summary>
        /// <param name="name">
        ///     The name of the file.
        /// </param>
        /// <returns>
        ///     The kind the extension stands for, or <see cref="ContentKind.MiscFile"/> for any other extension
        ///     and for a name without one.
        /// </returns>
        public static ContentKind FromFileName(ReadOnlySpan<char> name) =>
            Path.GetExtension(name) switch
            {
                var extension when extension.Equals(FileExtensions.Markdown, StringComparison.OrdinalIgnoreCase) => ContentKind.Page,
                var extension when extension.Equals(FileExtensions.Css, StringComparison.OrdinalIgnoreCase) => ContentKind.Stylesheet,
                var extension when extension.Equals(FileExtensions.JavaScript, StringComparison.OrdinalIgnoreCase) => ContentKind.Script,
                var extension when extension.Equals(FileExtensions.Icon, StringComparison.OrdinalIgnoreCase) => ContentKind.Favicon,
                _ => ContentKind.MiscFile,
            };
    }
}
