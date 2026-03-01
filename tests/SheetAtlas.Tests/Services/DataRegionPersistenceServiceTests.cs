using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SheetAtlas.Core.Application.DTOs;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Domain.ValueObjects;
using Xunit;

namespace SheetAtlas.Tests.Services
{
    public class DataRegionPersistenceServiceTests : IDisposable
    {
        private readonly Mock<ILogger<DataRegionPersistenceService>> _mockLogger;
        private readonly DataRegionPersistenceService _service;
        private readonly string _testExcelPath;

        public DataRegionPersistenceServiceTests()
        {
            _mockLogger = new Mock<ILogger<DataRegionPersistenceService>>();
            _service = new DataRegionPersistenceService(_mockLogger.Object);
            // Use a unique path per test run to avoid collisions
            _testExcelPath = $"/tmp/test-{Guid.NewGuid()}/report.xlsx";
        }

        public void Dispose()
        {
            // Cleanup: delete any persisted files
            try
            {
                _service.DeleteAsync(_testExcelPath).GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public async Task SaveAsync_CreatesJsonFile()
        {
            // Arrange
            var data = CreateTestRegionFile();

            // Act
            await _service.SaveAsync(_testExcelPath, data);

            // Assert — load should succeed
            var loaded = await _service.LoadAsync(_testExcelPath);
            loaded.Should().NotBeNull();
        }

        [Fact]
        public async Task LoadAsync_ExistingFile_DeserializesCorrectly()
        {
            // Arrange
            var data = CreateTestRegionFile();
            await _service.SaveAsync(_testExcelPath, data);

            // Act
            var loaded = await _service.LoadAsync(_testExcelPath);

            // Assert
            loaded.Should().NotBeNull();
            loaded!.Version.Should().Be(1);
            loaded.Sheets.Should().ContainKey("Sheet1");
            loaded.Sheets["Sheet1"].Regions.Should().ContainKey("MainData");
            var region = loaded.Sheets["Sheet1"].Regions["MainData"];
            region.Name.Should().Be("MainData");
            region.DataStartRow.Should().Be(1);
            region.DataEndRow.Should().Be(100);
            region.StartColumn.Should().Be(0);
            region.EndColumn.Should().Be(5);
        }

        [Fact]
        public async Task LoadAsync_NoFile_ReturnsNull()
        {
            // Act
            var loaded = await _service.LoadAsync("/tmp/nonexistent/file.xlsx");

            // Assert
            loaded.Should().BeNull();
        }

        [Fact]
        public async Task LoadAsync_CorruptedFile_ReturnsNull()
        {
            // Arrange — save valid data first, then corrupt the file
            var data = CreateTestRegionFile();
            await _service.SaveAsync(_testExcelPath, data);

            // Find and corrupt the file
            var loaded = await _service.LoadAsync(_testExcelPath);
            loaded.Should().NotBeNull(); // Verify it works first

            // Corrupt by saving garbage
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var storageRoot = Path.Combine(appDataPath, "SheetAtlas", "DataRegions");
            var folderName = SheetAtlas.Core.Shared.Helpers.FilePathHelper.GenerateLogFolderName(_testExcelPath);
            var filePath = Path.Combine(storageRoot, folderName, "regions.json");
            await File.WriteAllTextAsync(filePath, "{{not valid json!!");

            // Act
            var corruptedResult = await _service.LoadAsync(_testExcelPath);

            // Assert
            corruptedResult.Should().BeNull();
        }

        [Fact]
        public async Task DeleteAsync_RemovesFile()
        {
            // Arrange
            var data = CreateTestRegionFile();
            await _service.SaveAsync(_testExcelPath, data);
            var beforeDelete = await _service.LoadAsync(_testExcelPath);
            beforeDelete.Should().NotBeNull();

            // Act
            await _service.DeleteAsync(_testExcelPath);

            // Assert
            var afterDelete = await _service.LoadAsync(_testExcelPath);
            afterDelete.Should().BeNull();
        }

        [Fact]
        public async Task RoundTrip_SaveThenLoad_PreservesData()
        {
            // Arrange
            var data = new DataRegionFile
            {
                Version = 1,
                LastModified = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc),
                Sheets = new Dictionary<string, SheetRegionsDto>
                {
                    ["Sheet1"] = new SheetRegionsDto
                    {
                        Regions = new Dictionary<string, DataRegion>
                        {
                            ["Header"] = DataRegion.Manual("Header", headerStart: 0, dataStart: 1, dataEnd: 50),
                            ["Footer"] = DataRegion.Manual("Footer", headerStart: 52, dataStart: 53, dataEnd: 60)
                        }
                    },
                    ["Sheet2"] = new SheetRegionsDto
                    {
                        Regions = new Dictionary<string, DataRegion>
                        {
                            ["Full"] = DataRegion.WholeSheet("Full", rowCount: 200, colCount: 10)
                        }
                    }
                }
            };

            // Act
            await _service.SaveAsync(_testExcelPath, data);
            var loaded = await _service.LoadAsync(_testExcelPath);

            // Assert
            loaded.Should().NotBeNull();
            loaded!.Version.Should().Be(1);
            loaded.Sheets.Should().HaveCount(2);
            loaded.Sheets["Sheet1"].Regions.Should().HaveCount(2);
            loaded.Sheets["Sheet1"].Regions["Header"].DataStartRow.Should().Be(1);
            loaded.Sheets["Sheet1"].Regions["Footer"].DataStartRow.Should().Be(53);
            loaded.Sheets["Sheet2"].Regions["Full"].StartColumn.Should().Be(0);
            loaded.Sheets["Sheet2"].Regions["Full"].EndColumn.Should().Be(9);
        }

        private static DataRegionFile CreateTestRegionFile()
        {
            return new DataRegionFile
            {
                Version = 1,
                LastModified = DateTime.UtcNow,
                Sheets = new Dictionary<string, SheetRegionsDto>
                {
                    ["Sheet1"] = new SheetRegionsDto
                    {
                        Regions = new Dictionary<string, DataRegion>
                        {
                            ["MainData"] = new DataRegion
                            {
                                Name = "MainData",
                                HeaderStartRow = 0,
                                DataStartRow = 1,
                                DataEndRow = 100,
                                StartColumn = 0,
                                EndColumn = 5
                            }
                        }
                    }
                }
            };
        }
    }
}
