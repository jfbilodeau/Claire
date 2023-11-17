namespace Claire;

// Base class for all exceptions in Claire
public class ClaireException : ApplicationException
{   
    public ClaireException(string message) : base(message)
    {
    }

    public ClaireException(string message, Exception innerException) : base(message, innerException)
    {
    }
}