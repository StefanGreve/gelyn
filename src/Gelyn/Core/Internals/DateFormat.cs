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
}
