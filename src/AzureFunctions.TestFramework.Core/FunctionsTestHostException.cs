namespace AzureFunctions.TestFramework.Core;

/// <summary>
/// Exception thrown when there is an error in the Functions test host.
/// </summary>
public class FunctionsTestHostException : Exception
{
    public FunctionsTestHostException()
    {
    }

    public FunctionsTestHostException(string message) : base(message)
    {
    }

    public FunctionsTestHostException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}
