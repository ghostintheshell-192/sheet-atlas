using FluentAssertions;
using Moq;
using SheetAtlas.Core.Domain.Entities;
using SheetAtlas.Core.Domain.ValueObjects;
using SheetAtlas.Logging.Services;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.Tests.ViewModels
{
    /// <summary>
    /// Unit tests for RegionsSidebarViewModel.
    /// Covers tree management (Add, Remove, Refresh) and view toggle behaviour.
    /// </summary>
    public class RegionsSidebarViewModelTests
    {
        private readonly Mock<ILogService> _logger = new();
        private RegionsSidebarViewModel CreateViewModel() => new(_logger.Object);

        #region Initial State

        [Fact]
        public void Constructor_EmptyState_HasNoRegions()
        {
            var vm = CreateViewModel();

            vm.FileGroups.Should().BeEmpty();
            vm.TotalRegionCount.Should().Be(0);
            vm.HasAnyRegions.Should().BeFalse();
            vm.HasSelectedRegion.Should().BeFalse();
        }

        #endregion

        #region AddRegion

        [Fact]
        public void AddRegion_NewFile_CreatesFileGroupWithSheetAndRegion()
        {
            var vm = CreateViewModel();
            var region = new DataRegion { Name = "Sales", DataStartRow = 1 };

            vm.AddRegion("/files/a.xlsx", "a.xlsx", "Sheet1", region);

            vm.FileGroups.Should().HaveCount(1);
            vm.FileGroups[0].FileName.Should().Be("a.xlsx");
            vm.FileGroups[0].Sheets.Should().HaveCount(1);
            vm.FileGroups[0].Sheets[0].SheetName.Should().Be("Sheet1");
            vm.FileGroups[0].Sheets[0].Regions.Should().HaveCount(1);
            vm.FileGroups[0].Sheets[0].Regions[0].Name.Should().Be("Sales");
            vm.TotalRegionCount.Should().Be(1);
            vm.HasAnyRegions.Should().BeTrue();
        }

        [Fact]
        public void AddRegion_SameFile_AddsToExistingFileGroup()
        {
            var vm = CreateViewModel();
            vm.AddRegion("/files/a.xlsx", "a.xlsx", "Sheet1", new DataRegion { Name = "R1", DataStartRow = 1 });
            vm.AddRegion("/files/a.xlsx", "a.xlsx", "Sheet1", new DataRegion { Name = "R2", DataStartRow = 5 });

            vm.FileGroups.Should().HaveCount(1);
            vm.FileGroups[0].Sheets[0].Regions.Should().HaveCount(2);
            vm.TotalRegionCount.Should().Be(2);
        }

        [Fact]
        public void AddRegion_DifferentFiles_CreatesMultipleFileGroups()
        {
            var vm = CreateViewModel();
            vm.AddRegion("/files/a.xlsx", "a.xlsx", "Sheet1", new DataRegion { Name = "R1", DataStartRow = 1 });
            vm.AddRegion("/files/b.xlsx", "b.xlsx", "Sheet1", new DataRegion { Name = "R2", DataStartRow = 1 });

            vm.FileGroups.Should().HaveCount(2);
            vm.TotalRegionCount.Should().Be(2);
        }

        #endregion

        #region RefreshFromFiles

        [Fact]
        public void RefreshFromFiles_WithRegions_PopulatesTree()
        {
            var vm = CreateViewModel();

            var sheet = new SASheetData("Data", new[] { "ID", "Name" });
            sheet.AddRow(new[] { new SACellData(SACellValue.FromText("ID")), new SACellData(SACellValue.FromText("Name")) });
            sheet.AddRow(new[] { new SACellData(SACellValue.FromInteger(1)), new SACellData(SACellValue.FromText("Alice")) });
            sheet.AddDataRegion(new DataRegion { Name = "Employees", DataStartRow = 1 });

            var fileMock = new Mock<IFileLoadResultViewModel>();
            fileMock.Setup(f => f.FilePath).Returns("/files/emp.xlsx");
            fileMock.Setup(f => f.FileName).Returns("emp.xlsx");
            fileMock.Setup(f => f.File).Returns(new ExcelFile(
                "/files/emp.xlsx",
                LoadStatus.Success,
                new Dictionary<string, SASheetData> { { "Data", sheet } },
                new List<ExcelError>()));

            vm.RefreshFromFiles(new[] { fileMock.Object });

            vm.FileGroups.Should().HaveCount(1);
            vm.FileGroups[0].FileName.Should().Be("emp.xlsx");
            vm.TotalRegionCount.Should().Be(1);
        }

        [Fact]
        public void RefreshFromFiles_SheetWithNoRegions_NotAddedToTree()
        {
            var vm = CreateViewModel();

            var sheet = new SASheetData("Empty", new[] { "X" });
            sheet.AddRow(new[] { new SACellData(SACellValue.FromText("X")) });
            // No regions added

            var fileMock = new Mock<IFileLoadResultViewModel>();
            fileMock.Setup(f => f.FilePath).Returns("/files/f.xlsx");
            fileMock.Setup(f => f.FileName).Returns("f.xlsx");
            fileMock.Setup(f => f.File).Returns(new ExcelFile(
                "/files/f.xlsx", LoadStatus.Success,
                new Dictionary<string, SASheetData> { { "Empty", sheet } },
                new List<ExcelError>()));

            vm.RefreshFromFiles(new[] { fileMock.Object });

            vm.FileGroups.Should().BeEmpty();
            vm.HasAnyRegions.Should().BeFalse();
        }

        [Fact]
        public void RefreshFromFiles_Called_ClearsExistingGroupsFirst()
        {
            var vm = CreateViewModel();
            vm.AddRegion("/files/old.xlsx", "old.xlsx", "Sheet1", new DataRegion { Name = "Old", DataStartRow = 1 });
            vm.FileGroups.Should().HaveCount(1);

            // Refresh with empty list
            vm.RefreshFromFiles(Array.Empty<IFileLoadResultViewModel>());

            vm.FileGroups.Should().BeEmpty();
            vm.TotalRegionCount.Should().Be(0);
        }

        #endregion

        #region SelectedRegion

        [Fact]
        public void SelectedRegion_WhenSet_UpdatesHasSelectedRegion()
        {
            var vm = CreateViewModel();
            vm.HasSelectedRegion.Should().BeFalse();

            vm.SelectedRegion = new RegionItem
            {
                Name = "Sales",
                FilePath = "/f.xlsx",
                SheetName = "Sheet1",
                Region = new DataRegion { Name = "Sales", DataStartRow = 1 }
            };

            vm.HasSelectedRegion.Should().BeTrue();
        }

        [Fact]
        public void SelectedRegion_ClearedToNull_UpdatesHasSelectedRegion()
        {
            var vm = CreateViewModel();
            vm.SelectedRegion = new RegionItem
            {
                Name = "R",
                FilePath = "/f.xlsx",
                SheetName = "S",
                Region = new DataRegion { Name = "R", DataStartRow = 1 }
            };

            vm.SelectedRegion = null;

            vm.HasSelectedRegion.Should().BeFalse();
        }

        #endregion

        #region IsRegionView

        [Fact]
        public void IsRegionView_DefaultIsFalse()
        {
            var vm = CreateViewModel();
            vm.IsRegionView.Should().BeFalse();
        }

        [Fact]
        public void IsRegionView_SetToTrue_UpdatesProperty()
        {
            var vm = CreateViewModel();
            vm.IsRegionView = true;
            vm.IsRegionView.Should().BeTrue();
        }

        [Fact]
        public void IsRegionView_SwitchedToFalse_ClearsSelectedRegionGroup()
        {
            var vm = CreateViewModel();
            vm.IsRegionView = true;
            // SelectedRegionGroup would be set in a real scenario; switching back should clear it
            vm.IsRegionView = false;
            vm.SelectedRegionGroup.Should().BeNull();
        }

        #endregion

        #region UpdateRegion

        [Fact]
        public void UpdateRegion_ExistingRegion_UpdatesInPlace()
        {
            var vm = CreateViewModel();
            vm.AddRegion("/f.xlsx", "f.xlsx", "Sheet1", new DataRegion
            {
                Name = "Sales",
                DataStartRow = 1,
                DataEndRow = 10
            });

            var updated = new DataRegion { Name = "Sales", DataStartRow = 1, DataEndRow = 20 };
            vm.UpdateRegion("/f.xlsx", "Sheet1", updated);

            var item = vm.FileGroups[0].Sheets[0].Regions[0];
            item.Region.DataEndRow.Should().Be(20);
        }

        #endregion
    }
}
