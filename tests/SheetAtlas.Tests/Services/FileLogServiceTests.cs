using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Models;
using Microsoft.Extensions.Logging;

namespace SheetAtlas.Tests.Services
{
    public class FileLogServiceTests : IDisposable
    {
        private readonly Mock<ILogger<FileLogService>> _mockLogger;
        private readonly string _tempDirectory;
        private readonly FileLogService _service;

        public FileLogServiceTests()
        {
            _mockLogger = new Mock<ILogger<FileLogService>>();
            _tempDirectory = Path.Combine(Path.GetTempPath(), $"SheetAtlas_Tests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDirectory);

            // Mock the environment to use our temp directory
            _service = new FileLogService(_mockLogger.Object);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                    Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region SaveFileLogAsync Tests

        [Fact]
        public async Task SaveFileLogAsync_WithValidEntry_CreatesLogFile()
        {
            // Arrange
            var logEntry = CreateValidFileLogEntry();
            var rootDir = _service.GetLogRootDirectory();

            // Act
            await _service.SaveFileLogAsync(logEntry);

            // Assert
            Directory.Exists(rootDir).Should().BeTrue();
            var folders = Directory.GetDirectories(rootDir);
            folders.Should().NotBeEmpty();
        }

        [Fact]
        public async Task SaveFileLogAsync_WithValidEntry_WritesJsonFile()
        {
            // Arrange
            var logEntry = CreateValidFileLogEntry();
            var rootDir = _service.GetLogRootDirectory();

            // Act
            await _service.SaveFileLogAsync(logEntry);

            // Assert
            var jsonFiles = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);
            jsonFiles.Should().NotBeEmpty();

            var content = File.ReadAllText(jsonFiles[0]);
            content.Should().Contain("\"schemaVersion\"");
            content.Should().Contain("\"file\"");
            content.Should().Contain("\"loadAttempt\"");
        }

        [Fact]
        public async Task SaveFileLogAsync_WithNullEntry_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SaveFileLogAsync(null!));
        }

