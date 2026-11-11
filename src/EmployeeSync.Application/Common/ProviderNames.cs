namespace EmployeeSync.Application.Common;

public static class ProviderNames
{
    public const string Provider1 = "provider1";
    public const string Provider2 = "provider2";

    public static string Normalize(string provider) => provider.Trim().ToLowerInvariant();
}
