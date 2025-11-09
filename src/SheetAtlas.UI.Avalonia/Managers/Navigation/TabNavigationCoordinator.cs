using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SheetAtlas.UI.Avalonia.Managers.Navigation;

/// <summary>
/// Coordinates tab visibility and navigation in the main window.
/// Manages showing, hiding, and switching between different tabs.
/// </summary>
public class TabNavigationCoordinator : ITabNavigationCoordinator
{
    private bool _isFileDetailsTabVisible;
    private bool _isSearchTabVisible;
    private bool _isComparisonTabVisible;
    private int _selectedTabIndex = -1; // -1 = welcome screen

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsFileDetailsTabVisible
    {
        get => _isFileDetailsTabVisible;
        set
        {
            if (SetField(ref _isFileDetailsTabVisible, value))
            {
                OnPropertyChanged(nameof(HasAnyTabVisible));
            }
        }
    }

    public bool IsSearchTabVisible
    {
        get => _isSearchTabVisible;
        set
        {
            if (SetField(ref _isSearchTabVisible, value))
            {
                OnPropertyChanged(nameof(HasAnyTabVisible));
            }
        }
    }

    public bool IsComparisonTabVisible
    {
        get => _isComparisonTabVisible;
        set
        {
            if (SetField(ref _isComparisonTabVisible, value))
            {
                OnPropertyChanged(nameof(HasAnyTabVisible));
            }
        }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetField(ref _selectedTabIndex, value);
    }

    public bool HasAnyTabVisible => IsFileDetailsTabVisible || IsSearchTabVisible || IsComparisonTabVisible;

    public void ShowFileDetailsTab()
    {
        IsFileDetailsTabVisible = true;
        SelectedTabIndex = GetTabIndex("FileDetails");
    }

    public void ShowSearchTab()
    {
        IsSearchTabVisible = true;
        SelectedTabIndex = GetTabIndex("Search");
    }

    public void ShowComparisonTab()
    {
        IsComparisonTabVisible = true;
        SelectedTabIndex = GetTabIndex("Comparison");
    }

    public void CloseFileDetailsTab()
    {
        IsFileDetailsTabVisible = false;
        SwitchToNextVisibleTab("FileDetails");
    }

    public void CloseSearchTab()
    {
        IsSearchTabVisible = false;
        SwitchToNextVisibleTab("Search");
    }

    public void CloseComparisonTab()
    {
        IsComparisonTabVisible = false;
        SwitchToNextVisibleTab("Comparison");
    }

    /// <summary>
    /// Maps tab names to their absolute indices in the XAML TabControl.
    /// These indices correspond to the TabItem positions in MainWindow.axaml.
    /// IMPORTANT: These are absolute indices in the XAML markup, NOT relative to visible tabs.
    /// Avalonia TabControl uses absolute indices regardless of TabItem visibility.
    /// </summary>
    private static int GetTabIndex(string tabName)
    {
        return tabName switch
        {
            "FileDetails" => 0,  // First TabItem in XAML
            "Search" => 1,       // Second TabItem in XAML
            "Comparison" => 2,   // Third TabItem in XAML
            _ => -1              // Invalid tab name
        };
    }

    /// <summary>
    /// Switches to the next visible tab after closing the current one.
    /// Uses a priority order to determine which tab to select.
    /// If no tabs are visible, sets SelectedTabIndex to -1 (welcome screen).
    /// </summary>
    /// <param name="closedTabName">The name of the tab being closed (to exclude from selection)</param>
    private void SwitchToNextVisibleTab(string closedTabName)
    {
        // Define priority order for tab selection
        // Each tab type has its preferred fallback sequence
        var tabPriorities = closedTabName switch
        {
            "FileDetails" => new[] { "Search", "Comparison" },
            "Search" => new[] { "FileDetails", "Comparison" },
            "Comparison" => new[] { "Search", "FileDetails" },
            _ => Array.Empty<string>()
        };

        // Find first visible tab from priority list
        foreach (var tabName in tabPriorities)
        {
            bool isVisible = tabName switch
            {
                "FileDetails" => IsFileDetailsTabVisible,
                "Search" => IsSearchTabVisible,
                "Comparison" => IsComparisonTabVisible,
                _ => false
            };

            if (isVisible)
            {
                SelectedTabIndex = GetTabIndex(tabName);
                return;
            }
        }

        // No tabs visible - show welcome screen
        SelectedTabIndex = -1;
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
