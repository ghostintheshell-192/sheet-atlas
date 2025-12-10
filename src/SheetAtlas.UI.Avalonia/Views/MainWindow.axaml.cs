using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnHeaderTapped(object? sender, TappedEventArgs e)
    {
        // Clear selection when tapping header area
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedFile = null;
        }
    }

    private void OnClearSelectionClick(object? sender, RoutedEventArgs e)
    {
        // Clear selection when clicking Deselect button
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedFile = null;
        }
    }

    private void OnUnloadFileClick(object? sender, RoutedEventArgs e)
    {
        // Unload the selected file
        if (DataContext is MainWindowViewModel viewModel && viewModel.SelectedFile != null)
        {
            var fileToRemove = viewModel.SelectedFile;
            viewModel.FileDetailsViewModel?.CleanAllDataCommand.Execute(null);
        }
    }

    private void OnFileItemTapped(object? sender, TappedEventArgs e)
    {
        // Toggle IsExpanded for the tapped file
        if (sender is Grid grid && grid.DataContext is IFileLoadResultViewModel fileViewModel)
        {
            fileViewModel.IsExpanded = !fileViewModel.IsExpanded;

            // Note: Don't set SelectedFile here or block the event.
            // Let the ListBox handle selection natively to support multi-select
            // (Ctrl+Click, Shift+Click). The SelectionChanged event will update
            // SelectedFile and TemplateManagementViewModel accordingly.
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
                // Invoke the existing event handler
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

    private void OnFilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && sender is ListBox listBox)
        {
            var selectedFiles = listBox.SelectedItems?
                .OfType<IFileLoadResultViewModel>()
                .ToList() ?? new List<IFileLoadResultViewModel>();

            viewModel.UpdateSelectedFiles(selectedFiles);
        }
    }
}
