using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Common.Exceptions;
using EmployeeSync.Application.Contracts;
using EmployeeSync.Domain.Common;
using EmployeeSync.Domain.Employees;
using EmployeeSync.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EmployeeSync.Application.Commands.SubmitEmployee;

public sealed class SubmitEmployeeHandler : ICommandHandler<SubmitEmployeeCommand, Result<SubmitEmployeeResult>>
{
    private const int StatusSaveAttempts = 3;

    private readonly IProviderAdapterFactory _adapterFactory;
    private readonly IEmployeeRepository _repository;
    private readonly ITrackTikClient _trackTikClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SubmitEmployeeHandler> _logger;

    public SubmitEmployeeHandler(
        IProviderAdapterFactory adapterFactory,
        IEmployeeRepository repository,
        ITrackTikClient trackTikClient,
        IEventPublisher eventPublisher,
        TimeProvider timeProvider,
        ILogger<SubmitEmployeeHandler> logger)
    {
        _adapterFactory = adapterFactory;
        _repository = repository;
        _trackTikClient = trackTikClient;
        _eventPublisher = eventPublisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<SubmitEmployeeResult>> HandleAsync(
        SubmitEmployeeCommand command,
        CancellationToken cancellationToken)
    {
        var adapter = _adapterFactory.Get(command.Provider);
        if (adapter is null)
        {
            return Result<SubmitEmployeeResult>.Failure(
                Error.NotFound($"Unknown provider '{command.Provider}'."));
        }

        var mappedResult = await adapter.MapAsync(command.Payload, cancellationToken);
        if (mappedResult.IsFailure)
        {
            return Result<SubmitEmployeeResult>.Failure(mappedResult.Error!);
        }

        var mapped = mappedResult.Value;
        var identity = new EmployeeIdentity(adapter.Name, mapped.ProviderEmployeeId);
        var now = _timeProvider.GetUtcNow();

        Employee? employee;
        var created = false;

        try
        {
            employee = await _repository.FindAsync(identity, cancellationToken);

            if (employee is null)
            {
                employee = Employee.Create(identity, mapped, now);
                created = await _repository.TryAddAsync(employee, cancellationToken);

                if (!created)
                {
                    employee = await _repository.FindAsync(identity, cancellationToken);
                    if (employee is null)
                    {
                        return Result<SubmitEmployeeResult>.Failure(
                            Error.Unexpected("The employee identity was not found after a concurrent insert."));
                    }

                    employee.UpdateFrom(mapped, now);
                    employee.MarkPending(now);
                    await _repository.SaveChangesAsync(cancellationToken);
                }
            }
            else
            {
                employee.UpdateFrom(mapped, now);
                employee.MarkPending(now);
                await _repository.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not persist the pending employee {Provider}/{ProviderEmployeeId}.",
                identity.Provider,
                identity.ProviderEmployeeId);

            return Result<SubmitEmployeeResult>.Failure(
                Error.Unexpected("The employee could not be stored."));
        }

        employee.BeginAttempt(_timeProvider.GetUtcNow());

        try
        {
            if (employee.ExistsInTrackTik)
            {
                await _trackTikClient.UpdateAsync(employee.ToTrackTikEmployee(), cancellationToken);
            }
            else
            {
                await _trackTikClient.CreateAsync(employee.ToTrackTikEmployee(), cancellationToken);
            }
        }
        catch (TrackTikException exception)
        {
            _logger.LogWarning(
                "TrackTik rejected {Provider}/{ProviderEmployeeId}: {Reason}",
                employee.Provider,
                employee.ProviderEmployeeId,
                exception.SafeMessage);

            employee.MarkFailed(_timeProvider.GetUtcNow(), exception.SafeMessage);

            if (!await SaveWithRetryAsync(cancellationToken))
            {
                return Result<SubmitEmployeeResult>.Failure(
                    Error.Unexpected("The sync failed and the failure status could not be saved."));
            }

            return Result<SubmitEmployeeResult>.Failure(
                Error.SyncFailed(
                    "The employee was stored with status 'failed' but could not be synchronized.",
                    new SyncFailureInfo(
                        employee.SyncStatus.ToApiString(),
                        employee.Attempts,
                        employee.LastError,
                        employee.LastAttemptAt)));
        }

        employee.MarkSynced(_timeProvider.GetUtcNow());

        if (!await SaveWithRetryAsync(cancellationToken))
        {
            return Result<SubmitEmployeeResult>.Failure(
                Error.Unexpected("The employee was synchronized but the status could not be saved."));
        }

        try
        {
            await _eventPublisher.PublishAsync(
                EmployeeSynced.From(employee, _timeProvider.GetUtcNow()),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "EmployeeSynced publishing failed for {Provider}/{ProviderEmployeeId}.",
                employee.Provider,
                employee.ProviderEmployeeId);
        }

        return Result<SubmitEmployeeResult>.Success(
            new SubmitEmployeeResult(
                EmployeeResponse.From(employee),
                created));
    }

    private async Task<bool> SaveWithRetryAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= StatusSaveAttempts; attempt++)
        {
            try
            {
                await _repository.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (attempt < StatusSaveAttempts)
            {
                _logger.LogWarning(
                    exception,
                    "Status save attempt {Attempt} failed; retrying.",
                    attempt);

                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Status save failed after {Attempts} attempts.", StatusSaveAttempts);
                return false;
            }
        }

        return false;
    }
}
