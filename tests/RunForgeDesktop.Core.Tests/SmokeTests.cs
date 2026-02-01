using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Tests;

/// <summary>
/// Basic smoke tests to verify the test infrastructure and core types.
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
        // Verify the Core project reference works via model type
        var assemblyName = typeof(RunIndexEntry).Assembly.GetName().Name;
        Assert.Equal("RunForgeDesktop.Core", assemblyName);
    }

    [Fact]
    public void ModelTypes_AreAccessible()
    {
        // Verify all model types are accessible
        Assert.NotNull(typeof(RunIndexEntry));
        Assert.NotNull(typeof(RunSummary));
        Assert.NotNull(typeof(RunRequest));
        Assert.NotNull(typeof(RunResult));
        Assert.NotNull(typeof(TrainingMetrics));
        Assert.NotNull(typeof(MetricsV1));
        Assert.NotNull(typeof(FeatureImportanceV1));
        Assert.NotNull(typeof(LinearCoefficientsV1));
        Assert.NotNull(typeof(InterpretabilityIndexV1));
        Assert.NotNull(typeof(ArtifactEntry));
    }
}
