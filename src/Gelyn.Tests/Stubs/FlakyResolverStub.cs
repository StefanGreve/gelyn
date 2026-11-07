using System;

using Gelyn.Abstractions;
using Gelyn.Model;

namespace Gelyn.Tests.Stubs;

/// <summary>
///     A resolver that fails its first request and answers every later one through the resolver it wraps,
///     so that a test can tell a server that survived the failure from one that did not.
/// </summary>
internal sealed class FlakyResolverStub : StaticFileResolverContract
{
    private readonly StaticFileResolverContract _inner;
    private readonly Exception _failure;
    private bool _failed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FlakyResolverStub"/> class.
    /// </summary>
    /// <param name="inner">
    ///     The resolver that answers once the failure has been thrown.
    /// </param>
    /// <param name="failure">
    ///     The exception the first request fails with.
    /// </param>
    public FlakyResolverStub(StaticFileResolverContract inner, Exception failure)
    {
        this._inner = inner;
        this._failure = failure;
    }

    /// <inheritdoc/>
    public override ServedFile Resolve(string outputDirectory, string requestPath)
    {
        if (this._failed) return this._inner.Resolve(outputDirectory, requestPath);

        this._failed = true;
        throw this._failure;
    }
}
