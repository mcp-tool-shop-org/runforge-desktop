using System.Text;
using System.Text.Json;
using RunForgeDesktop.Core.Models;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of export service for run data.
/// </summary>
public sealed class ExportService : IExportService
{
    private readonly IInterpretabilityService _interpretabilityService;
    private readonly IRunDetailService _runDetailService;

    public ExportService(
        IInterpretabilityService interpretabilityService,
        IRunDetailService runDetailService)
    {
        _interpretabilityService = interpretabilityService;
        _runDetailService = runDetailService;
    }

    public async Task<ExportResult> ExportFeatureImportanceToCsvAsync(
        string workspacePath,
        string runDir,
        string outputPath)
    {
        try
        {
            // Load the interpretability index first
            var indexResult = await _interpretabilityService.LoadIndexAsync(workspacePath, runDir);
            if (!indexResult.IsSuccess || indexResult.Index is null)
            {
                return ExportResult.Failure(indexResult.ErrorMessage ?? "Failed to load interpretability index");
            }

            var entry = indexResult.Index.GetArtifact("feature_importance.v1");
            if (entry is null || !entry.Available)
            {
                return ExportResult.Failure("Feature importance artifact not available");
            }

            var artifact = await _interpretabilityService.LoadArtifactAsync<FeatureImportanceV1>(
                workspacePath, runDir, entry.Path);

            if (artifact is null)
            {
                return ExportResult.Failure("Failed to load feature importance data");
            }

            var sb = new StringBuilder();
            sb.AppendLine("Rank,Feature,Importance,Importance_Percent");

            var ranked = artifact.Importances
                .OrderByDescending(x => x.Value)
                .Select((kv, i) => new { Rank = i + 1, kv.Key, kv.Value });

            foreach (var item in ranked)
            {
                sb.AppendLine($"{item.Rank},\"{EscapeCsv(item.Key)}\",{item.Value:F8},{item.Value * 100:F4}%");
            }

            await File.WriteAllTextAsync(outputPath, sb.ToString());
            var fileInfo = new FileInfo(outputPath);

            return ExportResult.Success(outputPath, fileInfo.Length);
        }
        catch (Exception ex)
        {
            return ExportResult.Failure($"Export failed: {ex.Message}");
        }
    }

    public async Task<ExportResult> ExportLinearCoefficientsToCsvAsync(
        string workspacePath,
        string runDir,
        string outputPath)
    {
        try
        {
            // Load the interpretability index first
            var indexResult = await _interpretabilityService.LoadIndexAsync(workspacePath, runDir);
            if (!indexResult.IsSuccess || indexResult.Index is null)
            {
                return ExportResult.Failure(indexResult.ErrorMessage ?? "Failed to load interpretability index");
            }

            var entry = indexResult.Index.GetArtifact("linear_coefficients.v1");
            if (entry is null || !entry.Available)
            {
                return ExportResult.Failure("Linear coefficients artifact not available");
            }

            var artifact = await _interpretabilityService.LoadArtifactAsync<LinearCoefficientsV1>(
                workspacePath, runDir, entry.Path);

            if (artifact is null)
            {
                return ExportResult.Failure("Failed to load linear coefficients data");
            }

            var sb = new StringBuilder();
            sb.AppendLine("Class,Feature,Coefficient,Abs_Coefficient");

            foreach (var classEntry in artifact.Coefficients)
            {
                var className = classEntry.Key;
                var ranked = classEntry.Value
                    .OrderByDescending(x => Math.Abs(x.Value));

                foreach (var coeff in ranked)
                {
                    sb.AppendLine($"\"{EscapeCsv(className)}\",\"{EscapeCsv(coeff.Key)}\",{coeff.Value:F8},{Math.Abs(coeff.Value):F8}");
                }
            }

            await File.WriteAllTextAsync(outputPath, sb.ToString());
            var fileInfo = new FileInfo(outputPath);

            return ExportResult.Success(outputPath, fileInfo.Length);
        }
        catch (Exception ex)
        {
            return ExportResult.Failure($"Export failed: {ex.Message}");
        }
    }

    public async Task<ExportResult> ExportMetricsToCsvAsync(
        string workspacePath,
        string runDir,
        string outputPath)
    {
        try
        {
            // Load the interpretability index first
            var indexResult = await _interpretabilityService.LoadIndexAsync(workspacePath, runDir);
            if (!indexResult.IsSuccess || indexResult.Index is null)
            {
                return ExportResult.Failure(indexResult.ErrorMessage ?? "Failed to load interpretability index");
            }

            var entry = indexResult.Index.GetArtifact("metrics.v1");
            if (entry is null || !entry.Available)
            {
                return ExportResult.Failure("Metrics artifact not available");
            }

            var artifact = await _interpretabilityService.LoadArtifactAsync<MetricsV1>(
                workspacePath, runDir, entry.Path);

            if (artifact is null)
            {
                return ExportResult.Failure("Failed to load metrics data");
            }

            var sb = new StringBuilder();
            sb.AppendLine("Category,Metric,Value");

            foreach (var category in artifact.Metrics)
            {
                foreach (var metric in category.Value)
                {
                    sb.AppendLine($"\"{EscapeCsv(category.Key)}\",\"{EscapeCsv(metric.Key)}\",{metric.Value:F8}");
                }
            }

            await File.WriteAllTextAsync(outputPath, sb.ToString());
            var fileInfo = new FileInfo(outputPath);

            return ExportResult.Success(outputPath, fileInfo.Length);
        }
        catch (Exception ex)
        {
            return ExportResult.Failure($"Export failed: {ex.Message}");
        }
    }

    public async Task<ExportResult> ExportRunSummaryToJsonAsync(
        string workspacePath,
        string runDir,
        string outputPath)
    {
        try
        {
            var details = await _runDetailService.LoadRunDetailAsync(workspacePath, runDir);

            var summary = new
            {
                RunId = Path.GetFileName(runDir),
                RunDir = runDir,
                Request = details.Request,
                Result = details.Result,
                Metrics = details.Metrics,
                ExportedAt = DateTime.UtcNow.ToString("o")
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            var json = JsonSerializer.Serialize(summary, options);
            await File.WriteAllTextAsync(outputPath, json);
            var fileInfo = new FileInfo(outputPath);

            return ExportResult.Success(outputPath, fileInfo.Length);
        }
        catch (Exception ex)
        {
            return ExportResult.Failure($"Export failed: {ex.Message}");
        }
    }

    public async Task<ExportResult> CopyArtifactAsync(string sourcePath, string outputPath)
    {
        try
        {
            if (!File.Exists(sourcePath))
            {
                return ExportResult.Failure($"Source file not found: {sourcePath}");
            }

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            await Task.Run(() => File.Copy(sourcePath, outputPath, overwrite: true));
            var fileInfo = new FileInfo(outputPath);

            return ExportResult.Success(outputPath, fileInfo.Length);
        }
        catch (Exception ex)
        {
            return ExportResult.Failure($"Copy failed: {ex.Message}");
        }
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }
}
