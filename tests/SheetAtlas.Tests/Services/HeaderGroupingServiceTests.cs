namespace SheetAtlas.Tests.Services
{
    using SheetAtlas.Core.Application.Interfaces;
    using SheetAtlas.Core.Application.Services;
    using SheetAtlas.Core.Application.Services.HeaderResolvers;
    using Xunit;

    public class HeaderGroupingServiceTests
    {
        private readonly IHeaderGroupingService _service;

        public HeaderGroupingServiceTests()
        {
            _service = new HeaderGroupingService();
        }

        [Fact]
        public void GroupHeaders_WithNoResolver_GroupsByOriginalNames()
        {
            // Arrange
            var headers = new List<string> { "Price", "Cost", "Date" };

            // Act
            var result = _service.GroupHeaders(headers);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Contains(result, g => g.DisplayName == "Price" && g.OriginalHeaders.Count == 1);
            Assert.Contains(result, g => g.DisplayName == "Cost" && g.OriginalHeaders.Count == 1);
            Assert.Contains(result, g => g.DisplayName == "Date" && g.OriginalHeaders.Count == 1);
        }

        [Fact]
        public void GroupHeaders_WithResolver_GroupsBySemanticNames()
        {
            // Arrange
            var headers = new List<string> { "Price", "Cost", "Value" };
            var semanticNames = new Dictionary<string, string>
            {
                ["Price"] = "Amount",
                ["Cost"] = "Amount",
                ["Value"] = "Amount"
            };
            var resolver = new DictionaryHeaderResolver(semanticNames);

            // Act
            var result = _service.GroupHeaders(headers, resolver);

            // Assert
            Assert.Single(result);
            Assert.Equal("Amount", result[0].DisplayName);
            Assert.Equal(3, result[0].OriginalHeaders.Count);
            Assert.Contains("Price", result[0].OriginalHeaders);
            Assert.Contains("Cost", result[0].OriginalHeaders);
            Assert.Contains("Value", result[0].OriginalHeaders);
        }

        [Fact]
        public void GroupHeaders_WithPartialMapping_MixesSemanticAndOriginal()
        {
            // Arrange
            var headers = new List<string> { "Price", "Cost", "Date" };
            var semanticNames = new Dictionary<string, string>
            {
                ["Price"] = "Amount",
                ["Cost"] = "Amount"
                // Date has no mapping
            };
            var resolver = new DictionaryHeaderResolver(semanticNames);

            // Act
            var result = _service.GroupHeaders(headers, resolver);

            // Assert
            Assert.Equal(2, result.Count);

            var amountGroup = result.First(g => g.DisplayName == "Amount");
            Assert.Equal(2, amountGroup.OriginalHeaders.Count);
            Assert.Contains("Price", amountGroup.OriginalHeaders);
            Assert.Contains("Cost", amountGroup.OriginalHeaders);

            var dateGroup = result.First(g => g.DisplayName == "Date");
            Assert.Single(dateGroup.OriginalHeaders);
            Assert.Contains("Date", dateGroup.OriginalHeaders);
        }

        [Fact]
        public void GroupHeaders_WithIncludedColumns_FiltersHeaders()
        {
            // Arrange
            var headers = new List<string> { "Price", "Cost", "Date" };
            var includedColumns = new List<string> { "Price", "Date" };

            // Act
            var result = _service.GroupHeaders(headers, null, includedColumns);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, g => g.DisplayName == "Price");
            Assert.Contains(result, g => g.DisplayName == "Date");
            Assert.DoesNotContain(result, g => g.DisplayName == "Cost");
        }

        [Fact]
        public void GroupHeaders_WithIncludedColumnsAndResolver_FiltersAndGroups()
        {
            // Arrange
            var headers = new List<string> { "Price", "Cost", "Value", "Date" };
            var semanticNames = new Dictionary<string, string>
            {
                ["Price"] = "Amount",
                ["Cost"] = "Amount",
                ["Value"] = "Amount"
            };
            var resolver = new DictionaryHeaderResolver(semanticNames);
            var includedColumns = new List<string> { "Price", "Cost" }; // Exclude Value and Date

            // Act
            var result = _service.GroupHeaders(headers, resolver, includedColumns);

            // Assert
            Assert.Single(result);
            Assert.Equal("Amount", result[0].DisplayName);
            Assert.Equal(2, result[0].OriginalHeaders.Count);
            Assert.Contains("Price", result[0].OriginalHeaders);
            Assert.Contains("Cost", result[0].OriginalHeaders);
            Assert.DoesNotContain("Value", result[0].OriginalHeaders);
        }

        [Fact]
        public void GroupHeaders_WithCaseInsensitiveIncludedColumns_Filters()
        {
            // Arrange
            var headers = new List<string> { "Price", "Cost", "Date" };
            var includedColumns = new List<string> { "price", "DATE" }; // Different case

            // Act
            var result = _service.GroupHeaders(headers, null, includedColumns);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, g => g.DisplayName == "Price");
            Assert.Contains(result, g => g.DisplayName == "Date");
        }

        [Fact]
        public void GroupHeaders_WithEmptyHeaders_ReturnsEmptyList()
        {
            // Arrange
            var headers = new List<string>();

            // Act
            var result = _service.GroupHeaders(headers);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void GroupHeaders_WithNullHeaders_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _service.GroupHeaders(null!));
        }

        [Fact]
        public void GroupHeaders_WithFunctionResolver_UsesFunction()
        {
            // Arrange
            var headers = new List<string> { "Rev 2023", "Rev 2024", "Cost" };
            string? ResolverFunc(string header) => header.StartsWith("Rev") ? "Revenue" : null;
            var resolver = new FunctionHeaderResolver(ResolverFunc);

            // Act
            var result = _service.GroupHeaders(headers, resolver);

            // Assert
            Assert.Equal(2, result.Count);

            var revenueGroup = result.First(g => g.DisplayName == "Revenue");
            Assert.Equal(2, revenueGroup.OriginalHeaders.Count);
            Assert.Contains("Rev 2023", revenueGroup.OriginalHeaders);
            Assert.Contains("Rev 2024", revenueGroup.OriginalHeaders);

            var costGroup = result.First(g => g.DisplayName == "Cost");
            Assert.Single(costGroup.OriginalHeaders);
        }
    }
}
