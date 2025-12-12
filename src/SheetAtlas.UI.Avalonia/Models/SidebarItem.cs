using Avalonia.Controls.Templates;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SheetAtlas.UI.Avalonia.Models;

/// <summary>
/// Represents a sidebar item in the MultiSidebar control.
/// Each item has an icon, tooltip, content template, and open/close state.
/// </summary>
public partial class SidebarItem : ObservableObject
{
    /// <summary>
    /// Geometry data for the sidebar icon.
    /// </summary>
    [ObservableProperty]
    private Geometry? _iconData;

    /// <summary>
    /// Tooltip text shown on hover.
    /// </summary>
    [ObservableProperty]
    private string _tooltip = string.Empty;

    /// <summary>
    /// The content template to render when this sidebar is open.
    /// </summary>
    [ObservableProperty]
    private IDataTemplate? _contentTemplate;

    /// <summary>
    /// Whether this sidebar panel is currently open.
    /// </summary>
    [ObservableProperty]
    private bool _isOpen;

    /// <summary>
    /// Width of the sidebar panel when open.
    /// </summary>
    [ObservableProperty]
    private double _width = 256;
}
