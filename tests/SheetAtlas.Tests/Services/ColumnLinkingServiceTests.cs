using FluentAssertions;
using SheetAtlas.Core.Application.Services;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Tests.Services;

/// <summary>
/// Unit tests for ColumnLinkingService.
/// Tests column grouping behavior and warning detection.
/// </summary>
public class ColumnLinkingServiceTests
{
    private readonly ColumnLinkingService _service = new();

    #region Grouping Tests

    [Fact]
    public void CreateInitialGroups_SameNameDifferentType_GroupsTogether()
    {
        // Arrange - same column name with different detected types
        var columns = new[]
        {
            new ColumnInfo("Amount", DataType.Currency, "FileA.xlsx"),
            new ColumnInfo("Amount", DataType.Number, "FileB.xlsx"),
            new ColumnInfo("Amount", DataType.Unknown, "FileC.xlsx")
        };

        // Act
        var result = _service.CreateInitialGroups(columns);

        // Assert - should be ONE group, not three
        result.Should().HaveCount(1);
        result[0].LinkedColumns.Should().HaveCount(3);
        result[0].SemanticName.Should().Be("Amount");
    }

    [Fact]
    public void CreateInitialGroups_SameNameDifferentCase_GroupsTogether()
    {
        // Arrange - same column name with different casing
        var columns = new[]
        {
            new ColumnInfo("EBIT", DataType.Number, "FileA.xlsx"),
            new ColumnInfo("ebit", DataType.Number, "FileB.xlsx"),
            new ColumnInfo("Ebit", DataType.Number, "FileC.xlsx")
        };

        // Act
        var result = _service.CreateInitialGroups(columns);

        // Assert - should be ONE group
        result.Should().HaveCount(1);
        result[0].LinkedColumns.Should().HaveCount(3);
    }

    [Fact]
    public void CreateInitialGroups_DifferentNames_SeparateGroups()
    {
        // Arrange - different column names
        var columns = new[]
        {
            new ColumnInfo("Revenue", DataType.Number, "FileA.xlsx"),
            new ColumnInfo("Expenses", DataType.Number, "FileA.xlsx"),
            new ColumnInfo("Profit", DataType.Number, "FileA.xlsx")
        };

        // Act
        var result = _service.CreateInitialGroups(columns);

        // Assert - should be THREE separate groups
        result.Should().HaveCount(3);
    }

    [Fact]
    public void CreateInitialGroups_DominantType_MostFrequent()
    {
        // Arrange - Currency appears twice, Number once
        var columns = new[]
        {
            new ColumnInfo("Amount", DataType.Currency, "FileA.xlsx"),
            new ColumnInfo("Amount", DataType.Currency, "FileB.xlsx"),
            new ColumnInfo("Amount", DataType.Number, "FileC.xlsx")
        };

        // Act
        var result = _service.CreateInitialGroups(columns);

        // Assert - dominant type should be Currency (most frequent)
        result.Should().HaveCount(1);
        result[0].DominantType.Should().Be(DataType.Currency);
    }

    #endregion

    #region Warning Detection Tests

    [Fact]
    public void HasCaseVariations_SameNameDifferentCase_ReturnsTrue()
    {
        // Arrange
        var columns = new[]
        {
            new ColumnInfo("EBIT", DataType.Number, "FileA.xlsx"),
            new ColumnInfo("ebit", DataType.Number, "FileB.xlsx")
        };

        // Act
        var result = _service.CreateInitialGroups(columns);

        // Assert
        result.Should().HaveCount(1);
        result[0].HasCaseVariations.Should().BeTrue();
        result[0].HasWarnings.Should().BeTrue();
        result[0].WarningMessage.Should().Contain("Case variations");
    }

    [Fact]
    public void HasCaseVariations_SameNameSameCase_ReturnsFalse()
    {
        // Arrange - identical names
        var columns = new[]
        {
            new ColumnInfo("EBIT", DataType.Number, "FileA.xlsx"),
            new ColumnInfo("EBIT", DataType.Number, "FileB.xlsx")
        };

        // Act
        var result = _service.CreateInitialGroups(columns);

        // Assert
        result.Should().HaveCount(1);
        result[0].HasCaseVariations.Should().BeFalse();
    }

    [Fact]
    public void HasCaseVariations_DifferentNamesAfterMerge_ReturnsFalse()
    {
        // Arrange - simulate manual merge of intentionally different names
        // This is what happens when user groups "2016 VAR" with "2017 VAR"
        var linkedColumns = new[]
        {
            LinkedColumn.Create("2016 YEAR VAR[%]", DataType.Number, "FileA.xlsx"),
            LinkedColumn.Create("2017 YEAR VAR[%]", DataType.Number, "FileA.xlsx"),
            LinkedColumn.Create("2018 YEAR VAR[%]", DataType.Number, "FileA.xlsx")
        };

        var link = ColumnLink.FromGroup("Year Variations", DataType.Number, linkedColumns);

        // Assert - these are intentionally different names, NOT case variations
        link.HasCaseVariations.Should().BeFalse();
    }

    [Fact]
    public void HasTypeVariations_DifferentTypes_ReturnsTrue()
    {
        // Arrange
        var columns = new[]
        {
            new ColumnInfo("Amount", DataType.Currency, "FileA.xlsx"),
            new ColumnInfo("Amount", DataType.Number, "FileB.xlsx")
        };

        // Act
        var result = _service.CreateInitialGroups(columns);

        // Assert
        result.Should().HaveCount(1);
        result[0].HasTypeVariations.Should().BeTrue();
        result[0].HasWarnings.Should().BeTrue();
        result[0].WarningMessage.Should().Contain("Type variations");
    }

    [Fact]
    public void HasTypeVariations_SameType_ReturnsFalse()
    {
        // Arrange
        var columns = new[]
        {
            new ColumnInfo("Amount", DataType.Number, "FileA.xlsx"),
            new ColumnInfo("Amount", DataType.Number, "FileB.xlsx")
        };

        // Act
        var result = _service.CreateInitialGroups(columns);

        // Assert
        result.Should().HaveCount(1);
        result[0].HasTypeVariations.Should().BeFalse();
    }

    [Fact]
    public void HasWarnings_BothVariations_CombinedMessage()
    {
        // Arrange - both case and type variations
        var columns = new[]
        {
            new ColumnInfo("EBIT", DataType.Currency, "FileA.xlsx"),
            new ColumnInfo("ebit", DataType.Number, "FileB.xlsx")
        };

        // Act
        var result = _service.CreateInitialGroups(columns);

        // Assert
        result.Should().HaveCount(1);
        result[0].HasCaseVariations.Should().BeTrue();
        result[0].HasTypeVariations.Should().BeTrue();
        result[0].HasWarnings.Should().BeTrue();
        result[0].WarningMessage.Should().Contain("Case variations");
        result[0].WarningMessage.Should().Contain("Type variations");
    }

    #endregion

    #region Single Column Tests

    [Fact]
    public void HasWarnings_SingleColumn_ReturnsFalse()
    {
        // Arrange - single column cannot have variations
        var columns = new[]
        {
            new ColumnInfo("Amount", DataType.Number, "FileA.xlsx")
        };

        // Act
        var result = _service.CreateInitialGroups(columns);

        // Assert
        result.Should().HaveCount(1);
        result[0].HasCaseVariations.Should().BeFalse();
        result[0].HasTypeVariations.Should().BeFalse();
        result[0].HasWarnings.Should().BeFalse();
    }

    #endregion
}
