using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Views;

public partial class FilesSidebarView : UserControl
{
    public FilesSidebarView()
    {
        InitializeComponent();
    }

    private void OnClearSelectionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectedFile = null;
        }
    }

    private void OnUnloadFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.SelectedFile != null)
        {
            viewModel.FileDetailsViewModel?.CleanAllDataCommand.Execute(null);
        }
    }

    private void OnFileItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid grid && grid.DataContext is IFileLoadResultViewModel fileViewModel)
        {
            fileViewModel.IsExpanded = !fileViewModel.IsExpanded;
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
