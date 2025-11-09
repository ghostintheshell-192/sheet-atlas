using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Application.Services.Foundation;
using SheetAtlas.Core.Domain.Exceptions;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Infrastructure.External;
using SheetAtlas.Infrastructure.External.Readers;
using FluentAssertions;
using Moq;
using SheetAtlas.Logging.Models;
using SheetAtlas.Logging.Services;
using SheetAtlas.Core.Configuration;
using Microsoft.Extensions.Options;
using DocumentFormat.OpenXml.Packaging;

namespace SheetAtlas.Tests.Services
{
    public class ExceptionHandlerTests
    {
        private readonly Mock<ILogService> _mockLogger;
        private readonly ExceptionHandler _handler;

        public ExceptionHandlerTests()
        {
            _mockLogger = new Mock<ILogService>();
            _handler = new ExceptionHandler(_mockLogger.Object);
        }

        [Fact]
        public void Handle_ComparisonException_ReturnsCriticalError()
        {
            // Arrange
            var exception = ComparisonException.NoCommonColumns();
            var context = "CompareFiles";

            // Act
            var result = _handler.Handle(exception, context);

            // Assert
            result.Should().NotBeNull();
            result.Level.Should().Be(LogSeverity.Critical);
            result.Message.Should().Contain("common columns");
        }

        [Fact]
        public void Handle_FileNotFoundException_ReturnsUserFriendlyMessage()
        {
            // Arrange
            var exception = new FileNotFoundException("File not found", "test.xlsx");
            var context = "LoadFile";

            // Act
            var result = _handler.Handle(exception, context);

            // Assert
            result.Should().NotBeNull();
            result.Level.Should().Be(LogSeverity.Critical);
            result.Message.Should().Contain("File not found");
            result.Message.Should().Contain("test.xlsx");
        }

        [Fact]
        public void Handle_UnauthorizedAccessException_ReturnsAccessDeniedMessage()
        {
            // Arrange
            var exception = new UnauthorizedAccessException("Access denied");
            var context = "LoadFile";

            // Act
            var result = _handler.Handle(exception, context);

            // Assert
            result.Should().NotBeNull();
            result.Level.Should().Be(LogSeverity.Critical);
            result.Message.Should().Contain("access denied");
            result.Message.Should().Contain("permissions");
        }

        [Fact]
        public void Handle_IOException_ReturnsFileReadError()
        {
            // Arrange
            var exception = new IOException("IO error occurred");
            var context = "LoadFile";

            // Act
            var result = _handler.Handle(exception, context);

            // Assert
            result.Should().NotBeNull();
            result.Level.Should().Be(LogSeverity.Critical);
            result.Message.Should().Contain("File reading error");
        }

        [Fact]
        public void Handle_GenericException_ReturnsFallbackMessage()
        {
            // Arrange
            var exception = new InvalidOperationException("Something went wrong");
            var context = "ProcessData";

            // Act
            var result = _handler.Handle(exception, context);

            // Assert
            result.Should().NotBeNull();
            result.Level.Should().Be(LogSeverity.Critical);
            result.Message.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void GetUserMessage_FileNotFoundException_ReturnsGenericMessage()
        {
            // Arrange
            var exception = new FileNotFoundException();

            // Act
            var message = _handler.GetUserMessage(exception);

            // Assert
            message.Should().Be("File not found");
        }

        [Fact]
        public void IsRecoverable_ArgumentNullException_ReturnsFalse()
        {
            // Arrange
            var exception = new ArgumentNullException("param");

            // Act
            var isRecoverable = _handler.IsRecoverable(exception);

            // Assert
            isRecoverable.Should().BeFalse();
        }

        [Fact]
        public void IsRecoverable_NullReferenceException_ReturnsFalse()
        {
            // Arrange
            var exception = new NullReferenceException();

            // Act
            var isRecoverable = _handler.IsRecoverable(exception);

            // Assert
            isRecoverable.Should().BeFalse();
        }

        #region Integration Tests with Real File Scenarios

        [Fact]
        public async Task Handle_RealNonExistentFile_HandlesFileNotFoundCorrectly()
        {
            // Arrange
            var excelReaderService = CreateRealExcelReaderService();
            var nonExistentPath = Path.Combine(GetTestDataPath(), "NonExistent", "missing.xlsx");

            // Act
            var result = await excelReaderService.LoadFileAsync(nonExistentPath);

            // The service returns ExcelFile with errors instead of throwing
            // Now handle those errors with ExceptionHandler
            var firstError = result.Errors.FirstOrDefault();

            // Assert
            result.Status.Should().Be(LoadStatus.Failed);
            result.Errors.Should().NotBeEmpty();
            firstError.Should().NotBeNull();
            firstError!.Level.Should().Be(LogSeverity.Critical);
        }

        [Fact]
        public async Task Handle_RealCorruptedFile_HandlesCorruptionCorrectly()
        {
            // Arrange
            var excelReaderService = CreateRealExcelReaderService();
            var corruptedPath = GetTestFilePath("Invalid", "corrupted.xlsx");

            // Act
            var result = await excelReaderService.LoadFileAsync(corruptedPath);

            // Assert
            result.Status.Should().Be(LoadStatus.Failed);
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e => e.Level == LogSeverity.Critical);

            // Test that ExceptionHandler would handle this correctly
            var firstError = result.Errors.First();
            if (firstError.InnerException != null)
            {
                var handledError = _handler.Handle(firstError.InnerException, "LoadFile");
                handledError.Level.Should().Be(LogSeverity.Critical);
            }
        }

