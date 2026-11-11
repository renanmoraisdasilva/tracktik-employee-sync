using System.Text.Json;
using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Commands.SubmitEmployee;
using EmployeeSync.Application.Common.Exceptions;
using EmployeeSync.Application.Contracts;
using EmployeeSync.Domain.Common;
using EmployeeSync.Domain.Employees;
using EmployeeSync.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace EmployeeSync.UnitTests;

public sealed class SubmitEmployeeHandlerTests
{
    private static readonly MappedEmployee Mapped = new(
        "P1_001",
        "Alice",
        "Johnson",
        "alice.johnson@provider1.com",
        "+1-555-0101",
        "Security Officer",
        "Security Operations",
        new DateOnly(2024, 1, 15),
        EmployeeStatus.Active);

    private static readonly JsonElement Payload = JsonDocument.Parse("{}").RootElement;

    [Fact]
    public async Task First_success_persists_pending_then_synced_and_publishes_one_event()
    {
        var harness = new Harness();

        var result = await harness.Handler.HandleAsync(
            new SubmitEmployeeCommand("provider1", Payload),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeTrue();
        result.Value.Employee.SyncStatus.ShouldBe("synced");
        result.Value.Employee.Attempts.ShouldBe(1);
        result.Value.Employee.LastError.ShouldBeNull();

        harness.Captured.ShouldNotBeNull();
        harness.Captured!.SyncStatus.ShouldBe(SyncStatus.Synced);
        harness.Captured.ExistsInTrackTik.ShouldBeTrue();

        await harness.TrackTik.Received(1).CreateAsync(
            Arg.Is<TrackTikEmployee>(employee => employee.EmployeeId == "provider1-P1_001"),
            Arg.Any<CancellationToken>());

        await harness.TrackTik.DidNotReceive().UpdateAsync(
            Arg.Any<TrackTikEmployee>(),
            Arg.Any<CancellationToken>());

        await harness.Publisher.Received(1).PublishAsync(
            Arg.Any<EmployeeSynced>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task First_success_commits_pending_before_forward_and_event_after_commit()
    {
        var harness = new Harness();
        var commits = new List<(SyncStatus Status, int Attempts)>();
        (SyncStatus Status, int Attempts)? observedByTrackTik = null;
        var eventSawSyncedCommit = false;

        harness.Repository.TryAddAsync(
                Arg.Any<Employee>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var employee = call.Arg<Employee>();
                harness.Capture(employee);
                commits.Add((employee.SyncStatus, employee.Attempts));
                return Task.FromResult(true);
            });

        harness.Repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var employee = harness.Captured!;
                commits.Add((employee.SyncStatus, employee.Attempts));
                return Task.CompletedTask;
            });

        harness.TrackTik.CreateAsync(
                Arg.Any<TrackTikEmployee>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var employee = harness.Captured!;
                observedByTrackTik = (employee.SyncStatus, employee.Attempts);
                return Task.CompletedTask;
            });

