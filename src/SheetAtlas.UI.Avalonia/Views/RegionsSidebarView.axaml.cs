using Avalonia.Controls;
using Avalonia.Input;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Views;

public partial class RegionsSidebarView : UserControl
{
    public RegionsSidebarView()
    {
        InitializeComponent();
    }

    private void OnTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not RegionsSidebarViewModel vm) return;

        var selectedItem = (sender as TreeView)?.SelectedItem;
        vm.SelectedRegion = selectedItem as RegionItem;
    }

    private void OnRegionGroupCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not RegionsSidebarViewModel vm) return;

        var source = sender as Control;
        var group = source?.DataContext as RegionNameGroup;
        if (group == null) return;

        // Toggle selection: click again to deselect
        if (vm.SelectedRegionGroup == group)
        {
            group.IsSelected = false;
            vm.SelectedRegionGroup = null;
        }
        else
        {
            // Deselect previous
            if (vm.SelectedRegionGroup != null)
                vm.SelectedRegionGroup.IsSelected = false;

            group.IsSelected = true;
            group.IsExpanded = true;
            vm.SelectedRegionGroup = group;
        }
    }

    private void OnExpandCollapseClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;

        // Walk up to find the RegionNameGroup
        var group = control.DataContext as RegionNameGroup;
        if (group == null) return;

        group.IsExpanded = !group.IsExpanded;
        e.Handled = true; // Prevent card selection
    }
}
