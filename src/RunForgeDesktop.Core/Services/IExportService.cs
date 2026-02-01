namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Service for exporting run data to various formats.
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Exports feature importance data to CSV format.
    /// </summary>
    Task<ExportResult> ExportFeatureImportanceToCsvAsync(
        string workspacePath,
        string runDir,
        string outputPath);

    /// <summary>
    /// Exports linear coefficients data to CSV format.
    /// </summary>
    Task<ExportResult> ExportLinearCoefficientsToCsvAsync(
        string workspacePath,
        string runDir,
        string outputPath);

    /// <summary>
    /// Exports metrics data to CSV format.
    /// </summary>
    Task<ExportResult> ExportMetricsToCsvAsync(
        string workspacePath,
        string runDir,
        string outputPath);

    /// <summary>
    /// Exports run summary to JSON format.
    /// </summary>
    Task<ExportResult> ExportRunSummaryToJsonAsync(
        string workspacePath,
        string runDir,
        string outputPath);

    /// <summary>
    /// Copies an artifact file to a new location.
    /// </summary>
    Task<ExportResult> CopyArtifactAsync(
        string sourcePath,
        string outputPath);
}

/// <summary>
/// Result of an export operation.
/// </summary>
public sealed record ExportResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? OutputPath { get; init; }
    public long BytesWritten { get; init; }

    public static ExportResult Success(string outputPath, long bytesWritten) => new()
    {
        IsSuccess = true,
        OutputPath = outputPath,
        BytesWritten = bytesWritten
    };

    public static ExportResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}
