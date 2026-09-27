using System;

using Gelyn.Model.Options;

using Microsoft.Extensions.Options;

namespace Gelyn.Tests.Stubs;

/// <summary>
///     An options monitor that reports a fixed <see cref="SiteOptions"/> instance and refuses to hand out
///     change notifications.
/// </summary>
internal sealed class OptionsMonitorStub : IOptionsMonitor<SiteOptions>
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OptionsMonitorStub"/> class.
    /// </summary>
    /// <param name="options">
    ///     The options to report, or <see langword="null"/> to report the defaults.
    /// </param>
    public OptionsMonitorStub(SiteOptions? options = null) => this.CurrentValue = options ?? new SiteOptions();

    /// <inheritdoc/>
    public SiteOptions CurrentValue { get; }

    /// <inheritdoc/>
    public SiteOptions Get(string? name) => this.CurrentValue;

    /// <summary>
    ///     Not supported.
    /// </summary>
    /// <param name="listener">
    ///     Unused.
    /// </param>
    /// <returns>
    ///     Never returns.
    /// </returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown. The services under test read <see cref="CurrentValue"/> only, so a no-op
    ///     subscription would hide a change in what they depend on.
    /// </exception>
    public IDisposable OnChange(Action<SiteOptions, string?> listener) => throw new NotSupportedException();
}