        [Fact]
        public async Task SaveFileLogAsync_WithNullFile_ThrowsArgumentException()
        {
            // Arrange
            var logEntry = new FileLogEntry { File = null!, LoadAttempt = new LoadAttemptInfo() };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveFileLogAsync(logEntry));
        }

        [Fact]
        public async Task SaveFileLogAsync_WithNullFilePath_ThrowsArgumentException()
        {
            // Arrange
            var logEntry = new FileLogEntry
            {
                File = new FileInfoDto { OriginalPath = null! },
                LoadAttempt = new LoadAttemptInfo()
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveFileLogAsync(logEntry));
        }

        [Fact]
        public async Task SaveFileLogAsync_WithEmptyFilePath_ThrowsArgumentException()
        {
            // Arrange
            var logEntry = new FileLogEntry
            {
                File = new FileInfoDto { OriginalPath = "" },
                LoadAttempt = new LoadAttemptInfo()
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveFileLogAsync(logEntry));
        }

        [Fact]
        public async Task SaveFileLogAsync_MultipleEntries_CreatesMultipleFiles()
        {
            // Arrange
            var entry1 = CreateValidFileLogEntry();
            var entry2 = CreateValidFileLogEntry();
            var rootDir = _service.GetLogRootDirectory();

            // Act
            await _service.SaveFileLogAsync(entry1);
            await Task.Delay(1100); // Ensure different timestamps
            await _service.SaveFileLogAsync(entry2);

            // Assert
            var jsonFiles = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);
            jsonFiles.Should().HaveCountGreaterThanOrEqualTo(2);
        }

        [Fact]
        public async Task SaveFileLogAsync_CreatesFolderStructure()
        {
            // Arrange
            var logEntry = CreateValidFileLogEntry();
            var rootDir = _service.GetLogRootDirectory();

            // Act
            await _service.SaveFileLogAsync(logEntry);

            // Assert
            Directory.Exists(rootDir).Should().BeTrue();
            var subFolders = Directory.GetDirectories(rootDir);
            subFolders.Should().NotBeEmpty();
        }

        [Fact]
        public async Task SaveFileLogAsync_SerializesAllProperties()
        {
            // Arrange
            var logEntry = CreateValidFileLogEntry();
            logEntry.Errors.Add(ExcelError.FileError("Test error"));
            var rootDir = _service.GetLogRootDirectory();

            // Act
            await _service.SaveFileLogAsync(logEntry);

            // Assert
            var jsonFiles = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);
            var content = File.ReadAllText(jsonFiles[0]);

            content.Should().Contain("\"errors\"");
            content.Should().Contain("\"summary\"");
            content.Should().Contain("\"extensions\"");
        }

        [Fact]
        public async Task SaveFileLogAsync_UsesAtomicWrite()
        {
            // Arrange
            var logEntry = CreateValidFileLogEntry();
            var rootDir = _service.GetLogRootDirectory();

            // Act
            await _service.SaveFileLogAsync(logEntry);

            // Assert - No .tmp files should remain
            var tmpFiles = Directory.GetFiles(rootDir, "*.tmp", SearchOption.AllDirectories);
            tmpFiles.Should().BeEmpty();
        }

        #endregion

        #region GetFileLogHistoryAsync Tests

        [Fact]
        public async Task GetFileLogHistoryAsync_WithExistingLogs_ReturnsAllLogs()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            var entry1 = CreateValidFileLogEntry(filePath);
            var entry2 = CreateValidFileLogEntry(filePath);
            await _service.SaveFileLogAsync(entry1);
            await Task.Delay(1100);
            await _service.SaveFileLogAsync(entry2);

            // Act
            var result = await _service.GetFileLogHistoryAsync(filePath);

            // Assert
            result.Should().NotBeEmpty();
            result.Count.Should().BeGreaterThanOrEqualTo(2);
        }

        [Fact]
        public async Task GetFileLogHistoryAsync_WithNoLogs_ReturnsEmptyList()
        {
            // Arrange
            var filePath = "/home/user/nonexistent.xlsx";

            // Act
            var result = await _service.GetFileLogHistoryAsync(filePath);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetFileLogHistoryAsync_ReturnsSortedNewestFirst()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            var timestamps = new List<DateTime>();

            for (int i = 0; i < 3; i++)
            {
                var entry = CreateValidFileLogEntry(filePath);
                timestamps.Add(entry.LoadAttempt.Timestamp);
                await _service.SaveFileLogAsync(entry);
                await Task.Delay(1100);
            }

            // Act
            var result = await _service.GetFileLogHistoryAsync(filePath);

            // Assert
            result.Should().NotBeEmpty();
            // Verify sorted by timestamp (newest first)
            for (int i = 0; i < result.Count - 1; i++)
            {
                result[i].LoadAttempt.Timestamp.Should().BeOnOrAfter(result[i + 1].LoadAttempt.Timestamp);
            }
        }

        [Fact]
        public async Task GetFileLogHistoryAsync_WithNullPath_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetFileLogHistoryAsync(null!));
        }

        [Fact]
        public async Task GetFileLogHistoryAsync_WithEmptyPath_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetFileLogHistoryAsync(""));
        }

        [Fact]
        public async Task GetFileLogHistoryAsync_WithWhitespacePath_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetFileLogHistoryAsync("   "));
        }

        [Fact]
        public async Task GetFileLogHistoryAsync_SkipsCorruptedFiles()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            var entry = CreateValidFileLogEntry(filePath);
            await _service.SaveFileLogAsync(entry);

            // Corrupt one JSON file
            var rootDir = _service.GetLogRootDirectory();
            var jsonFiles = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);
            if (jsonFiles.Length > 0)
            {
                File.WriteAllText(jsonFiles[0], "{ invalid json");
            }

            // Add a valid entry
            await Task.Delay(1100);
            await _service.SaveFileLogAsync(CreateValidFileLogEntry(filePath));

            // Act
            var result = await _service.GetFileLogHistoryAsync(filePath);

            // Assert - Should return at least the valid entry
            result.Should().NotBeEmpty();
        }

        #endregion

        #region GetLatestFileLogAsync Tests

        [Fact]
        public async Task GetLatestFileLogAsync_WithExistingLogs_ReturnsNewestLog()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            var entry1 = CreateValidFileLogEntry(filePath);
            await _service.SaveFileLogAsync(entry1);
            await Task.Delay(1100);

            var entry2 = CreateValidFileLogEntry(filePath);
            await _service.SaveFileLogAsync(entry2);

            // Act
            var result = await _service.GetLatestFileLogAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            result!.LoadAttempt.Timestamp.Should().BeCloseTo(entry2.LoadAttempt.Timestamp, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task GetLatestFileLogAsync_WithNoLogs_ReturnsNull()
        {
            // Arrange
            var filePath = "/home/user/nonexistent.xlsx";

            // Act
            var result = await _service.GetLatestFileLogAsync(filePath);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetLatestFileLogAsync_WithSingleLog_ReturnsThatLog()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            var entry = CreateValidFileLogEntry(filePath);
            await _service.SaveFileLogAsync(entry);

            // Act
            var result = await _service.GetLatestFileLogAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            result!.File.OriginalPath.Should().Be(filePath);
        }

        #endregion

        #region DeleteFileLogsAsync Tests

        [Fact]
        public async Task DeleteFileLogsAsync_WithExistingLogs_DeletesFolder()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            var entry = CreateValidFileLogEntry(filePath);
            await _service.SaveFileLogAsync(entry);

            var rootDir = _service.GetLogRootDirectory();
            var foldersBefore = Directory.GetDirectories(rootDir).Length;
            foldersBefore.Should().BeGreaterThan(0);

            // Act
            await _service.DeleteFileLogsAsync(filePath);

            // Assert
            var foldersAfter = Directory.GetDirectories(rootDir).Length;
            foldersAfter.Should().BeLessThan(foldersBefore);
        }

        [Fact]
        public async Task DeleteFileLogsAsync_WithNoLogs_CompletesSuccessfully()
        {
            // Arrange
            var filePath = "/home/user/nonexistent.xlsx";

            // Act & Assert
            await _service.DeleteFileLogsAsync(filePath);
        }

        [Fact]
        public async Task DeleteFileLogsAsync_WithNullPath_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteFileLogsAsync(null!));
        }

        [Fact]
        public async Task DeleteFileLogsAsync_RemovesAllFilesInFolder()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            await _service.SaveFileLogAsync(CreateValidFileLogEntry(filePath));
            await Task.Delay(1100);
            await _service.SaveFileLogAsync(CreateValidFileLogEntry(filePath));

            var rootDir = _service.GetLogRootDirectory();
            var jsonFilesBefore = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);
            jsonFilesBefore.Should().NotBeEmpty();

            // Act
            await _service.DeleteFileLogsAsync(filePath);

            // Assert
            var jsonFilesAfter = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);
            jsonFilesAfter.Should().NotContain(f => f.Contains(Path.GetFileName(filePath)));
        }

        #endregion

        #region CleanupOldLogsAsync Tests

        [Fact]
        public async Task CleanupOldLogsAsync_WithRetentionDaysLessThanOne_SkipsCleanup()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            var entry = CreateValidFileLogEntry(filePath);
            await _service.SaveFileLogAsync(entry);

            var rootDir = _service.GetLogRootDirectory();
            var filesBefore = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);

            // Act
            await _service.CleanupOldLogsAsync(0);

            // Assert
            var filesAfter = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);
            filesAfter.Should().HaveSameCount(filesBefore);
        }

        [Fact]
        public async Task CleanupOldLogsAsync_WithNegativeRetentionDays_SkipsCleanup()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            await _service.SaveFileLogAsync(CreateValidFileLogEntry(filePath));

            var rootDir = _service.GetLogRootDirectory();
            var filesBefore = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);

            // Act
            await _service.CleanupOldLogsAsync(-1);

            // Assert
            var filesAfter = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);
            filesAfter.Should().HaveSameCount(filesBefore);
        }

        [Fact]
        public async Task CleanupOldLogsAsync_WithVeryHighRetention_KeepsRecentFiles()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            var entry = CreateValidFileLogEntry(filePath);
            await _service.SaveFileLogAsync(entry);

            var rootDir = _service.GetLogRootDirectory();
            var filesBefore = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);

            // Act
            await _service.CleanupOldLogsAsync(365);

            // Assert
            var filesAfter = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);
            filesAfter.Should().HaveSameCount(filesBefore);
        }

        [Fact]
        public async Task CleanupOldLogsAsync_DeletesOldFiles()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            var entry = CreateValidFileLogEntry(filePath);
            await _service.SaveFileLogAsync(entry);

            var rootDir = _service.GetLogRootDirectory();
            var jsonFiles = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);

            // Modify file's last write time to be 40 days old
            foreach (var file in jsonFiles)
            {
                var oldDate = DateTime.Now.AddDays(-40);
                File.SetLastWriteTime(file, oldDate);
            }

            // Act
            await _service.CleanupOldLogsAsync(30);

            // Assert
            var filesAfter = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);
            filesAfter.Should().BeEmpty();
        }

        [Fact]
        public async Task CleanupOldLogsAsync_DeletesEmptyFolders()
        {
            // Arrange
            var filePath = "/home/user/test.xlsx";
            var entry = CreateValidFileLogEntry(filePath);
            await _service.SaveFileLogAsync(entry);

            var rootDir = _service.GetLogRootDirectory();
            var jsonFiles = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);

            // Make files old
            foreach (var file in jsonFiles)
            {
                File.SetLastWriteTime(file, DateTime.Now.AddDays(-40));
            }

            var foldersBefore = Directory.GetDirectories(rootDir);
            foldersBefore.Should().NotBeEmpty();

            // Act
            await _service.CleanupOldLogsAsync(30);

            // Assert
            var foldersAfter = Directory.GetDirectories(rootDir);
            // All file folders should be deleted
            var allFolders = Directory.GetDirectories(rootDir, "*", SearchOption.AllDirectories);
            allFolders.Should().BeEmpty();
        }

        [Fact]
        public async Task CleanupOldLogsAsync_WithNonExistentLogDirectory_CompletesSuccessfully()
        {
            // Act & Assert
            await _service.CleanupOldLogsAsync(30);
        }

        #endregion

        #region GetLogRootDirectory Tests

        [Fact]
        public void GetLogRootDirectory_ReturnsMeaningfulPath()
        {
            // Act
            var result = _service.GetLogRootDirectory();

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("SheetAtlas");
            result.Should().Contain("Logs");
        }

        #endregion

        #region Integration Tests

        [Fact]
        public async Task CompleteWorkflow_SaveRetrieveDelete()
        {
            // Arrange
            var filePath = "/home/user/documents/report.xlsx";
            var entry1 = CreateValidFileLogEntry(filePath);
            var entry2 = CreateValidFileLogEntry(filePath);

            // Act - Save
            await _service.SaveFileLogAsync(entry1);
            await Task.Delay(1100);
            await _service.SaveFileLogAsync(entry2);

            // Act - Retrieve
            var history = await _service.GetFileLogHistoryAsync(filePath);
            var latest = await _service.GetLatestFileLogAsync(filePath);

            // Assert - Retrieve
            history.Should().NotBeEmpty();
            latest.Should().NotBeNull();

            // Act - Delete
            await _service.DeleteFileLogsAsync(filePath);

            // Assert - Delete
            var afterDelete = await _service.GetFileLogHistoryAsync(filePath);
            afterDelete.Should().BeEmpty();
        }

        [Fact]
        public async Task MultipleFiles_MaintainSeparateLogs()
        {
            // Arrange
            var file1Path = "/home/user/file1.xlsx";
            var file2Path = "/home/user/file2.xlsx";
            var entry1 = CreateValidFileLogEntry(file1Path);
            var entry2 = CreateValidFileLogEntry(file2Path);

            // Act
            await _service.SaveFileLogAsync(entry1);
            await _service.SaveFileLogAsync(entry2);

            // Assert
            var history1 = await _service.GetFileLogHistoryAsync(file1Path);
            var history2 = await _service.GetFileLogHistoryAsync(file2Path);

            history1.Should().NotBeEmpty();
            history2.Should().NotBeEmpty();
            history1[0].File.OriginalPath.Should().Be(file1Path);
            history2[0].File.OriginalPath.Should().Be(file2Path);
        }

        #endregion

        #region Helper Methods

        private static FileLogEntry CreateValidFileLogEntry(string? filePath = null)
        {
            filePath ??= "/home/user/test-file.xlsx";

            return new FileLogEntry
            {
                SchemaVersion = "1.0",
                File = new FileInfoDto
                {
                    Name = Path.GetFileName(filePath),
                    OriginalPath = filePath,
                    SizeBytes = 1024,
                    Hash = "md5:abc123def456",
                    LastModified = DateTime.UtcNow
                },
                LoadAttempt = new LoadAttemptInfo
                {
                    Timestamp = DateTime.UtcNow,
                    Status = "Success",
                    DurationMs = 1500,
                    AppVersion = "1.0.0"
                },
                Errors = new List<ExcelError>(),
                Summary = new ErrorSummary
                {
                    TotalErrors = 0,
                    BySeverity = new Dictionary<string, int>(),
                    ByContext = new Dictionary<string, int>()
                },
                Extensions = new Dictionary<string, object?> { { "customField", "value" } }
            };
        }

        #endregion
    }
}
