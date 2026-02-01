using RunForgeDesktop.Core.Services;

namespace RunForgeDesktop.Core.Tests.Services;

/// <summary>
/// Tests for LiveLogService.
/// </summary>
public class LiveLogServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly LiveLogService _service;

    public LiveLogServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"LiveLogServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _service = new LiveLogService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        GC.SuppressFinalize(this);
    }

    #region GetSnapshot Tests

    [Fact]
    public void GetSnapshot_FileDoesNotExist_ReturnsNoLogsStatus()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "nonexistent.txt");

        // Act
        var snapshot = _service.GetSnapshot(logPath, 0, null, TimeSpan.FromSeconds(30));

        // Assert
        Assert.Equal(LogStatus.NoLogs, snapshot.Status);
        Assert.Equal(0, snapshot.FileSizeBytes);
        Assert.Equal(0, snapshot.TotalLineCount);
    }

    [Fact]
    public void GetSnapshot_RunSucceeded_ReturnsCompletedStatus()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "some log content\n");

        // Act
        var snapshot = _service.GetSnapshot(logPath, 0, "succeeded", TimeSpan.FromSeconds(30));

        // Assert
        Assert.Equal(LogStatus.Completed, snapshot.Status);
    }

    [Fact]
    public void GetSnapshot_RunFailed_ReturnsFailedStatus()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "some log content\n");

        // Act
        var snapshot = _service.GetSnapshot(logPath, 0, "failed", TimeSpan.FromSeconds(30));

        // Assert
        Assert.Equal(LogStatus.Failed, snapshot.Status);
    }

    [Fact]
    public void GetSnapshot_RecentlyWritten_ReturnsReceivingStatus()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "some log content\n");

        // Act
        var snapshot = _service.GetSnapshot(logPath, 0, null, TimeSpan.FromMinutes(5));

        // Assert
        Assert.Equal(LogStatus.Receiving, snapshot.Status);
        Assert.True(snapshot.TimeSinceLastUpdate < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetSnapshot_StaleFile_ReturnsStaleStatus()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "some log content\n");

        // Set file time to 1 minute ago
        var oldTime = DateTime.UtcNow.AddMinutes(-1);
        File.SetLastWriteTimeUtc(logPath, oldTime);

        // Act
        var snapshot = _service.GetSnapshot(logPath, 0, null, TimeSpan.FromSeconds(30));

        // Assert
        Assert.Equal(LogStatus.Stale, snapshot.Status);
        Assert.True(snapshot.TimeSinceLastUpdate > TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GetSnapshot_TracksFileSize()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        var content = "line1\nline2\nline3\n";
        File.WriteAllText(logPath, content);

        // Act
        var snapshot = _service.GetSnapshot(logPath, 0, null, TimeSpan.FromMinutes(5));

        // Assert
        Assert.True(snapshot.FileSizeBytes > 0);
    }

    [Fact]
    public void GetSnapshot_EstimatesLinesAdded()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        var content = new string('x', 160) + "\n"; // ~2 lines worth at 80 chars/line
        File.WriteAllText(logPath, content);

        // Act
        var snapshot = _service.GetSnapshot(logPath, 0, null, TimeSpan.FromMinutes(5));

        // Assert
        Assert.True(snapshot.LinesAddedSinceLastSnapshot > 0);
    }

    #endregion

    #region ReadDeltaAsync Tests

    [Fact]
    public async Task ReadDeltaAsync_FileDoesNotExist_ReturnsEmpty()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "nonexistent.txt");

        // Act
        var (lines, offset) = await _service.ReadDeltaAsync(logPath, 0);

        // Assert
        Assert.Empty(lines);
        Assert.Equal(0, offset);
    }

    [Fact]
    public async Task ReadDeltaAsync_NoNewData_ReturnsEmpty()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        var content = "line1\nline2\n";
        File.WriteAllText(logPath, content);
        var initialSize = new FileInfo(logPath).Length;

        // Act
        var (lines, offset) = await _service.ReadDeltaAsync(logPath, initialSize);

        // Assert
        Assert.Empty(lines);
        Assert.Equal(initialSize, offset);
    }

    [Fact]
    public async Task ReadDeltaAsync_NewLinesAppended_ReturnsOnlyNewLines()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "line1\nline2\n");
        var initialSize = new FileInfo(logPath).Length;

        // Append new lines
        File.AppendAllText(logPath, "line3\nline4\n");

        // Act
        var (lines, offset) = await _service.ReadDeltaAsync(logPath, initialSize);

        // Assert
        Assert.Equal(2, lines.Count);
        Assert.Equal("line3", lines[0]);
        Assert.Equal("line4", lines[1]);
    }

    [Fact]
    public async Task ReadDeltaAsync_RespectsMaxLines()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "");

        // Write 10 lines
        for (int i = 0; i < 10; i++)
        {
            File.AppendAllText(logPath, $"line{i}\n");
        }

        // Act
        var (lines, _) = await _service.ReadDeltaAsync(logPath, 0, maxLines: 3);

        // Assert
        Assert.Equal(3, lines.Count);
        // Should keep the most recent lines
        Assert.Contains("line7", lines);
        Assert.Contains("line8", lines);
        Assert.Contains("line9", lines);
    }

    #endregion

    #region ReadTailAsync Tests

    [Fact]
    public async Task ReadTailAsync_FileDoesNotExist_ReturnsEmpty()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "nonexistent.txt");

        // Act
        var (lines, bytes) = await _service.ReadTailAsync(logPath);

        // Assert
        Assert.Empty(lines);
        Assert.Equal(0, bytes);
    }

    [Fact]
    public async Task ReadTailAsync_EmptyFile_ReturnsEmpty()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "");

        // Act
        var (lines, bytes) = await _service.ReadTailAsync(logPath);

        // Assert
        Assert.Empty(lines);
        Assert.Equal(0, bytes);
    }

    [Fact]
    public async Task ReadTailAsync_SmallFile_ReturnsAllLines()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "line1\nline2\nline3\n");

        // Act
        var (lines, _) = await _service.ReadTailAsync(logPath, lineCount: 200);

        // Assert
        Assert.Equal(3, lines.Count);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
        Assert.Equal("line3", lines[2]);
    }

    [Fact]
    public async Task ReadTailAsync_LargeFile_ReturnsOnlyRequestedLines()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        var content = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"line{i}")) + "\n";
        File.WriteAllText(logPath, content);

        // Act
        var (lines, _) = await _service.ReadTailAsync(logPath, lineCount: 5);

        // Assert
        Assert.Equal(5, lines.Count);
        Assert.Equal("line95", lines[0]);
        Assert.Equal("line96", lines[1]);
        Assert.Equal("line97", lines[2]);
        Assert.Equal("line98", lines[3]);
        Assert.Equal("line99", lines[4]);
    }

    [Fact]
    public async Task ReadTailAsync_ReturnsTotalBytes()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        var content = "line1\nline2\n";
        File.WriteAllText(logPath, content);
        var expectedSize = new FileInfo(logPath).Length;

        // Act
        var (_, bytes) = await _service.ReadTailAsync(logPath);

        // Assert
        Assert.Equal(expectedSize, bytes);
    }

    #endregion

    #region Robustness Tests - Truncation and Replacement Detection

    [Fact]
    public void GetSnapshot_FileDeleted_ReturnsDeletedChange()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "some content\n");
        var state = new LogMonitorState { LastFileSize = 100 };

        // Delete the file
        File.Delete(logPath);

        // Act
        var snapshot = _service.GetSnapshot(logPath, state, null, TimeSpan.FromMinutes(5));

        // Assert
        Assert.Equal(LogStatus.NoLogs, snapshot.Status);
        Assert.Equal(FileChangeReason.Deleted, snapshot.FileChange);
    }

    [Fact]
    public void GetSnapshot_FileTruncated_ReturnsTruncatedChange()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "line1\nline2\nline3\nline4\n");
        var initialSize = new FileInfo(logPath).Length;

        var state = new LogMonitorState
        {
            LastFileSize = initialSize,
            LastCreationTimeUtc = new FileInfo(logPath).CreationTimeUtc
        };

        // Truncate the file (write less content)
        File.WriteAllText(logPath, "line1\n");

        // Act
        var snapshot = _service.GetSnapshot(logPath, state, null, TimeSpan.FromMinutes(5));

        // Assert
        Assert.Equal(FileChangeReason.Truncated, snapshot.FileChange);
    }

    [Fact]
    public async Task ReadDeltaAsync_FileTruncated_ReturnsWasReset()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "line1\nline2\nline3\nline4\n");
        var initialSize = new FileInfo(logPath).Length;

        var state = new LogMonitorState
        {
            LastByteOffset = initialSize,
            LastFileSize = initialSize,
            LastCreationTimeUtc = new FileInfo(logPath).CreationTimeUtc
        };

        // Truncate the file
        File.WriteAllText(logPath, "new\n");

        // Act
        var result = await _service.ReadDeltaAsync(logPath, state);

        // Assert
        Assert.True(result.WasReset);
        Assert.Equal(FileChangeReason.Truncated, result.ResetReason);
    }

    [Fact]
    public async Task ReadDeltaAsync_FileReplaced_ReturnsWasReset()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "original content\n");

        // Use a creation time in the past so replacement is detectable
        var oldCreationTime = DateTime.UtcNow.AddHours(-1);
        File.SetCreationTimeUtc(logPath, oldCreationTime);

        var state = new LogMonitorState
        {
            LastByteOffset = new FileInfo(logPath).Length,
            LastFileSize = new FileInfo(logPath).Length,
            LastCreationTimeUtc = oldCreationTime
        };

        // Delete and recreate the file (which will have a new creation time)
        File.Delete(logPath);
        await Task.Delay(10);
        File.WriteAllText(logPath, "replaced content\n");
        // Ensure the new file has a different (current) creation time
        // Windows should set this automatically, but we can force it
        File.SetCreationTimeUtc(logPath, DateTime.UtcNow);

        // Act
        var result = await _service.ReadDeltaAsync(logPath, state);

        // Assert
        Assert.True(result.WasReset);
        Assert.Equal(FileChangeReason.Replaced, result.ResetReason);
    }

    [Fact]
    public async Task ReadDeltaAsync_FileDeleted_ReturnsWasReset()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "some content\n");

        var state = new LogMonitorState
        {
            LastByteOffset = new FileInfo(logPath).Length,
            LastFileSize = new FileInfo(logPath).Length
        };

        // Delete the file
        File.Delete(logPath);

        // Act
        var result = await _service.ReadDeltaAsync(logPath, state);

        // Assert
        Assert.True(result.WasReset);
        Assert.Equal(FileChangeReason.Deleted, result.ResetReason);
    }

    #endregion

    #region Robustness Tests - Adaptive Polling

    [Fact]
    public void GetSnapshot_RecentActivity_ReturnsFastPollingInterval()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "some log content\n");
        var state = new LogMonitorState();

        // Act
        var snapshot = _service.GetSnapshot(logPath, state, null, TimeSpan.FromMinutes(5));

        // Assert
        Assert.Equal(LogStatus.Receiving, snapshot.Status);
        Assert.Equal(_service.FastPollingInterval, snapshot.RecommendedPollingInterval);
    }

    [Fact]
    public void GetSnapshot_StaleFile_ReturnsSlowPollingInterval()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "some log content\n");

        // Set file time to 2 minutes ago (beyond SlowPollingThreshold of 60s)
        var oldTime = DateTime.UtcNow.AddMinutes(-2);
        File.SetLastWriteTimeUtc(logPath, oldTime);

        var state = new LogMonitorState();

        // Act
        var snapshot = _service.GetSnapshot(logPath, state, null, TimeSpan.FromSeconds(30));

        // Assert
        Assert.Equal(LogStatus.Stale, snapshot.Status);
        Assert.Equal(_service.SlowPollingInterval, snapshot.RecommendedPollingInterval);
    }

    [Fact]
    public void GetSnapshot_UpdatesStatePollingInterval()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "some content\n");
        var state = new LogMonitorState();

        // Act
        _service.GetSnapshot(logPath, state, null, TimeSpan.FromMinutes(5));

        // Assert
        Assert.Equal(_service.FastPollingInterval, state.CurrentPollingInterval);
    }

    #endregion

    #region Robustness Tests - MaxBytesPerTick Capping

    [Fact]
    public async Task ReadDeltaAsync_LargeChunk_ReturnsCappedResult()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");

        // Set a small MaxBytesPerTick for testing
        _service.MaxBytesPerTick = 100;

        // Write 500 bytes
        var content = new string('x', 50) + "\n";
        for (int i = 0; i < 10; i++)
        {
            File.AppendAllText(logPath, content);
        }

        var state = new LogMonitorState { LastByteOffset = 0 };

        // Act
        var result = await _service.ReadDeltaAsync(logPath, state);

        // Assert
        Assert.True(result.WasCapped);
        Assert.True(result.BytesRemaining > 0);
    }

    [Fact]
    public async Task ReadDeltaAsync_SmallChunk_NotCapped()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "line1\nline2\nline3\n");

        var state = new LogMonitorState { LastByteOffset = 0 };

        // Act
        var result = await _service.ReadDeltaAsync(logPath, state);

        // Assert
        Assert.False(result.WasCapped);
        Assert.Equal(0, result.BytesRemaining);
    }

    #endregion

    #region LogMonitorState Tests

    [Fact]
    public void LogMonitorState_Reset_ClearsAllTracking()
    {
        // Arrange
        var state = new LogMonitorState
        {
            LastByteOffset = 1000,
            LastFileSize = 2000,
            LastCreationTimeUtc = DateTime.UtcNow,
            LastWriteTimeUtc = DateTime.UtcNow,
            LastActivityUtc = DateTime.UtcNow
        };

        // Act
        state.Reset();

        // Assert
        Assert.Equal(0, state.LastByteOffset);
        Assert.Equal(0, state.LastFileSize);
        Assert.Equal(DateTime.MinValue, state.LastCreationTimeUtc);
        Assert.Equal(DateTime.MinValue, state.LastWriteTimeUtc);
    }

    [Fact]
    public async Task ReadDeltaAsync_WithState_UpdatesAllStateFields()
    {
        // Arrange
        var logPath = Path.Combine(_testDir, "logs.txt");
        File.WriteAllText(logPath, "line1\nline2\n");
        var state = new LogMonitorState();

        // Act
        await _service.ReadDeltaAsync(logPath, state);

        // Assert
        Assert.True(state.LastByteOffset > 0);
        Assert.True(state.LastFileSize > 0);
        Assert.NotEqual(DateTime.MinValue, state.LastCreationTimeUtc);
        Assert.NotEqual(DateTime.MinValue, state.LastWriteTimeUtc);
        Assert.True(state.LastActivityUtc > DateTime.UtcNow.AddMinutes(-1));
    }

    #endregion

    #region LogSnapshot Tests

    [Fact]
    public void LogSnapshot_StatusMessage_FormatsCorrectly()
    {
        // Arrange & Act
        var noLogs = new LogSnapshot
        {
            Status = LogStatus.NoLogs,
            TimeSinceLastUpdate = TimeSpan.Zero,
            LinesAddedSinceLastSnapshot = 0,
            TotalLineCount = 0,
            FileSizeBytes = 0,
            LastWriteTimeUtc = DateTime.MinValue
        };

        var receiving = new LogSnapshot
        {
            Status = LogStatus.Receiving,
            TimeSinceLastUpdate = TimeSpan.FromSeconds(0.5),
            LinesAddedSinceLastSnapshot = 5,
            TotalLineCount = 100,
            FileSizeBytes = 8000,
            LastWriteTimeUtc = DateTime.UtcNow
        };

        var stale = new LogSnapshot
        {
            Status = LogStatus.Stale,
            TimeSinceLastUpdate = TimeSpan.FromSeconds(45),
            LinesAddedSinceLastSnapshot = 0,
            TotalLineCount = 100,
            FileSizeBytes = 8000,
            LastWriteTimeUtc = DateTime.UtcNow.AddSeconds(-45)
        };

        // Assert
        Assert.Equal("No logs available", noLogs.StatusMessage);
        Assert.Equal("Receiving logs", receiving.StatusMessage);
        Assert.Contains("No new logs", stale.StatusMessage);
    }

    [Fact]
    public void LogSnapshot_StatusIndicator_ReturnsCorrectSymbol()
    {
        // Arrange & Act
        var statuses = new[]
        {
            (LogStatus.NoLogs, "○"),
            (LogStatus.Receiving, "●"),
            (LogStatus.Stale, "◐"),
            (LogStatus.Completed, "✓"),
            (LogStatus.Failed, "✗")
        };

        foreach (var (status, expected) in statuses)
        {
            var snapshot = new LogSnapshot
            {
                Status = status,
                TimeSinceLastUpdate = TimeSpan.Zero,
                LinesAddedSinceLastSnapshot = 0,
                TotalLineCount = 0,
                FileSizeBytes = 0,
                LastWriteTimeUtc = DateTime.MinValue
            };

            // Assert
            Assert.Equal(expected, snapshot.StatusIndicator);
        }
    }

    #endregion
}
