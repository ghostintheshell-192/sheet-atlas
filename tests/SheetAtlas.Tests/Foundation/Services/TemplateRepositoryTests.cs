using FluentAssertions;
using SheetAtlas.Core.Application.Interfaces;
using SheetAtlas.Core.Application.Services.Foundation;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;

namespace SheetAtlas.Tests.Foundation.Services
{
    /// <summary>
    /// Unit tests for ITemplateRepository.
    /// Tests template persistence (save/load/delete).
    /// </summary>
    public class TemplateRepositoryTests : IDisposable
    {
        private readonly string _testDir;
        private readonly ITemplateRepository _repository;

        public TemplateRepositoryTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"SheetAtlasTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _repository = new TemplateRepository(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        #region SaveTemplateAsync Tests

        [Fact]
        public async Task SaveTemplateAsync_NewTemplate_CreatesFile()
        {
            // Arrange
            var template = CreateSampleTemplate("TestTemplate");

            // Act
            var path = await _repository.SaveTemplateAsync(template);

            // Assert
            File.Exists(path).Should().BeTrue();
            path.Should().EndWith(".json");
            _repository.TemplateExists("TestTemplate").Should().BeTrue();
        }

        [Fact]
        public async Task SaveTemplateAsync_ExistingTemplateNoOverwrite_ThrowsException()
        {
            // Arrange
            var template = CreateSampleTemplate("DuplicateTemplate");
            await _repository.SaveTemplateAsync(template);

            // Act & Assert
            var act = async () => await _repository.SaveTemplateAsync(template, overwrite: false);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*already exists*");
        }

        [Fact]
        public async Task SaveTemplateAsync_ExistingTemplateWithOverwrite_Succeeds()
        {
            // Arrange
            var template = CreateSampleTemplate("OverwriteTemplate");
            await _repository.SaveTemplateAsync(template);

            template.Description = "Updated description";

            // Act
            await _repository.SaveTemplateAsync(template, overwrite: true);

            // Assert
            var loaded = await _repository.LoadTemplateAsync("OverwriteTemplate");
            loaded.Should().NotBeNull();
            loaded!.Description.Should().Be("Updated description");
        }

        #endregion

        #region LoadTemplateAsync Tests

        [Fact]
        public async Task LoadTemplateAsync_ExistingTemplate_ReturnsTemplate()
        {
            // Arrange
            var original = CreateSampleTemplate("LoadableTemplate");
            original.Description = "Test Description";
            await _repository.SaveTemplateAsync(original);

            // Act
            var loaded = await _repository.LoadTemplateAsync("LoadableTemplate");

            // Assert
            loaded.Should().NotBeNull();
            loaded!.Name.Should().Be("LoadableTemplate");
            loaded.Description.Should().Be("Test Description");
            loaded.Columns.Should().HaveCount(2);
        }

