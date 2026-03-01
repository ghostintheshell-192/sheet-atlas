using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SheetAtlas.UI.Avalonia.Models;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private SidebarItem? _columnsSidebarItem;
    private SidebarItem? _filesSidebarItem;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

            _filesSidebarItem = MainSidebar.Items.FirstOrDefault(i => i.Tooltip == "Loaded Files");
            _columnsSidebarItem = MainSidebar.Items.FirstOrDefault(i => i.Tooltip == "Columns");

            if (_columnsSidebarItem != null)
                _columnsSidebarItem.BadgeCount = viewModel.ColumnCount;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm) return;

        if (e.PropertyName == nameof(MainWindowViewModel.ColumnCount) && _columnsSidebarItem != null)
            _columnsSidebarItem.BadgeCount = vm.ColumnCount;

        if (e.PropertyName == nameof(MainWindowViewModel.HasLoadedFiles)
            && vm.HasLoadedFiles
            && _filesSidebarItem != null)
        {
            _filesSidebarItem.IsOpen = true;
        }
    }

    private void OnHeaderTapped(object? sender, TappedEventArgs e)
    {
        // Clear selection when tapping header area
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedFile = null;
        }
    }

    private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        // Trigger search when Enter key is pressed
        if (e.Key == Key.Enter && DataContext is MainWindowViewModel viewModel)
        {
            if (viewModel.SearchViewModel?.SearchCommand?.CanExecute(null) == true)
            {
                viewModel.SearchViewModel.SearchCommand.Execute(null);
            }
        }
    }

    private void OnKebabMenuTapped(object? sender, TappedEventArgs e)
    {
        // Prevent tapped event from bubbling up to OnFileItemTapped
        e.Handled = true;
    }

    private void OnRemoveFromListClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is IFileLoadResultViewModel file)
        {
            if (DataContext is MainWindowViewModel viewModel && viewModel.FileDetailsViewModel != null)
            {
                viewModel.FileDetailsViewModel.SelectedFile = file;
                viewModel.FileDetailsViewModel.RemoveFromListCommand.Execute(null);
            }
        }
    }

    private void OnCleanAllDataClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is IFileLoadResultViewModel file)
        {
            if (DataContext is MainWindowViewModel viewModel && viewModel.FileDetailsViewModel != null)
            {
                viewModel.FileDetailsViewModel.SelectedFile = file;
                viewModel.FileDetailsViewModel.CleanAllDataCommand.Execute(null);
            }
        }
    }

    private void OnRemoveNotificationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is IFileLoadResultViewModel file)
        {
            if (DataContext is MainWindowViewModel viewModel && viewModel.FileDetailsViewModel != null)
            {
                viewModel.FileDetailsViewModel.SelectedFile = file;
                viewModel.FileDetailsViewModel.RemoveNotificationCommand.Execute(null);
            }
        }
    }

    private void OnTryAgainClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is IFileLoadResultViewModel file)
        {
            if (DataContext is MainWindowViewModel viewModel && viewModel.FileDetailsViewModel != null)
            {
                viewModel.FileDetailsViewModel.SelectedFile = file;
                viewModel.FileDetailsViewModel.TryAgainCommand.Execute(null);
            }
        }
    }
}
