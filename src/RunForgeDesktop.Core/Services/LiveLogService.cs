using System.Text;

namespace RunForgeDesktop.Core.Services;

/// <summary>
/// Implementation of live log monitoring with robustness features.
/// Handles truncation, replacement, adaptive polling, and large file performance.
/// </summary>
public sealed class LiveLogService : ILiveLogService
{
    /// <inheritdoc />
    public int MaxBytesPerTick { get; set; } = 64 * 1024; // 64KB per tick to prevent UI hitching

    /// <inheritdoc />
    public TimeSpan SlowPollingThreshold { get; set; } = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    public TimeSpan SlowPollingInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <inheritdoc />
    public TimeSpan FastPollingInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc />
    public LogSnapshot GetSnapshot(
        string logFilePath,
        LogMonitorState state,
        string? runStatus,
        TimeSpan staleThreshold)
    {
        // Check if run already completed
        if (runStatus == "succeeded")
        {
            return CreateCompletedSnapshot(logFilePath, LogStatus.Completed, state);
        }

        if (runStatus == "failed")
        {
            return CreateCompletedSnapshot(logFilePath, LogStatus.Failed, state);
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
                LastWriteTimeUtc = DateTime.MinValue,
                FileChange = state.LastFileSize > 0 ? FileChangeReason.Deleted : FileChangeReason.None,
                RecommendedPollingInterval = FastPollingInterval
            };
        }

