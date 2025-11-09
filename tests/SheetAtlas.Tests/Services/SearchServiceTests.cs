using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Services;
using Moq;
using FluentAssertions;

namespace SheetAtlas.Tests.Services
{
    public class SearchServiceTests
    {
        private readonly Mock<ILogService> _mockLogger;
        private readonly SearchService _searchService;
        private static readonly string[] _columnNames = new[] { "A" };

        public SearchServiceTests()
        {
            _mockLogger = new Mock<ILogService>();
            _searchService = new SearchService(_mockLogger.Object);
        }

        #region Regex Tests

        [Theory]
        [InlineData("test123", @"test\d+", true)]
        [InlineData("test", @"test\d+", false)]
        [InlineData("StartHere", "^Start", true)]
        [InlineData("NotStart", "^Start", false)]
        [InlineData("EndHere", "Here$", true)]
        [InlineData("HereNot", "Here$", false)]
        [InlineData("abc123def", @"\d+", true)]
        [InlineData("abcdef", @"\d+", false)]
        public void Search_WithRegexPattern_MatchesCorrectly(string cellValue, string pattern, bool shouldMatch)
        {
            // Arrange
            var file = CreateTestFile("data.xlsx", "Sheet1", cellValue);  // Filename won't match patterns
            var options = new SearchOptions { UseRegex = true, CaseSensitive = true };

            // Act
            var results = _searchService.Search(file, pattern, options);

            // Assert
            var cellMatches = results.Where(r => r.Row >= 0).ToList();  // Filter out filename matches
            if (shouldMatch)
            {
                cellMatches.Should().HaveCount(1);
                cellMatches.First().Value.Should().Be(cellValue);
            }
            else
            {
                cellMatches.Should().BeEmpty();
            }
        }

        [Theory]
        [InlineData("Test", "test", true)]  // Case insensitive
        [InlineData("TEST", "test", true)]  // Case insensitive
        [InlineData("TeSt", "test", true)]  // Case insensitive
        public void Search_WithRegexCaseInsensitive_MatchesIgnoringCase(string cellValue, string pattern, bool shouldMatch)
        {
            // Arrange
            var file = CreateTestFile("data.xlsx", "Sheet1", cellValue);
            var options = new SearchOptions { UseRegex = true, CaseSensitive = false };

            // Act
            var results = _searchService.Search(file, pattern, options);

            // Assert
            var cellMatches = results.Where(r => r.Row >= 0).ToList();
            if (shouldMatch)
            {
                cellMatches.Should().HaveCountGreaterThanOrEqualTo(1);
            }
            else
            {
                cellMatches.Should().BeEmpty();
            }
        }

        [Theory]
        [InlineData("Test", "test", false)]  // Case sensitive - should NOT match
        [InlineData("TEST", "TEST", true)]   // Case sensitive - exact match
        [InlineData("test", "test", true)]   // Case sensitive - exact match
        public void Search_WithRegexCaseSensitive_MatchesExactCase(string cellValue, string pattern, bool shouldMatch)
        {
            // Arrange
            var file = CreateTestFile("data.xlsx", "Sheet1", cellValue);
            var options = new SearchOptions { UseRegex = true, CaseSensitive = true };

            // Act
            var results = _searchService.Search(file, pattern, options);

            // Assert
            var cellMatches = results.Where(r => r.Row >= 0).ToList();
            if (shouldMatch)
            {
                cellMatches.Should().HaveCountGreaterThanOrEqualTo(1);
            }
            else
            {
                cellMatches.Should().BeEmpty();
            }
        }

        [Fact]
        public void Search_WithInvalidRegexPattern_FallsBackToContainsSearch()
        {
            // Arrange
            var file = CreateTestFile("data.xlsx", "Sheet1", "test[value");
            var invalidPattern = "test[";  // Invalid regex - unclosed bracket
            var options = new SearchOptions { UseRegex = true };

            // Act
            var results = _searchService.Search(file, invalidPattern, options);

            // Assert
            // Should fallback to Contains search and find the match
            var cellMatches = results.Where(r => r.Row >= 0).ToList();
            cellMatches.Should().HaveCount(1);
            cellMatches.First().Value.Should().Be("test[value");
        }

        [Theory]
        [InlineData(@".*.*.*.*.*a", "aaaaaaa")]  // Catastrophic backtracking pattern
        public void Search_WithComplexRegexCausingTimeout_FallsBackGracefully(string pattern, string cellValue)
        {
            // Arrange
            var file = CreateTestFile("data.xlsx", "Sheet1", cellValue);
            var options = new SearchOptions { UseRegex = true };

            // Act
            var results = _searchService.Search(file, pattern, options);

            // Assert
            // Should either timeout and fallback, or complete successfully
            // Either way, should not throw exception
            results.Should().NotBeNull();
        }

        [Theory]
        [InlineData("email@example.com", @"[\w\.-]+@[\w\.-]+\.\w+", true)]  // Email regex
        [InlineData("not-an-email", @"[\w\.-]+@[\w\.-]+\.\w+", false)]
        [InlineData("192.168.1.1", @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}", true)]  // IP regex
        [InlineData("invalid.ip", @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}", false)]
        public void Search_WithCommonRegexPatterns_WorksCorrectly(string cellValue, string pattern, bool shouldMatch)
        {
            // Arrange
            var file = CreateTestFile("data.xlsx", "Sheet1", cellValue);
            var options = new SearchOptions { UseRegex = true };

            // Act
            var results = _searchService.Search(file, pattern, options);

            // Assert
            var cellMatches = results.Where(r => r.Row >= 0).ToList();
            if (shouldMatch)
            {
                cellMatches.Should().HaveCountGreaterThanOrEqualTo(1);
            }
            else
            {
                cellMatches.Should().BeEmpty();
            }
        }

        #endregion

        #region Helper Methods

        private static ExcelFile CreateTestFile(string fileName, string sheetName, string cellValue)
        {
            var sheet = new SASheetData(sheetName, _columnNames);
            var rowData = new SACellData[]
            {
                new SACellData(SACellValue.FromText(cellValue))
            };
            sheet.AddRow(rowData);

            var sheets = new Dictionary<string, SASheetData> { { sheetName, sheet } };

            return new ExcelFile(
                filePath: fileName,
                status: LoadStatus.Success,
                sheets: sheets,
                errors: new List<ExcelError>()
            );
        }

        #endregion
    }
}
