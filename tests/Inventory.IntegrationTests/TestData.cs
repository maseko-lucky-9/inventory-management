namespace Inventory.IntegrationTests;

public static class TestData
{
    public static string Unique(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N")[..12];
}
