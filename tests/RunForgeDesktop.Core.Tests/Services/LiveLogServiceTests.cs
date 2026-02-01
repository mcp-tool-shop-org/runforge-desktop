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
