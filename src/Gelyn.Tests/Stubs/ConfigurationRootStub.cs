using System;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Gelyn.Tests.Stubs;

/// <summary>
///     A configuration root that holds nothing and counts the times it was reloaded, optionally failing the
///     reload the way a configuration file whose new values are invalid does.
/// </summary>
/// <remarks>
///     The consumer under test reads <see cref="Reload"/> and the indexer only, so the members that make up
///     the rest of the interface answer with nothing, because a stand-in would have to be kept meaningful.
/// </remarks>
internal sealed class ConfigurationRootStub : IConfigurationRoot
{
    private readonly Exception? _failure;
    private readonly ConfigurationReloadToken _token = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationRootStub"/> class.
    /// </summary>
    /// <param name="failure">
    ///     The exception every reload throws, or <see langword="null"/> for a reload that succeeds.
    /// </param>
    public ConfigurationRootStub(Exception? failure = null) => this._failure = failure;

    /// <summary>
    ///     The number of times the configuration was asked to reload, the failed attempts included.
    /// </summary>
    public int Reloads { get; private set; }

    /// <inheritdoc/>
    public IEnumerable<IConfigurationProvider> Providers => [];

    /// <inheritdoc/>
    public string? this[string key]
    {
        get => null;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public IEnumerable<IConfigurationSection> GetChildren() => [];

    /// <inheritdoc/>
    public IChangeToken GetReloadToken() => this._token;

    /// <summary>
    ///     Not supported.
    /// </summary>
    /// <param name="key">
    ///     Unused.
    /// </param>
    /// <returns>
    ///     Never returns.
    /// </returns>
    /// <exception cref="NotSupportedException">
    ///     Always thrown. A section of an empty configuration would be an empty section, which no test can
    ///     tell apart from one that was never asked for.
    /// </exception>
    public IConfigurationSection GetSection(string key) => throw new NotSupportedException();

    /// <summary>
    ///     Records the reload and fails it when the stub was constructed to.
    /// </summary>
    /// <exception cref="Exception">
    ///     The exception the stub was constructed with.
    /// </exception>
    public void Reload()
    {
        this.Reloads++;

        if (this._failure is not null)
            throw this._failure;
    }
}
