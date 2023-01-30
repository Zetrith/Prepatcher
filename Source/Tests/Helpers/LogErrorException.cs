namespace Tests;

public class LogErrorException : Exception
{
    public LogErrorException(string message) : base(message)
    {
    }
}
