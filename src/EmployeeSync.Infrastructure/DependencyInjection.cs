using EmployeeSync.Application.Abstractions;
using EmployeeSync.Infrastructure.Events;
using EmployeeSync.Infrastructure.Persistence;
using EmployeeSync.Infrastructure.TrackTik;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EmployeeSync.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Database:UseSqlite"))
        {
            var connectionString = configuration.GetConnectionString("EmployeeSync")
                ?? throw new InvalidOperationException("ConnectionStrings:EmployeeSync is required.");

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
        }

        services.Configure<TrackTikOptions>(configuration.GetSection(TrackTikOptions.SectionName));
        services.Configure<EventOptions>(configuration.GetSection(EventOptions.SectionName));
        services.Configure<NatsOptions>(configuration.GetSection(NatsOptions.SectionName));

        services.AddHttpClient("TrackTikToken", (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TrackTikOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddHttpClient<ITrackTikClient, TrackTikClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TrackTikOptions>>().Value;
            client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.AddSingleton<ITrackTikTokenProvider, TrackTikTokenProvider>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmployeeReportRepository, EmployeeReportRepository>();

        var eventMode = configuration[$"{EventOptions.SectionName}:Mode"] ?? "InMemory";
        if (eventMode.Equals("Nats", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEventPublisher, NatsEventPublisher>();
        }
        else
        {
            services.AddSingleton<IEventPublisher, InMemoryEventPublisher>();
        }

        return services;
    }
}
