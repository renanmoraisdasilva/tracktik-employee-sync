using EmployeeSync.Application.Abstractions;
using EmployeeSync.Application.Common;

namespace EmployeeSync.Application.Providers;

public sealed class ProviderAdapterFactory : IProviderAdapterFactory
{
    private readonly IReadOnlyDictionary<string, IProviderAdapter> _adapters;

    public ProviderAdapterFactory(IEnumerable<IProviderAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IProviderAdapter? Get(string provider) =>
        _adapters.TryGetValue(ProviderNames.Normalize(provider), out var adapter)
            ? adapter
            : null;
}
