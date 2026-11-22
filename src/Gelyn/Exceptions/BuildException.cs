namespace Gelyn.Exceptions;

/// <summary>
///     The exception that is thrown when the site breaks a rule of the build.
/// </summary>
/// <remarks>
///     The rules are that no two entries publish one path, that a front matter block declares only stylesheets
///     and scripts the scan located, and that the output directory is not a link. A build that fails this way
///     exits with code 1, as one that fails on the file system does, where a <see cref="ConfigurationException"/>
///     exits with 2.
/// </remarks>
internal sealed class BuildException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BuildException"/> class.
    /// </summary>
    public BuildException()
        : this("The build was rejected.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="BuildException"/> class with a message.
    /// </summary>
    /// <param name="message">
    ///     The rule the site breaks, a sentence that names the entries at fault.
    /// </param>
    public BuildException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="BuildException"/> class with a message and its cause.
    /// </summary>
    /// <param name="message">
    ///     The rule the site breaks, a sentence that names the entries at fault.
    /// </param>
    /// <param name="innerException">
    ///     The exception that caused the rejection.
    /// </param>
    public BuildException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
