using System.Text;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of live log monitoring.
/// Uses file mtime and byte offset polling for efficient delta reads.
/// </summary>
public sealed class LiveLogService : ILiveLogService
{
    /// <inheritdoc />
    public LogSnapshot GetSnapshot(
        string logFilePath,
        long lastKnownSize,
        string? runStatus,
        TimeSpan staleThreshold)
    {
        // Check if run already completed
        if (runStatus == "succeeded")
        {
            return CreateCompletedSnapshot(logFilePath, LogStatus.Completed);
        }

        if (runStatus == "failed")
        {
            return CreateCompletedSnapshot(logFilePath, LogStatus.Failed);
        }

        // Check if file exists
        if (!File.Exists(logFilePath))
        {
            return new LogSnapshot
            {
                Status = LogStatus.NoLogs,
                TimeSinceLastUpdate = TimeSpan.Zero,
                LinesAddedSinceLastSnapshot = 0,
                TotalLineCount = 0,
                FileSizeBytes = 0,
                LastWriteTimeUtc = DateTime.MinValue
            };
        }

        try
        {
            var fileInfo = new FileInfo(logFilePath);
            var lastWriteUtc = fileInfo.LastWriteTimeUtc;
            var currentSize = fileInfo.Length;
            var timeSinceUpdate = DateTime.UtcNow - lastWriteUtc;

            // Estimate lines added (rough: assume ~80 chars per line average)
            var bytesAdded = Math.Max(0, currentSize - lastKnownSize);
            var estimatedLinesAdded = (int)(bytesAdded / 80);

            // Estimate total lines (same rough calculation)
            var estimatedTotalLines = (long)(currentSize / 80);

            // Determine status based on staleness
            var status = timeSinceUpdate > staleThreshold
                ? LogStatus.Stale
                : LogStatus.Receiving;

            return new LogSnapshot
            {
                Status = status,
                TimeSinceLastUpdate = timeSinceUpdate,
                LinesAddedSinceLastSnapshot = estimatedLinesAdded,
                TotalLineCount = estimatedTotalLines,
                FileSizeBytes = currentSize,
                LastWriteTimeUtc = lastWriteUtc
            };
        }
        catch (IOException)
        {
            // File might be locked or inaccessible
            return new LogSnapshot
            {
                Status = LogStatus.NoLogs,
                TimeSinceLastUpdate = TimeSpan.Zero,
                LinesAddedSinceLastSnapshot = 0,
                TotalLineCount = 0,
                FileSizeBytes = 0,
                LastWriteTimeUtc = DateTime.MinValue
            };
        }
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<string> Lines, long NewByteOffset)> ReadDeltaAsync(
        string logFilePath,
        long fromByteOffset,
        int maxLines = 500)
    {
        if (!File.Exists(logFilePath))
        {
            return (Array.Empty<string>(), 0);
        }

        try
        {
            var fileInfo = new FileInfo(logFilePath);
            var currentSize = fileInfo.Length;

            // No new data
            if (currentSize <= fromByteOffset)
            {
                return (Array.Empty<string>(), fromByteOffset);
            }

            // Read only the new bytes
            var bytesToRead = currentSize - fromByteOffset;

            await using var stream = new FileStream(
                logFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite); // Allow reading while file is being written

            stream.Seek(fromByteOffset, SeekOrigin.Begin);

            var buffer = new byte[bytesToRead];
            var bytesRead = await stream.ReadAsync(buffer, 0, (int)bytesToRead);

            if (bytesRead == 0)
            {
                return (Array.Empty<string>(), fromByteOffset);
            }

            var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var lines = text.Split('\n', StringSplitOptions.None);

            // Handle partial line at the end (keep it for next read)
            var completeLines = new List<string>();
            var newOffset = fromByteOffset + bytesRead;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');

                // Skip the last element if it's empty (means file ended with newline)
                // or if it's incomplete (no newline at end)
                if (i == lines.Length - 1)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        // File ended with newline, all lines complete
                        break;
                    }
                    else if (!text.EndsWith('\n'))
                    {
                        // Incomplete line - back up the offset
                        newOffset -= Encoding.UTF8.GetByteCount(lines[i]);
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(line))
                {
                    completeLines.Add(line);
                }
            }

