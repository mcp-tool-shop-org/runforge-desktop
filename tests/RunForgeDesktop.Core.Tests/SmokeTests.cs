namespace RunForgeDesktop.Core.Tests;

/// <summary>
/// Basic smoke tests to verify the test infrastructure works.
/// Real tests will be added as domain models and services are implemented.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void TestInfrastructure_Works()
    {
        // Verify xUnit is properly configured
        Assert.True(true);
    }

    [Fact]
    public void CoreAssembly_CanBeReferenced()
    {
        // Verify the Core project reference works
        var assemblyName = typeof(RunForgeDesktop.Core.Placeholder).Assembly.GetName().Name;
        Assert.Equal("RunForgeDesktop.Core", assemblyName);
    }
}
