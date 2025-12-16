using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Views;

public partial class ColumnsSidebarView : UserControl
{
    private const string DragDataFormat = "ColumnLinkViewModel";
    private Border? _highlightedBorder;
    private IBrush? _originalBorderBrush;

    public ColumnsSidebarView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnExpandCollapseClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Panel panel && panel.DataContext is ColumnLinkViewModel vm)
        {
            vm.IsExpanded = !vm.IsExpanded;
        }
    }

    private async void OnColumnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only start drag with left button
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (sender is Border border && border.DataContext is ColumnLinkViewModel vm)
        {
            // Check if click is on expand/collapse panel (don't drag when toggling)
            var position = e.GetPosition(border);
            if (position.X < 25 && vm.IsGrouped)
                return;

            var data = new DataObject();
            data.Set(DragDataFormat, vm);

            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DragDataFormat))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var source = e.Data.Get(DragDataFormat) as ColumnLinkViewModel;
        var targetBorder = FindColumnCardBorder(e.Source as Visual);
        var target = targetBorder?.DataContext as ColumnLinkViewModel;

        if (source == null || target == null || source == target)
        {
            e.DragEffects = DragDropEffects.None;
            ClearHighlight();
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        HighlightBorder(targetBorder);
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        ClearHighlight();
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        ClearHighlight();

        if (!e.Data.Contains(DragDataFormat))
            return;

        var source = e.Data.Get(DragDataFormat) as ColumnLinkViewModel;
        var targetBorder = FindColumnCardBorder(e.Source as Visual);
        var target = targetBorder?.DataContext as ColumnLinkViewModel;

        if (source == null || target == null || source == target)
            return;

        if (DataContext is ColumnLinkingViewModel parentVm)
        {
            parentVm.MergeColumns(target, source);
        }
    }

    private Border? FindColumnCardBorder(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Border border && border.DataContext is ColumnLinkViewModel)
                return border;
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private void HighlightBorder(Border? border)
    {
        if (border == _highlightedBorder)
            return;

        ClearHighlight();

        if (border == null)
            return;

        _highlightedBorder = border;
        _originalBorderBrush = border.BorderBrush;

        if (Application.Current?.TryFindResource("AccentOrange", out var accent) == true && accent is IBrush accentBrush)
        {
            border.BorderBrush = accentBrush;
            border.BorderThickness = new Thickness(2);
        }
    }

    private void ClearHighlight()
    {
        if (_highlightedBorder != null)
        {
            _highlightedBorder.BorderBrush = _originalBorderBrush;
            _highlightedBorder.BorderThickness = new Thickness(1);
            _highlightedBorder = null;
            _originalBorderBrush = null;
        }
    }

    #region Context Menu Handlers

    private void OnMenuButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            // Find the parent Border that has the ContextMenu
            var parent = button.Parent;
            while (parent != null && parent is not Border { ContextMenu: not null })
            {
                parent = (parent as Visual)?.GetVisualParent();
            }

            if (parent is Border border && border.ContextMenu != null)
            {
                border.ContextMenu.Open(border);
            }
        }
    }

    private void OnRenameClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            var contextMenu = menuItem.Parent as ContextMenu;
            var vm = contextMenu?.DataContext as ColumnLinkViewModel;
            if (vm != null)
            {
                vm.IsEditing = true;
            }
        }
    }

    private void OnUngroupClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            var contextMenu = menuItem.Parent as ContextMenu;
            var vm = contextMenu?.DataContext as ColumnLinkViewModel;
            if (vm != null && DataContext is ColumnLinkingViewModel parentVm)
            {
                parentVm.UngroupColumn(vm);
            }
        }
    }

    private void OnEditTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is ColumnLinkViewModel vm)
        {
            if (e.Key == Key.Enter)
            {
                vm.IsEditing = false;
                NotifyParentColumnRenamed();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                vm.IsEditing = false;
                e.Handled = true;
            }
        }
    }

    private void OnEditTextBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is ColumnLinkViewModel vm)
        {
            vm.IsEditing = false;
            NotifyParentColumnRenamed();
        }
    }

    private void NotifyParentColumnRenamed()
    {
        if (DataContext is ColumnLinkingViewModel parentVm)
        {
            parentVm.NotifyColumnRenamed();
        }
    }

    #endregion
}
