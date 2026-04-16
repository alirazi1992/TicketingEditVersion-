namespace Ticketing.Application.Exceptions;

public class TicketValidationException : Exception
{
    public TicketValidationException(string message) : base(message) { }
    public TicketValidationException(string message, Exception innerException) : base(message, innerException) { }
    public TicketValidationException(string code, string message, object? field = null, object? value = null) : base(message)
    {
        Code = code;
        Field = field?.ToString();
        Value = value?.ToString();
    }
    public string? Code { get; }
    public string? Field { get; }
    public string? Value { get; }
}
