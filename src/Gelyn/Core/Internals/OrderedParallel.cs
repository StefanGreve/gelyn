using System.Runtime.ExceptionServices;

namespace Gelyn.Core.Internals;

/// <summary>
///     Processes independent items on several threads, with the results and the failure that a sequential loop
///     over the same items would produce.
/// </summary>
internal static class OrderedParallel
{
    /// <summary>
    ///     Projects every item into a result, running up to one projection per processor at a time.
    /// </summary>
    /// <typeparam name="TSource">
    ///     The type of the items.
    /// </typeparam>
    /// <typeparam name="TResult">
    ///     The type of the results.
    /// </typeparam>
    /// <param name="source">
    ///     The items to project.
    /// </param>
    /// <param name="selector">
    ///     The projection, which is called from several threads at once.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation. Projections that have already started run to completion.
    /// </param>
    /// <returns>
    ///     The result of each item, at the index of that item.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    ///     <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    internal static TResult[] Select<TSource, TResult>(
        IReadOnlyList<TSource> source,
        Func<TSource, TResult> selector,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);

        return For(source.Count, index => selector(source[index]), cancellationToken);
    }

    /// <summary>
    ///     Projects every index below a count into a result, running up to one projection per processor at a time.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The type of the results.
    /// </typeparam>
    /// <param name="count">
    ///     The number of indices, which start at zero.
    /// </param>
    /// <param name="selector">
    ///     The projection, which is called from several threads at once.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation. Projections that have already started run to completion.
    /// </param>
    /// <returns>
    ///     The result of each index, at that index.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         <see cref="Parallel"/> wraps a failure in an <see cref="AggregateException"/>. This method rethrows
    ///         it with its original type and stack trace instead, so that a caller that filters on the type sees
    ///         no difference from a sequential loop.
    ///     </para>
    ///     <para>
    ///         When several indices fail, the failure of the lowest wins. A failing index breaks the loop, which
    ///         then starts no index above it but still runs every index below it. The winning index therefore
    ///         does not depend on how the threads were scheduled. Indices above it that were already running
    ///         complete.
    ///     </para>
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    ///     <paramref name="cancellationToken"/> was canceled.
    /// </exception>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = Justifications.ByDesign)]
    internal static TResult[] For<TResult>(int count, Func<int, TResult> selector, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentNullException.ThrowIfNull(selector);

        var results = new TResult[count];
        var failures = new ExceptionDispatchInfo?[count];

        ParallelOptions options = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Environment.ProcessorCount,
        };

        ParallelLoopResult loop = Parallel.For(0, count, options, (index, state) =>
        {
            try
            {
                results[index] = selector(index);
            }
            catch (Exception exception)
            {
                failures[index] = ExceptionDispatchInfo.Capture(exception);
                state.Break();
            }
        });

        // Only a failing iteration breaks the loop, so the lowest breaking index always holds a capture.
        if (loop.LowestBreakIteration is long failed && failures[failed] is ExceptionDispatchInfo failure)
            failure.Throw();

        return results;
    }
}
