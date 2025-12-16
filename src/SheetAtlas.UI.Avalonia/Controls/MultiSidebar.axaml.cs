using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using SheetAtlas.UI.Avalonia.Models;

namespace SheetAtlas.UI.Avalonia.Controls;

/// <summary>
/// A VSCode-style multi-sidebar control with icon bar and collapsible panels.
/// </summary>
public partial class MultiSidebar : UserControl
{
    public static readonly StyledProperty<ObservableCollection<SidebarItem>> ItemsProperty =
        AvaloniaProperty.Register<MultiSidebar, ObservableCollection<SidebarItem>>(
            nameof(Items));

    public static readonly StyledProperty<bool> AllowMultipleOpenProperty =
        AvaloniaProperty.Register<MultiSidebar, bool>(
            nameof(AllowMultipleOpen), defaultValue: false);

    /// <summary>
    /// Collection of sidebar items to display.
    /// </summary>
    public ObservableCollection<SidebarItem> Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>
    /// Whether multiple sidebars can be open at the same time.
    /// When false (default), clicking an icon closes other open sidebars (radio button behavior).
    /// </summary>
    public bool AllowMultipleOpen
    {
        get => GetValue(AllowMultipleOpenProperty);
        set => SetValue(AllowMultipleOpenProperty, value);
    }

    /// <summary>
    /// Command to toggle a sidebar open/closed.
    /// </summary>
    public IRelayCommand<SidebarItem> ToggleSidebarCommand { get; }

    public MultiSidebar()
    {
        Items = new ObservableCollection<SidebarItem>();
        ToggleSidebarCommand = new RelayCommand<SidebarItem>(ToggleSidebar);

        InitializeComponent();
    }

    private void ToggleSidebar(SidebarItem? item)
    {
        if (item == null) return;

        if (item.IsOpen)
        {
            // If already open, close it
            item.IsOpen = false;
        }
        else
        {
            // If not allowing multiple open, close all others first
            if (!AllowMultipleOpen)
            {
                foreach (var sidebar in Items)
                {
                    if (sidebar != item)
                    {
                        sidebar.IsOpen = false;
                    }
                }
            }

            // Open the clicked sidebar
            item.IsOpen = true;
        }
    }

    #region Resize Logic

    private SidebarItem? _resizingItem;
    private Point _resizeStartPoint;
    private double _resizeStartWidth;

    private void OnResizeGripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is SidebarItem item)
        {
            _resizingItem = item;
            _resizeStartPoint = e.GetPosition(this);
            _resizeStartWidth = item.Width;
            e.Pointer.Capture(border);
            e.Handled = true;
        }
    }

    private void OnResizeGripPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizingItem == null) return;

        var currentPoint = e.GetPosition(this);
        var delta = currentPoint.X - _resizeStartPoint.X;
        var newWidth = _resizeStartWidth + delta;

        // Clamp to min/max
        newWidth = Math.Max(_resizingItem.MinWidth, Math.Min(_resizingItem.MaxWidth, newWidth));
        _resizingItem.Width = newWidth;
    }

    private void OnResizeGripPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizingItem != null && sender is Border border)
        {
            e.Pointer.Capture(null);
            _resizingItem = null;
        }
    }

    #endregion
}
