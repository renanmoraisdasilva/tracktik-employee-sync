namespace EmployeeSync.Application.Abstractions;

public interface IProviderAdapterFactory
{
    IProviderAdapter? Get(string provider);
}