        harness.Publisher.PublishAsync(
                Arg.Any<EmployeeSynced>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                eventSawSyncedCommit = commits.Count == 2
                    && commits[^1].Status == SyncStatus.Synced;
                return Task.CompletedTask;
            });

        var result = await harness.Handler.HandleAsync(
            new SubmitEmployeeCommand("provider1", Payload),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        commits.Count.ShouldBe(2);
        commits[0].Status.ShouldBe(SyncStatus.Pending);
        commits[0].Attempts.ShouldBe(0);
        commits[1].Status.ShouldBe(SyncStatus.Synced);
        commits[1].Attempts.ShouldBe(1);

        observedByTrackTik.ShouldNotBeNull();
        observedByTrackTik!.Value.Status.ShouldBe(SyncStatus.Pending);
        observedByTrackTik.Value.Attempts.ShouldBe(1);

        eventSawSyncedCommit.ShouldBeTrue();
    }

    [Fact]
    public async Task TrackTik_failure_keeps_the_row_failed_and_publishes_no_event()
    {
        var harness = new Harness();
        harness.TrackTik.CreateAsync(
                Arg.Any<TrackTikEmployee>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new TrackTikException("TrackTik returned 500.")));

        var result = await harness.Handler.HandleAsync(
            new SubmitEmployeeCommand("provider1", Payload),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.SyncFailed);
        result.Error.SyncFailure.ShouldNotBeNull();
        result.Error.SyncFailure!.SyncStatus.ShouldBe("failed");
        result.Error.SyncFailure.Attempts.ShouldBe(1);
        result.Error.SyncFailure.LastError.ShouldBe("TrackTik returned 500.");

        harness.Captured.ShouldNotBeNull();
        harness.Captured!.SyncStatus.ShouldBe(SyncStatus.Failed);
        harness.Captured.Attempts.ShouldBe(1);
        harness.Captured.ExistsInTrackTik.ShouldBeFalse();

        await harness.Publisher.DidNotReceive().PublishAsync(
            Arg.Any<EmployeeSynced>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resubmission_uses_put_and_keeps_one_row()
    {
        var harness = new Harness();
        var existing = Employee.Create(
            new EmployeeIdentity("provider1", "P1_001"),
            Mapped,
            harness.Clock.GetUtcNow());
        existing.BeginAttempt(harness.Clock.GetUtcNow());
        existing.MarkSynced(harness.Clock.GetUtcNow());

        harness.Repository.FindAsync(
                Arg.Any<EmployeeIdentity>(),
                Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await harness.Handler.HandleAsync(
            new SubmitEmployeeCommand("provider1", Payload),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeFalse();
        result.Value.Employee.Attempts.ShouldBe(2);
        result.Value.Employee.SyncStatus.ShouldBe("synced");

        await harness.TrackTik.Received(1).UpdateAsync(
            Arg.Any<TrackTikEmployee>(),
            Arg.Any<CancellationToken>());
        await harness.TrackTik.DidNotReceive().CreateAsync(
            Arg.Any<TrackTikEmployee>(),
            Arg.Any<CancellationToken>());
        await harness.Repository.DidNotReceive().TryAddAsync(
            Arg.Any<Employee>(),
            Arg.Any<CancellationToken>());
        await harness.Repository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await harness.Publisher.Received(1).PublishAsync(
            Arg.Any<EmployeeSynced>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_resubmission_after_create_still_uses_put()
    {
        var harness = new Harness();
        var existing = Employee.Create(
            new EmployeeIdentity("provider1", "P1_001"),
            Mapped,
            harness.Clock.GetUtcNow());
        existing.BeginAttempt(harness.Clock.GetUtcNow());
        existing.MarkSynced(harness.Clock.GetUtcNow());
        existing.BeginAttempt(harness.Clock.GetUtcNow());
        existing.MarkFailed(harness.Clock.GetUtcNow(), "previous failure");

        harness.Repository.FindAsync(
                Arg.Any<EmployeeIdentity>(),
                Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await harness.Handler.HandleAsync(
            new SubmitEmployeeCommand("provider1", Payload),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Employee.Attempts.ShouldBe(3);
        await harness.TrackTik.Received(1).UpdateAsync(
            Arg.Any<TrackTikEmployee>(),
            Arg.Any<CancellationToken>());
        await harness.TrackTik.DidNotReceive().CreateAsync(
            Arg.Any<TrackTikEmployee>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Concurrent_insert_race_reloads_and_updates_the_existing_row()
    {
        var harness = new Harness();
        var existing = Employee.Create(
            new EmployeeIdentity("provider1", "P1_001"),
            Mapped,
            harness.Clock.GetUtcNow());

        harness.Repository.FindAsync(
                Arg.Any<EmployeeIdentity>(),
                Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<Employee?>(null),
                Task.FromResult<Employee?>(existing));
        harness.Repository.TryAddAsync(
                Arg.Any<Employee>(),
                Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await harness.Handler.HandleAsync(
            new SubmitEmployeeCommand("provider1", Payload),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Created.ShouldBeFalse();
        result.Value.Employee.Attempts.ShouldBe(1);
        result.Value.Employee.SyncStatus.ShouldBe("synced");

        await harness.Repository.Received(1).TryAddAsync(
            Arg.Any<Employee>(),
            Arg.Any<CancellationToken>());
        await harness.Repository.Received(2).SaveChangesAsync(
            Arg.Any<CancellationToken>());
        await harness.TrackTik.Received(1).CreateAsync(
            Arg.Any<TrackTikEmployee>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_resubmission_that_never_created_still_uses_post()
    {
        var harness = new Harness();
        var existing = Employee.Create(
            new EmployeeIdentity("provider1", "P1_001"),
            Mapped,
            harness.Clock.GetUtcNow());
        existing.BeginAttempt(harness.Clock.GetUtcNow());
        existing.MarkFailed(harness.Clock.GetUtcNow(), "previous failure");

        harness.Repository.FindAsync(
                Arg.Any<EmployeeIdentity>(),
                Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await harness.Handler.HandleAsync(
            new SubmitEmployeeCommand("provider1", Payload),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Employee.Attempts.ShouldBe(2);
        await harness.TrackTik.Received(1).CreateAsync(
            Arg.Any<TrackTikEmployee>(),
            Arg.Any<CancellationToken>());
        await harness.TrackTik.DidNotReceive().UpdateAsync(
            Arg.Any<TrackTikEmployee>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Validation_failure_never_touches_the_repository_or_tracktik()
    {
        var harness = new Harness();
        harness.Adapter.MapAsync(
                Arg.Any<JsonElement>(),
                Arg.Any<CancellationToken>())
            .Returns(Result<MappedEmployee>.Failure(Error.Validation("email", "Invalid email.")));

        var result = await harness.Handler.HandleAsync(
            new SubmitEmployeeCommand("provider1", Payload),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);

        await harness.Repository.DidNotReceive().FindAsync(
            Arg.Any<EmployeeIdentity>(),
            Arg.Any<CancellationToken>());
        await harness.TrackTik.DidNotReceive().CreateAsync(
            Arg.Any<TrackTikEmployee>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unknown_provider_returns_not_found_before_any_work()
    {
        var harness = new Harness();
        harness.Factory.Get(Arg.Any<string>()).Returns((IProviderAdapter?)null);

        var result = await harness.Handler.HandleAsync(
            new SubmitEmployeeCommand("provider3", Payload),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);

        await harness.Repository.DidNotReceive().FindAsync(
            Arg.Any<EmployeeIdentity>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Event_publisher_failure_does_not_fail_the_committed_sync()
    {
        var harness = new Harness();
        harness.Publisher.PublishAsync(
                Arg.Any<EmployeeSynced>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("broker unavailable")));

        var result = await harness.Handler.HandleAsync(
            new SubmitEmployeeCommand("provider1", Payload),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Employee.SyncStatus.ShouldBe("synced");
        harness.Captured!.SyncStatus.ShouldBe(SyncStatus.Synced);
    }

    [Fact]
    public async Task Status_save_failure_returns_unexpected_error()
    {
        var harness = new Harness();
        harness.Repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("database unavailable")));

        var result = await harness.Handler.HandleAsync(
            new SubmitEmployeeCommand("provider1", Payload),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Unexpected);
        await harness.Repository.Received(3).SaveChangesAsync(
            Arg.Any<CancellationToken>());
    }

    private sealed class Harness
    {
        public Harness()
        {
            Adapter.Name.Returns("provider1");
            Adapter.MapAsync(Arg.Any<JsonElement>(), Arg.Any<CancellationToken>())
                .Returns(Result<MappedEmployee>.Success(Mapped));
            Factory.Get(Arg.Any<string>()).Returns(Adapter);

            Repository.FindAsync(
                    Arg.Any<EmployeeIdentity>(),
                    Arg.Any<CancellationToken>())
                .Returns((Employee?)null);
            Repository.TryAddAsync(
                    Arg.Do<Employee>(employee => Captured = employee),
                    Arg.Any<CancellationToken>())
                .Returns(true);
            Repository.SaveChangesAsync(Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            TrackTik.CreateAsync(
                    Arg.Any<TrackTikEmployee>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            TrackTik.UpdateAsync(
                    Arg.Any<TrackTikEmployee>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            Publisher.PublishAsync(
                    Arg.Any<EmployeeSynced>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            Handler = new SubmitEmployeeHandler(
                Factory,
                Repository,
                TrackTik,
                Publisher,
                Clock,
                NullLogger<SubmitEmployeeHandler>.Instance);
        }

        public IProviderAdapter Adapter { get; } = Substitute.For<IProviderAdapter>();

        public IProviderAdapterFactory Factory { get; } = Substitute.For<IProviderAdapterFactory>();

        public IEmployeeRepository Repository { get; } = Substitute.For<IEmployeeRepository>();

        public ITrackTikClient TrackTik { get; } = Substitute.For<ITrackTikClient>();

        public IEventPublisher Publisher { get; } = Substitute.For<IEventPublisher>();

        public FakeTimeProvider Clock { get; } = new(
            new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero));

        public SubmitEmployeeHandler Handler { get; }

        public Employee? Captured { get; private set; }

        public void Capture(Employee employee) => Captured = employee;
    }
}
