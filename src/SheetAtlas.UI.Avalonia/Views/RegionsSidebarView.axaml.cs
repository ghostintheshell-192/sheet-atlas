using Avalonia.Controls;
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
}