        [Fact]
        public async Task Handle_RealUnsupportedFormat_HandlesFormatErrorCorrectly()
        {
            // Arrange
            var excelReaderService = CreateRealExcelReaderService();
            var unsupportedPath = GetTestFilePath("Invalid", "unsupported.xls");

            // Act
            var result = await excelReaderService.LoadFileAsync(unsupportedPath);

            // Assert
            result.Status.Should().Be(LoadStatus.Failed);
            result.Errors.Should().NotBeEmpty();
            result.Errors.Should().Contain(e =>
                e.Level == LogSeverity.Critical &&
                e.Message.Contains("format"));
        }

        [Fact]
        public void Handle_RealIOException_ReturnsUserFriendlyMessage()
        {
            // Arrange
            var ioException = new IOException("The process cannot access the file because it is being used by another process");
            var context = "LoadFile";

            // Act
            var result = _handler.Handle(ioException, context);

            // Assert
            result.Should().NotBeNull();
            result.Level.Should().Be(LogSeverity.Critical);
            result.Message.Should().Contain("File reading error");
            result.InnerException.Should().Be(ioException);
        }

        [Fact]
        public void Handle_RealUnauthorizedAccessException_ReturnsPermissionDeniedMessage()
        {
            // Arrange
            var unauthorizedException = new UnauthorizedAccessException("Access to the path is denied");
            var context = "LoadFile";

            // Act
            var result = _handler.Handle(unauthorizedException, context);

            // Assert
            result.Should().NotBeNull();
            result.Level.Should().Be(LogSeverity.Critical);
            result.Message.Should().Contain("access denied");
            result.Message.Should().Contain("permissions");
            result.InnerException.Should().Be(unauthorizedException);
        }

        #endregion

        #region Helper Methods

        private IExcelReaderService CreateRealExcelReaderService()
        {
            var serviceLogger = new Mock<ILogService>();
            var readerLogger = new Mock<ILogService>();
            var cellParser = new CellReferenceParser();
            var cellValueReader = new CellValueReader();
            var mergedRangeExtractor = new OpenXmlMergedRangeExtractor(cellParser);

            // Foundation services (real implementations for integration tests)
            var currencyDetector = new CurrencyDetector();
            var normalizationService = new DataNormalizationService();
            var columnAnalysisService = new ColumnAnalysisService(currencyDetector);
            var mergedCellResolver = new MergedCellResolver();

            // Create orchestrator (with MergedCellResolver as first parameter)
            var orchestrator = new SheetAnalysisOrchestrator(mergedCellResolver, columnAnalysisService, normalizationService, readerLogger.Object);

            // Create OpenXmlFileReader with orchestrator
            var openXmlReader = new OpenXmlFileReader(
                readerLogger.Object,
                cellParser,
                mergedRangeExtractor,
                cellValueReader,
                orchestrator);
            var readers = new List<IFileFormatReader> { openXmlReader };

            // Create settings mock
            var settings = new AppSettings
            {
                Performance = new PerformanceSettings { MaxConcurrentFileLoads = 5 }
            };
            var settingsMock = new Mock<IOptions<AppSettings>>();
            settingsMock.Setup(s => s.Value).Returns(settings);

            return new ExcelReaderService(readers, serviceLogger.Object, settingsMock.Object);
        }

        private static string GetTestDataPath()
        {
            return Path.Combine(
                Directory.GetCurrentDirectory(),
                "..",
                "..",
                "..",
                "TestData"
            );
        }

        private static string GetTestFilePath(string category, string filename)
        {
            var path = Path.Combine(GetTestDataPath(), category, filename);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Test file not found: {path}. Make sure TestData files are generated.");
            }

            return path;
        }

        #endregion
    }
}
