using System.Text.Json;
using EmployeeSync.Application.Abstractions;
using EmployeeSync.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;

namespace EmployeeSync.Infrastructure.Events;

public sealed class NatsEventPublisher : IEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly NatsOptions _options;
    private readonly ILogger<NatsEventPublisher> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private NatsConnection? _connection;

    public NatsEventPublisher(
        IOptions<NatsOptions> options,
        ILogger<NatsEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(EmployeeSynced domainEvent, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            domainEvent,
            SerializerOptions);

        await connection.PublishAsync(
            _options.Subject,
            payload,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Published EmployeeSynced {EventId} to {Subject}.",
            domainEvent.EventId,
            _options.Subject);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }

    private async Task<NatsConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);

        try
        {
            if (_connection is null)
            {
                var connection = new NatsConnection(new NatsOpts { Url = _options.Url });
                await connection.ConnectAsync();
                _connection = connection;
            }

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}
