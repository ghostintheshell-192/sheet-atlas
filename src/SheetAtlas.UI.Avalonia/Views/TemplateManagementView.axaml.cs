using Avalonia.Controls;
using Avalonia.Input;
using SheetAtlas.UI.Avalonia.ViewModels;

namespace SheetAtlas.UI.Avalonia.Views;

public partial class TemplateManagementView : UserControl
{
    public TemplateManagementView()
    {
        InitializeComponent();
    }

    private void OnFileResultHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is FileValidationResultViewModel result)
        {
            // Only toggle if there are issues to show
            if (result.HasIssues)
            {
                result.IsExpanded = !result.IsExpanded;
            }
        }
    }
}
