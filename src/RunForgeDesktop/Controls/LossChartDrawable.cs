namespace RunForgeDesktop.Controls;

/// <summary>
/// Single metric point for the loss chart.
/// </summary>
public record MetricPoint(int Step, int Epoch, float Loss);

/// <summary>
/// GraphicsView drawable that renders a loss curve.
/// One file. One chart. Live motion.
/// </summary>
public class LossChartDrawable : IDrawable
{
    public IReadOnlyList<MetricPoint> Points { get; set; } = Array.Empty<MetricPoint>();

    // Colors
    private readonly Color _lineColor = Color.FromArgb("#6366F1"); // Indigo
    private readonly Color _gridColor = Color.FromArgb("#374151"); // Gray-700
    private readonly Color _textColor = Color.FromArgb("#9CA3AF"); // Gray-400

    public void Draw(ICanvas canvas, RectF rect)
    {
        // Padding for axis labels
        const float leftPad = 50;
        const float bottomPad = 24;
        const float topPad = 16;
        const float rightPad = 16;

        var chartRect = new RectF(
            rect.Left + leftPad,
            rect.Top + topPad,
            rect.Width - leftPad - rightPad,
            rect.Height - topPad - bottomPad);

        // Draw background
        canvas.FillColor = Color.FromArgb("#0F172A"); // Slate-900
        canvas.FillRectangle(rect);

        // Draw grid lines
        DrawGrid(canvas, chartRect);

        // Draw axis labels
        DrawAxisLabels(canvas, chartRect);

        if (Points.Count < 2)
        {
            // Show placeholder text
            canvas.FontColor = _textColor;
            canvas.FontSize = 14;
            canvas.DrawString("Waiting for metrics...",
                rect.Center.X, rect.Center.Y,
                HorizontalAlignment.Center);
            return;
        }

        // Calculate bounds
        float minLoss = Points.Min(p => p.Loss);
        float maxLoss = Points.Max(p => p.Loss);

        // Add 10% padding to y-axis
        float range = maxLoss - minLoss;
        if (range < 0.001f) range = 0.1f;
        minLoss -= range * 0.05f;
        maxLoss += range * 0.05f;

        float xStep = chartRect.Width / (Points.Count - 1);

        // Draw the loss curve
        canvas.StrokeColor = _lineColor;
        canvas.StrokeSize = 2;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        var path = new PathF();

        for (int i = 0; i < Points.Count; i++)
        {
            float x = chartRect.Left + i * xStep;
            float y = Map(Points[i].Loss, minLoss, maxLoss, chartRect.Bottom, chartRect.Top);

            if (i == 0)
                path.MoveTo(x, y);
            else
                path.LineTo(x, y);
        }

        canvas.DrawPath(path);

        // Draw current value indicator (last point)
        if (Points.Count > 0)
        {
            var last = Points[^1];
            float lastX = chartRect.Right;
            float lastY = Map(last.Loss, minLoss, maxLoss, chartRect.Bottom, chartRect.Top);

            // Dot at current position
            canvas.FillColor = _lineColor;
            canvas.FillCircle(lastX, lastY, 4);

            // Current loss label
            canvas.FontColor = Colors.White;
            canvas.FontSize = 12;
            canvas.DrawString($"{last.Loss:F4}",
                lastX - 8, lastY - 16,
                HorizontalAlignment.Right);
        }

        // Draw y-axis scale labels
        DrawYAxisLabels(canvas, chartRect, minLoss, maxLoss);
    }

    private void DrawGrid(ICanvas canvas, RectF rect)
    {
        canvas.StrokeColor = _gridColor;
        canvas.StrokeSize = 0.5f;
        canvas.StrokeDashPattern = new float[] { 4, 4 };

        // Horizontal grid lines (4 lines)
        for (int i = 0; i <= 4; i++)
        {
            float y = rect.Top + (rect.Height * i / 4);
            canvas.DrawLine(rect.Left, y, rect.Right, y);
        }

        canvas.StrokeDashPattern = null;
    }

    private void DrawAxisLabels(ICanvas canvas, RectF rect)
    {
        canvas.FontColor = _textColor;
        canvas.FontSize = 10;

        // X-axis label
        canvas.DrawString("Step",
            rect.Center.X, rect.Bottom + 16,
            HorizontalAlignment.Center);

        // Y-axis label (rotated would be ideal but simplified here)
        canvas.DrawString("Loss",
            rect.Left - 40, rect.Center.Y,
            HorizontalAlignment.Left);
    }

    private void DrawYAxisLabels(ICanvas canvas, RectF rect, float minLoss, float maxLoss)
    {
        canvas.FontColor = _textColor;
        canvas.FontSize = 9;

        // Top, middle, bottom values
        canvas.DrawString($"{maxLoss:F2}", rect.Left - 8, rect.Top, HorizontalAlignment.Right);
        canvas.DrawString($"{(maxLoss + minLoss) / 2:F2}", rect.Left - 8, rect.Center.Y, HorizontalAlignment.Right);
        canvas.DrawString($"{minLoss:F2}", rect.Left - 8, rect.Bottom, HorizontalAlignment.Right);
    }

    private static float Map(float v, float inMin, float inMax, float outMin, float outMax)
    {
        if (Math.Abs(inMax - inMin) < 0.0001f) return outMax;
        return outMin + (v - inMin) * (outMax - outMin) / (inMax - inMin);
    }
}
