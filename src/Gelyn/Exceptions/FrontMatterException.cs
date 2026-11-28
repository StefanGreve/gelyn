using System.Globalization;

namespace Gelyn.Exceptions;

/// <summary>
///     The exception that is thrown when a front matter block cannot be read.
/// </summary>
/// <remarks>
///     The line and the reason are kept apart from the message, because the parser that throws does not know
///     which file the block belongs to. The caller that does names all three.
/// </remarks>
internal sealed class FrontMatterException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FrontMatterException"/> class.
    /// </summary>
    public FrontMatterException()
        : this("The front matter is malformed.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FrontMatterException"/> class with a message.
    /// </summary>
    /// <param name="message">
    ///     What is wrong with the block, as a sentence.
    /// </param>
    public FrontMatterException(string message)
        : base(message)
    {
        this.Reason = message;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FrontMatterException"/> class with a message and its
    ///     cause.
    /// </summary>
    /// <param name="message">
    ///     What is wrong with the block, as a sentence.
    /// </param>
    /// <param name="innerException">
    ///     The exception that caused the failure.
    /// </param>
    public FrontMatterException(string message, Exception innerException)
        : base(message, innerException)
    {
        this.Reason = message;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FrontMatterException"/> class with the line at fault and
    ///     the reason.
    /// </summary>
    /// <param name="line">
    ///     The one-based line of the block the failure starts on.
    /// </param>
    /// <param name="reason">
    ///     What is wrong with the line, as a clause that completes "because", without a period.
    /// </param>
    public FrontMatterException(int line, string reason)
        : base(string.Create(CultureInfo.InvariantCulture, $"The front matter is malformed on line {line}, because {reason}."))
    {
        this.Line = line;
        this.Reason = reason;
    }

    /// <summary>
    ///     The one-based line of the block the failure starts on, or zero when no line is known.
    /// </summary>
    public int Line { get; }

    /// <summary>
    ///     What is wrong with the line, as a clause that completes "because", or the message when no line is
    ///     known.
    /// </summary>
    public string Reason { get; }
}
