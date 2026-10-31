namespace Gelyn.Core.Internals;

/// <summary>
///     The formats a date is written in.
/// </summary>
internal static class DateFormat
{
    /// <summary>
    ///     The human-readable form of a date, as in <c>Jan 1, 2026</c>.
    /// </summary>
    internal const string Display = "MMM d, yyyy";

    /// <summary>
    ///     The ISO 8601 calendar date, as in <c>2026-01-01</c>.
    /// </summary>
    internal const string Machine = "yyyy-MM-dd";

    /// <summary>
    ///     The form a date takes in a listing, as in <c>2026-01-01</c>. It currently matches
    ///     <see cref="Machine" /> and is stated apart from it, so that a listing can be given its own form
    ///     without the <c>datetime</c> attribute following it.
    /// </summary>
    internal const string Listing = "yyyy-MM-dd";

    /// <summary>
    ///     The RFC 3339 instant a feed carries, as in <c>2026-01-01T00:00:00Z</c>.
    /// </summary>
    internal const string Instant = "yyyy-MM-dd'T'HH:mm:ss'Z'";
}
