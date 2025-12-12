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
