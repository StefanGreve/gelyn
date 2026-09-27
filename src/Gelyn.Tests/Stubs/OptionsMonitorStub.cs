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
    /// <inheritdoc/>
    public SiteOptions CurrentValue { get; } = new();

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
