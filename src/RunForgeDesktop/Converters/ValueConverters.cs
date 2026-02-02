using System.Globalization;

namespace RunForgeDesktop.Converters;

/// <summary>
/// Converts a boolean to its inverse.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }
}

/// <summary>
/// Converts a string to boolean (true if not null/empty).
/// </summary>
public class StringToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an int to inverse boolean (true if zero).
/// </summary>
public class IntToInverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            return i == 0;
        }
        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a success boolean to a color.
/// </summary>
public class SuccessToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool success)
        {
            return success ? Colors.Green : Colors.Red;
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an enum to its integer index.
/// </summary>
public class EnumToIntConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Enum e)
        {
            return System.Convert.ToInt32(e);
        }
        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i && parameter is Type enumType)
        {
            return Enum.ToObject(enumType, i);
        }
        return value;
    }
}

/// <summary>
/// Converts an object to boolean (true if not null).
/// </summary>
public class ObjectToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an object to inverse boolean (true if null).
/// </summary>
public class ObjectToInverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a percentage (0-100) to a width value for bar charts.
/// Default max width is 150.
/// </summary>
public class PercentToWidthConverter : IValueConverter
{
    public double MaxWidth { get; set; } = 150;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            return (percent / 100.0) * MaxWidth;
        }
        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean (positive/negative) to a color.
/// True = green (positive), False = red (negative).
/// </summary>
public class PositiveToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPositive)
        {
            return isPositive
                ? Color.FromArgb("#4CAF50") // Green for positive
                : Color.FromArgb("#F44336"); // Red for negative
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an ArtifactAvailabilityStatus to a color.
/// Present = green, NotAvailable = amber, Unsupported = gray, Corrupt = red.
/// </summary>
public class ArtifactStatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RunForgeDesktop.Core.Models.ArtifactAvailabilityStatus status)
        {
            return status switch
            {
                RunForgeDesktop.Core.Models.ArtifactAvailabilityStatus.Present => Color.FromArgb("#4CAF50"), // Green
                RunForgeDesktop.Core.Models.ArtifactAvailabilityStatus.NotAvailable => Color.FromArgb("#FFA726"), // Amber
                RunForgeDesktop.Core.Models.ArtifactAvailabilityStatus.Unsupported => Color.FromArgb("#9E9E9E"), // Gray
                RunForgeDesktop.Core.Models.ArtifactAvailabilityStatus.Corrupt => Color.FromArgb("#F44336"), // Red
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an ArtifactAvailabilityStatus to text color.
/// Present = green, NotAvailable = amber, Unsupported = gray, Corrupt = red.
/// </summary>
public class ArtifactStatusToTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RunForgeDesktop.Core.Models.ArtifactAvailabilityStatus status)
        {
            return status switch
            {
                RunForgeDesktop.Core.Models.ArtifactAvailabilityStatus.Present => Color.FromArgb("#2E7D32"), // Dark green
                RunForgeDesktop.Core.Models.ArtifactAvailabilityStatus.NotAvailable => Color.FromArgb("#E65100"), // Dark amber
                RunForgeDesktop.Core.Models.ArtifactAvailabilityStatus.Unsupported => Color.FromArgb("#616161"), // Dark gray
                RunForgeDesktop.Core.Models.ArtifactAvailabilityStatus.Corrupt => Color.FromArgb("#C62828"), // Dark red
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a LogStatus enum to a color.
/// Receiving = green, Stale = amber, Completed = blue, Failed = red, NoLogs = gray.
/// </summary>
public class LogStatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RunForgeDesktop.Core.Services.LogStatus status)
        {
            return status switch
            {
                RunForgeDesktop.Core.Services.LogStatus.Receiving => Color.FromArgb("#4CAF50"), // Green
                RunForgeDesktop.Core.Services.LogStatus.Stale => Color.FromArgb("#FFA726"), // Amber
                RunForgeDesktop.Core.Services.LogStatus.Completed => Color.FromArgb("#2196F3"), // Blue
                RunForgeDesktop.Core.Services.LogStatus.Failed => Color.FromArgb("#F44336"), // Red
                RunForgeDesktop.Core.Services.LogStatus.NoLogs => Color.FromArgb("#9E9E9E"), // Gray
                _ => Colors.Gray
            };
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a milestone reached status to color.
/// Reached = green, Not reached = gray.
/// </summary>
public class MilestoneToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isReached)
        {
            return isReached
                ? Color.FromArgb("#4CAF50") // Green
                : Color.FromArgb("#E0E0E0"); // Light gray
        }
        return Color.FromArgb("#E0E0E0");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a milestone active status to font weight.
/// Active = Bold, Not active = Normal.
/// </summary>
public class MilestoneToFontConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? FontAttributes.Bold : FontAttributes.None;
        }
        return FontAttributes.None;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean to a color.
/// True = green, False = red.
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b
                ? Color.FromArgb("#4CAF50") // Green
                : Color.FromArgb("#F44336"); // Red
        }
        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a string to boolean (true if not null/empty).
