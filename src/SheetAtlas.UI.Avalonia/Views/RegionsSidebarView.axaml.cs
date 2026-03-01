using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.UI.Avalonia.ViewModels;

// ReSharper disable UnusedParameter.Local

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

    private void OnClearFileClick(object? sender, RoutedEventArgs e)
    {
        var group = (sender as MenuItem)?.DataContext as FileRegionGroup;
        if (group != null && DataContext is RegionsSidebarViewModel vm)
            vm.ClearFileRegionsCommand.Execute(group);
    }

    #region RegionItem ⋮ menu handlers

    private void OnEditRegionMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is RegionItem item
            && DataContext is RegionsSidebarViewModel vm)
            vm.EditRegionCommand.Execute(item);
    }

    private void OnRenameMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is RegionItem item)
        {
            item.IsEditing = true;
            // Focus the TextBox after layout updates; it lives in a DataTemplate
            // so we find it by matching DataContext
            Dispatcher.UIThread.Post(() =>
            {
                this.GetVisualDescendants()
                    .OfType<TextBox>()
                    .FirstOrDefault(tb => tb.DataContext == item && tb.IsVisible)
                    ?.Focus();
            });
        }
    }

    private void OnClearRegionMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is RegionItem item
            && DataContext is RegionsSidebarViewModel vm)
            vm.ClearItemCommand.Execute(item);
    }

    #endregion

    #region Inline rename TextBox handlers

    private void OnRegionEditTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is RegionItem item
            && DataContext is RegionsSidebarViewModel vm)
        {
            if (e.Key == Key.Enter)
            {
                vm.CommitRegionRename(item);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                item.IsEditing = false;
                e.Handled = true;
            }
        }
    }

    private void OnRegionEditTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        // Skip commit if editing was cancelled (e.g. Escape already set IsEditing = false)
        if (sender is TextBox textBox && textBox.DataContext is RegionItem item
            && DataContext is RegionsSidebarViewModel vm
            && item.IsEditing)
            vm.CommitRegionRename(item);
    }

    #endregion
}
