using Avalonia.Controls;
using Avalonia.Input;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Views;

public partial class ColumnsSidebarView : UserControl
{
    public ColumnsSidebarView()
    {
        InitializeComponent();
    }

    private void OnExpandCollapseClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Panel panel && panel.DataContext is ColumnLinkViewModel vm)
        {
            vm.IsExpanded = !vm.IsExpanded;
        }
    }
}
