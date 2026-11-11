namespace EmployeeSync.Application.Common.Exceptions;

public sealed class TrackTikException : Exception
{
    public TrackTikException(string safeMessage, Exception? innerException = null)
        : base(safeMessage, innerException)
    {
        SafeMessage = safeMessage;
    }

    public string SafeMessage { get; }
}
