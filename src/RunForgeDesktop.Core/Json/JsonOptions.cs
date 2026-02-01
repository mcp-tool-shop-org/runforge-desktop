using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunForgeDesktop.Core.Json;

/// <summary>
/// Centralized JSON serialization options for RunForge Desktop.
/// Ensures deterministic, consistent parsing across all artifacts.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    /// Default options for reading RunForge artifacts.
    /// - Case-insensitive property matching (defensive)
    /// - Allow trailing commas (lenient)
    /// - Allow comments (lenient)
    /// - Number handling for potential edge cases
    /// </summary>
    public static JsonSerializerOptions Default { get; } = CreateDefault();

    /// <summary>
    /// Strict options for validation scenarios.
    /// - Case-sensitive property matching
    /// - No trailing commas
    /// - No comments
    /// </summary>
    public static JsonSerializerOptions Strict { get; } = CreateStrict();

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        return options;
    }

    private static JsonSerializerOptions CreateStrict()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            NumberHandling = JsonNumberHandling.Strict,
        };

        return options;
    }
}
