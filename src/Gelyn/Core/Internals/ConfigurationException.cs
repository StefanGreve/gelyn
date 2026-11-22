namespace Gelyn.Core.Internals;

/// <summary>
///     The exception that is thrown when the settings of a run are rejected.
/// </summary>
/// <remarks>
///     One instance carries every failure the load found, so that a run reports them all at once instead of
///     stopping at the first.
/// </remarks>
internal sealed class ConfigurationException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationException"/> class.
    /// </summary>
    public ConfigurationException()
        : this("The configuration was rejected.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationException"/> class with one failure.
    /// </summary>
    /// <param name="message">
    ///     The failure, a sentence that names the setting at fault.
    /// </param>
    public ConfigurationException(string message)
        : this([message])
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationException"/> class with one failure and its
    ///     cause.
    /// </summary>
    /// <param name="message">
    ///     The failure, a sentence that names the setting at fault.
    /// </param>
    /// <param name="innerException">
    ///     The exception that caused the rejection.
    /// </param>
    public ConfigurationException(string message, Exception innerException)
        : this([message], innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationException"/> class.
    /// </summary>
    /// <param name="failures">
    ///     The failures the load found, each a sentence that names the setting at fault.
    /// </param>
    /// <param name="innerException">
    ///     The exception that caused the rejection, or <see langword="null"/> when a rule rejected a value.
    /// </param>
    public ConfigurationException(IReadOnlyList<string> failures, Exception? innerException = null)
        : base(string.Join(" ", failures), innerException)
    {
        this.Failures = failures;
    }

    /// <summary>
    ///     The failures the load found, each a sentence that names the setting at fault.
    /// </summary>
    public IReadOnlyList<string> Failures { get; }
}
