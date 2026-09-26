namespace Gelyn.Internals;

/// <summary>
///     Options that override a configured setting for the duration of one run.
/// </summary>
/// <remarks>
///     Shared so that the command declaring an option and the configuration layer promoting it cannot drift
///     apart. The names carry no leading dashes, because that is the form the command line configuration
///     provider reports them under.
/// </remarks>
internal static class OverrideOptions
{
    /// <summary>
    ///     Overrides the site title.
    /// </summary>
    internal const string Title = "title";

    /// <summary>
    ///     Overrides the content directory.
    /// </summary>
    internal const string Content = "content";

    /// <summary>
    ///     Overrides the output directory.
    /// </summary>
    internal const string Output = "output";
}