        try
        {
            var fileInfo = new FileInfo(logFilePath);
            var lastWriteUtc = fileInfo.LastWriteTimeUtc;
            var creationTimeUtc = fileInfo.CreationTimeUtc;
            var currentSize = fileInfo.Length;
            var timeSinceUpdate = DateTime.UtcNow - lastWriteUtc;

            // Detect file changes
            var fileChange = FileChangeReason.None;

            // Check for file replacement (different creation time)
            if (state.LastCreationTimeUtc != DateTime.MinValue &&
                creationTimeUtc != state.LastCreationTimeUtc)
            {
                fileChange = FileChangeReason.Replaced;
            }
            // Check for truncation (size decreased)
            else if (currentSize < state.LastFileSize)
            {
                fileChange = FileChangeReason.Truncated;
            }

            // Estimate lines added (rough: assume ~80 chars per line average)
            var bytesAdded = Math.Max(0, currentSize - state.LastFileSize);
            var estimatedLinesAdded = (int)(bytesAdded / 80);

            // Estimate total lines (same rough calculation)
            var estimatedTotalLines = (long)(currentSize / 80);

            // Determine status based on staleness
            var status = timeSinceUpdate > staleThreshold
                ? LogStatus.Stale
                : LogStatus.Receiving;

            // Adaptive polling: slow down when stale
            var recommendedInterval = FastPollingInterval;
            if (status == LogStatus.Stale && timeSinceUpdate > SlowPollingThreshold)
            {
                recommendedInterval = SlowPollingInterval;
            }

            // Update last activity time if we got new data
            if (bytesAdded > 0)
            {
                state.LastActivityUtc = DateTime.UtcNow;
            }

            // Update state
            state.CurrentPollingInterval = recommendedInterval;

            return new LogSnapshot
            {
                Status = status,
                TimeSinceLastUpdate = timeSinceUpdate,
                LinesAddedSinceLastSnapshot = estimatedLinesAdded,
                TotalLineCount = estimatedTotalLines,
                FileSizeBytes = currentSize,
                LastWriteTimeUtc = lastWriteUtc,
                CreationTimeUtc = creationTimeUtc,
                FileChange = fileChange,
                RecommendedPollingInterval = recommendedInterval
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
                LastWriteTimeUtc = DateTime.MinValue,
                RecommendedPollingInterval = FastPollingInterval
            };
        }
    }

    /// <inheritdoc />
    public LogSnapshot GetSnapshot(
        string logFilePath,
        long lastKnownSize,
        string? runStatus,
        TimeSpan staleThreshold)
    {
        // Compatibility wrapper - create temporary state
        var state = new LogMonitorState { LastFileSize = lastKnownSize };
        return GetSnapshot(logFilePath, state, runStatus, staleThreshold);
    }

    /// <inheritdoc />
    public async Task<LogDeltaResult> ReadDeltaAsync(
        string logFilePath,
        LogMonitorState state,
        int maxLines = 500)
    {
        if (!File.Exists(logFilePath))
        {
            if (state.LastFileSize > 0)
            {
                // File was deleted
                state.Reset();
                return new LogDeltaResult
                {
                    Lines = Array.Empty<string>(),
                    WasReset = true,
                    ResetReason = FileChangeReason.Deleted
                };
            }
            return new LogDeltaResult { Lines = Array.Empty<string>() };
        }

        try
        {
            var fileInfo = new FileInfo(logFilePath);
            var currentSize = fileInfo.Length;
            var creationTime = fileInfo.CreationTimeUtc;

            // Detect file replacement or truncation
            var wasReset = false;
            var resetReason = FileChangeReason.None;

            // Check for replacement (different creation time)
            if (state.LastCreationTimeUtc != DateTime.MinValue &&
                creationTime != state.LastCreationTimeUtc)
            {
                wasReset = true;
                resetReason = FileChangeReason.Replaced;
                state.Reset();
            }
            // Check for truncation (size decreased)
            else if (currentSize < state.LastByteOffset)
            {
                wasReset = true;
                resetReason = FileChangeReason.Truncated;
                state.LastByteOffset = 0; // Reset to start
            }

            // No new data
            if (currentSize <= state.LastByteOffset)
            {
                // Update state tracking
                state.LastFileSize = currentSize;
                state.LastCreationTimeUtc = creationTime;
                state.LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;

                return new LogDeltaResult
                {
                    Lines = Array.Empty<string>(),
                    WasReset = wasReset,
                    ResetReason = resetReason
                };
            }

            // Calculate bytes to read with cap
            var totalBytesAvailable = currentSize - state.LastByteOffset;
            var bytesToRead = Math.Min(totalBytesAvailable, MaxBytesPerTick);
            var wasCapped = bytesToRead < totalBytesAvailable;
            var bytesRemaining = totalBytesAvailable - bytesToRead;

            await using var stream = new FileStream(
                logFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            stream.Seek(state.LastByteOffset, SeekOrigin.Begin);

            var buffer = new byte[bytesToRead];
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, (int)bytesToRead));

            if (bytesRead == 0)
            {
                return new LogDeltaResult
                {
                    Lines = Array.Empty<string>(),
                    WasReset = wasReset,
                    ResetReason = resetReason
                };
            }

            var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var lines = text.Split('\n', StringSplitOptions.None);

            // Handle partial line at the end
            var completeLines = new List<string>();
            var newOffset = state.LastByteOffset + bytesRead;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');

                if (i == lines.Length - 1)
                {
                    if (string.IsNullOrEmpty(line))
                    {
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

            // Update state
            state.LastByteOffset = newOffset;
            state.LastFileSize = currentSize;
            state.LastCreationTimeUtc = creationTime;
            state.LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
            state.LastActivityUtc = DateTime.UtcNow;

            return new LogDeltaResult
            {
                Lines = completeLines,
                WasReset = wasReset,
                ResetReason = resetReason,
                WasCapped = wasCapped,
                BytesRemaining = bytesRemaining
            };
        }
        catch (IOException ex)
        {
            return new LogDeltaResult
            {
                Lines = Array.Empty<string>(),
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<string> Lines, long NewByteOffset)> ReadDeltaAsync(
        string logFilePath,
        long fromByteOffset,
        int maxLines = 500)
    {
        // Compatibility wrapper
        var state = new LogMonitorState { LastByteOffset = fromByteOffset };
        var result = await ReadDeltaAsync(logFilePath, state, maxLines);
        return (result.Lines, state.LastByteOffset);
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
                await stream.ReadAsync(buffer.AsMemory(0, readSize));

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

    private LogSnapshot CreateCompletedSnapshot(string logFilePath, LogStatus status, LogMonitorState? state)
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
                LastWriteTimeUtc = DateTime.MinValue,
                RecommendedPollingInterval = SlowPollingInterval
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
                TotalLineCount = (long)(fileInfo.Length / 80),
                FileSizeBytes = fileInfo.Length,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                CreationTimeUtc = fileInfo.CreationTimeUtc,
                RecommendedPollingInterval = SlowPollingInterval
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
                LastWriteTimeUtc = DateTime.MinValue,
                RecommendedPollingInterval = SlowPollingInterval
            };
        }
    }
}