            // Apply max lines limit (keep the most recent)
            if (completeLines.Count > maxLines)
            {
                completeLines = completeLines.Skip(completeLines.Count - maxLines).ToList();
            }

            return (completeLines, newOffset);
        }
        catch (IOException)
        {
            // File access error - return empty
            return (Array.Empty<string>(), fromByteOffset);
        }
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<string> Lines, long TotalBytes)> ReadTailAsync(
        string logFilePath,
        int lineCount = 200)
    {
        if (!File.Exists(logFilePath))
        {
            return (Array.Empty<string>(), 0);
        }

        try
        {
            var fileInfo = new FileInfo(logFilePath);
            var totalBytes = fileInfo.Length;

            if (totalBytes == 0)
            {
                return (Array.Empty<string>(), 0);
            }

            // Read from the end, chunk by chunk
            const int chunkSize = 8192;
            var lines = new List<string>();
            var position = totalBytes;

            await using var stream = new FileStream(
                logFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            var accumulator = new StringBuilder();

            while (position > 0 && lines.Count < lineCount)
            {
                var readSize = (int)Math.Min(chunkSize, position);
                position -= readSize;

                stream.Seek(position, SeekOrigin.Begin);
                var buffer = new byte[readSize];
                await stream.ReadAsync(buffer, 0, readSize);

                var text = Encoding.UTF8.GetString(buffer);

                // Prepend to accumulator
                accumulator.Insert(0, text);

                // Extract complete lines from the back
                var content = accumulator.ToString();
                var splitLines = content.Split('\n');

                // First element might be incomplete (unless we're at the start)
                for (int i = splitLines.Length - 1; i > 0 && lines.Count < lineCount; i--)
                {
                    var line = splitLines[i].TrimEnd('\r');
                    if (!string.IsNullOrEmpty(line))
                    {
                        lines.Insert(0, line);
                    }
                }

                // Keep the first (possibly incomplete) line for next iteration
                accumulator.Clear();
                if (position > 0)
                {
                    accumulator.Append(splitLines[0]);
                }
                else if (!string.IsNullOrEmpty(splitLines[0].TrimEnd('\r')))
                {
                    // At the start of file, include the first line
                    lines.Insert(0, splitLines[0].TrimEnd('\r'));
                }
            }

            // Trim to requested line count
            if (lines.Count > lineCount)
            {
                lines = lines.Skip(lines.Count - lineCount).ToList();
            }

            return (lines, totalBytes);
        }
        catch (IOException)
        {
            return (Array.Empty<string>(), 0);
        }
    }

    private static LogSnapshot CreateCompletedSnapshot(string logFilePath, LogStatus status)
    {
        if (!File.Exists(logFilePath))
        {
            return new LogSnapshot
            {
                Status = status,
                TimeSinceLastUpdate = TimeSpan.Zero,
                LinesAddedSinceLastSnapshot = 0,
                TotalLineCount = 0,
                FileSizeBytes = 0,
                LastWriteTimeUtc = DateTime.MinValue
            };
        }

        try
        {
            var fileInfo = new FileInfo(logFilePath);
            return new LogSnapshot
            {
                Status = status,
                TimeSinceLastUpdate = DateTime.UtcNow - fileInfo.LastWriteTimeUtc,
                LinesAddedSinceLastSnapshot = 0,
                TotalLineCount = (long)(fileInfo.Length / 80), // Rough estimate
                FileSizeBytes = fileInfo.Length,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
            };
        }
        catch (IOException)
        {
            return new LogSnapshot
            {
                Status = status,
                TimeSinceLastUpdate = TimeSpan.Zero,
                LinesAddedSinceLastSnapshot = 0,
                TotalLineCount = 0,
                FileSizeBytes = 0,
                LastWriteTimeUtc = DateTime.MinValue
            };
        }
    }
}