        [Fact]
        public async Task LoadTemplateAsync_NonExistent_ReturnsNull()
        {
            // Act
            var result = await _repository.LoadTemplateAsync("NonExistentTemplate");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task LoadTemplateAsync_PreservesColumnConfiguration()
        {
            // Arrange
            var original = ExcelTemplate.Create("ColumnConfigTest")
                .AddColumn(ExpectedColumn.RequiredAt("ID", DataType.Number, 0)
                    .WithRules(ValidationRule.Unique(), ValidationRule.Positive()))
                .AddColumn(ExpectedColumn.Currency("Price", "EUR"));

            await _repository.SaveTemplateAsync(original);

            // Act
            var loaded = await _repository.LoadTemplateAsync("ColumnConfigTest");

            // Assert
            loaded.Should().NotBeNull();
            var idColumn = loaded!.Columns.First(c => c.Name == "ID");
            idColumn.Position.Should().Be(0);
            idColumn.IsRequired.Should().BeTrue();
            idColumn.Rules.Should().HaveCount(2);

            var priceColumn = loaded.Columns.First(c => c.Name == "Price");
            priceColumn.ExpectedCurrency.Should().Be("EUR");
        }

        #endregion

        #region DeleteTemplateAsync Tests

        [Fact]
        public async Task DeleteTemplateAsync_ExistingTemplate_DeletesFile()
        {
            // Arrange
            var template = CreateSampleTemplate("ToDelete");
            await _repository.SaveTemplateAsync(template);
            _repository.TemplateExists("ToDelete").Should().BeTrue();

            // Act
            var result = await _repository.DeleteTemplateAsync("ToDelete");

            // Assert
            result.Should().BeTrue();
            _repository.TemplateExists("ToDelete").Should().BeFalse();
        }

        [Fact]
        public async Task DeleteTemplateAsync_NonExistent_ReturnsFalse()
        {
            // Act
            var result = await _repository.DeleteTemplateAsync("NonExistentTemplate");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region ListTemplatesAsync Tests

        [Fact]
        public async Task ListTemplatesAsync_MultipleTemplates_ReturnsAll()
        {
            // Arrange
            await _repository.SaveTemplateAsync(CreateSampleTemplate("Template1"));
            await _repository.SaveTemplateAsync(CreateSampleTemplate("Template2"));
            await _repository.SaveTemplateAsync(CreateSampleTemplate("Template3"));

            // Act
            var list = await _repository.ListTemplatesAsync();

            // Assert
            list.Should().HaveCount(3);
            list.Select(s => s.Name).Should().Contain("Template1", "Template2", "Template3");
        }

        [Fact]
        public async Task ListTemplatesAsync_EmptyDirectory_ReturnsEmptyList()
        {
            // Act
            var list = await _repository.ListTemplatesAsync();

            // Assert
            list.Should().BeEmpty();
        }

        [Fact]
        public async Task ListTemplatesAsync_ReturnsSummaryWithCorrectInfo()
        {
            // Arrange
            var template = CreateSampleTemplate("SummaryTest");
            template.Description = "Summary test description";
            await _repository.SaveTemplateAsync(template);

            // Act
            var list = await _repository.ListTemplatesAsync();

            // Assert
            var summary = list.First();
            summary.Name.Should().Be("SummaryTest");
            summary.Description.Should().Be("Summary test description");
            summary.ColumnCount.Should().Be(2);
            summary.RequiredColumnCount.Should().Be(2);
        }

        #endregion

        #region ImportExportAsync Tests

        [Fact]
        public async Task ImportExportAsync_RoundTrip_PreservesTemplate()
        {
            // Arrange
            var original = CreateSampleTemplate("ImportExportTest");
            var exportPath = Path.Combine(_testDir, "exported_template.json");

            await _repository.SaveTemplateAsync(original);

            // Act - Export
            await _repository.ExportTemplateAsync("ImportExportTest", exportPath);

            // Delete original
            await _repository.DeleteTemplateAsync("ImportExportTest");

            // Act - Import
            var imported = await _repository.ImportTemplateAsync(exportPath);

            // Assert
            imported.Name.Should().Be("ImportExportTest");
            imported.Columns.Should().HaveCount(2);
            _repository.TemplateExists("ImportExportTest").Should().BeTrue();
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void GetTemplatePath_ReturnsValidPath()
        {
            // Act
            var path = _repository.GetTemplatePath("MyTemplate");
            var fileName = Path.GetFileName(path);

            // Assert
            fileName.Should().Be("MyTemplate.json");
            path.Should().StartWith(_testDir);
        }

        [Fact]
        public void GetTemplatePath_WithSpaces_PreservesSpaces()
        {
            // Act
            var path = _repository.GetTemplatePath("My Template Name");
            var fileName = Path.GetFileName(path);

            // Assert
            fileName.Should().Be("My Template Name.json");
        }

        [Fact]
        public async Task SaveTemplateAsync_UpdatesModifiedTimestamp()
        {
            // Arrange
            var template = CreateSampleTemplate("TimestampTest");
            var originalModified = template.ModifiedAt;

            await Task.Delay(10); // Small delay to ensure timestamp difference

            // Act
            await _repository.SaveTemplateAsync(template);

            // Assert
            var loaded = await _repository.LoadTemplateAsync("TimestampTest");
            loaded!.ModifiedAt.Should().BeAfter(originalModified);
        }

        #endregion

        #region Helper Methods

        private static ExcelTemplate CreateSampleTemplate(string name)
        {
            return ExcelTemplate.Create(name)
                .AddColumn(ExpectedColumn.Required("Amount", DataType.Number))
                .AddColumn(ExpectedColumn.Required("Description", DataType.Text));
        }

        #endregion
    }
}
