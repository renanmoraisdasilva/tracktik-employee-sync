namespace EmployeeSync.Application.Abstractions;

public interface ITrackTikTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);

    void Invalidate();
}