/// Alias for StringToBoolConverter with clearer name.
/// </summary>
public class StringNotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a string to boolean based on equality with parameter.
/// Returns true if value equals parameter (case-insensitive).
/// </summary>
public class StringEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && parameter is string target)
        {
            return string.Equals(str, target, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a run status string to a color for the Visual Activity System.
/// "succeeded" = gray (completed), "failed" = red, "running" = blue, "queued" = amber.
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLowerInvariant() switch
            {
                "succeeded" => Color.FromArgb("#6B7280"), // Gray - completed successfully
                "failed" => Color.FromArgb("#EF4444"), // Red - failed
                "running" => Color.FromArgb("#3B82F6"), // Blue - actively running
                "queued" => Color.FromArgb("#F59E0B"), // Amber - waiting in queue
                "stalled" => Color.FromArgb("#F97316"), // Orange - running but no activity
                "cancelled" => Color.FromArgb("#F59E0B"), // Amber - cancelled
                _ => Color.FromArgb("#6B7280") // Gray - unknown/default
            };
        }
        return Color.FromArgb("#6B7280");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a metric severity string to a color.
/// "improved" = green, "degraded" = red, "unchanged" = gray, "unknown" = gray.
/// </summary>
public class SeverityToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string severity)
        {
            return severity.ToLowerInvariant() switch
            {
                "improved" => Color.FromArgb("#4CAF50"), // Green
                "degraded" => Color.FromArgb("#F44336"), // Red
                "unchanged" => Color.FromArgb("#9E9E9E"), // Gray
                _ => Color.FromArgb("#9E9E9E") // Gray
            };
        }
        return Color.FromArgb("#9E9E9E");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a metric severity string to a text color.
/// "improved" = dark green, "degraded" = dark red, "unchanged" = gray.
/// </summary>
public class SeverityToTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string severity)
        {
            return severity.ToLowerInvariant() switch
            {
                "improved" => Color.FromArgb("#2E7D32"), // Dark green
                "degraded" => Color.FromArgb("#C62828"), // Dark red
                "unchanged" => Color.FromArgb("#757575"), // Medium gray
                _ => Color.FromArgb("#757575") // Medium gray
            };
        }
        return Color.FromArgb("#757575");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a metric severity string to an icon/emoji.
/// "improved" = ↑, "degraded" = ↓, "unchanged" = ─.
/// </summary>
public class SeverityToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string severity)
        {
            return severity.ToLowerInvariant() switch
            {
                "improved" => "↑",
                "degraded" => "↓",
                "unchanged" => "─",
                _ => "?"
            };
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean to FontAttributes.
/// True = Bold, False = None.
/// </summary>
public class BoolToFontAttributeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? FontAttributes.Bold : FontAttributes.None;
        }
        return FontAttributes.None;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an int to boolean (true if greater than zero).
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            return i > 0;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// ============================================
// Visual Activity System Converters (v0.5.0)
// ============================================

/// <summary>
/// Converts a slot filled boolean to a color.
/// True = Blue (active), False = Gray (empty).
/// Works with both light and dark themes.
/// </summary>
public class SlotFilledToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isFilled)
        {
            return isFilled
                ? Color.FromArgb("#3B82F6") // Blue - active/filled
                : Color.FromArgb("#D1D5DB"); // Light gray - empty
        }
        return Color.FromArgb("#D1D5DB");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a RunDisplayState enum to a color.
/// Queued = Amber, Running = Blue, Completed = Gray, Failed = Red, Stalled = Orange.
/// </summary>
public class RunDisplayStateToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RunForgeDesktop.Core.Models.RunDisplayState state)
        {
            return state switch
            {
                RunForgeDesktop.Core.Models.RunDisplayState.Queued => Color.FromArgb("#FFA726"),    // Amber
                RunForgeDesktop.Core.Models.RunDisplayState.Running => Color.FromArgb("#2196F3"),   // Blue
                RunForgeDesktop.Core.Models.RunDisplayState.Completed => Color.FromArgb("#9E9E9E"), // Gray
                RunForgeDesktop.Core.Models.RunDisplayState.Failed => Color.FromArgb("#F44336"),    // Red
                RunForgeDesktop.Core.Models.RunDisplayState.Stalled => Color.FromArgb("#FF9800"),   // Orange
                _ => Color.FromArgb("#9E9E9E") // Gray for Unknown
            };
        }
        return Color.FromArgb("#9E9E9E");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an ActivitySystemState enum to a color.
/// Idle = Gray, Busy = Blue, Stalled = Amber, Error = Red.
/// </summary>
public class ActivitySystemStateToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is RunForgeDesktop.Core.Services.ActivitySystemState state)
        {
            return state switch
            {
                RunForgeDesktop.Core.Services.ActivitySystemState.Idle => Color.FromArgb("#9E9E9E"),    // Gray
                RunForgeDesktop.Core.Services.ActivitySystemState.Busy => Color.FromArgb("#2196F3"),    // Blue
                RunForgeDesktop.Core.Services.ActivitySystemState.Stalled => Color.FromArgb("#FFA726"), // Amber
                RunForgeDesktop.Core.Services.ActivitySystemState.Error => Color.FromArgb("#F44336"),   // Red
                _ => Color.FromArgb("#9E9E9E")
            };
        }
        return Color.FromArgb("#9E9E9E");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool to one of two strings. Parameter format: "TrueValue|FalseValue"
/// </summary>
public class BoolToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string paramString)
        {
            var parts = paramString.Split('|');
            if (parts.Length == 2)
            {
                return boolValue ? parts[0] : parts[1];
            }
        }
        return value?.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a Color to a semi-transparent background version for pill backgrounds.
/// Used in Activity Strip for system state pills.
/// </summary>
public class ColorToBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Just return the color as-is for the pill background
        if (value is Color color)
        {
            return color;
        }
        return Color.FromArgb("#6B7280"); // Gray fallback
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a loss value (0-3) to a chart bar height (max 100px).
/// Used for simple bar chart visualization.
/// </summary>
public class LossToHeightConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double loss)
        {
            // Clamp loss between 0 and 3, then scale to max 100px
            var clamped = Math.Max(0, Math.Min(3, loss));
            return (clamped / 3.0) * 100;
        }
        return 10.0; // Minimum height
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
