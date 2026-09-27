using System;

namespace Gelyn.Tests.Stubs;

/// <summary>
///     A time provider frozen at a fixed instant, so that generated output does not depend on when the test
///     happens to run.
/// </summary>
internal sealed class TimeProviderStub : TimeProvider
{
    /// <summary>
    ///     The instant reported when none is supplied.
    /// </summary>
    public static readonly DateTimeOffset DefaultNow = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly DateTimeOffset _now;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TimeProviderStub"/> class.
    /// </summary>
    /// <param name="now">
    ///     The instant to report, or <see langword="null"/> to report <see cref="DefaultNow"/>.
    /// </param>
    public TimeProviderStub(DateTimeOffset? now = null) => this._now = now ?? DefaultNow;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => this._now;
}
