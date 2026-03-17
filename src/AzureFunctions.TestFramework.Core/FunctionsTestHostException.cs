namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Exception thrown when there is an error in the Functions test host.
/// </summary>
public class FunctionsTestHostException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionsTestHostException"/> class.
    /// </summary>
    public FunctionsTestHostException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionsTestHostException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public FunctionsTestHostException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionsTestHostException"/> class
    /// with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public FunctionsTestHostException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
