namespace Ticketing.Application.Exceptions;

public class StatusChangeForbiddenException : Exception
{
    public StatusChangeForbiddenException(string message) : base(message) { }
}
