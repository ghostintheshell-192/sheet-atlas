using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace SheetAtlas.UI.Avalonia.Services;

public class AvaloniaDialogService : IDialogService
{
    private static Window? GetMainWindow()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    public async Task ShowMessageAsync(string message, string title = "Information")
    {
        await ShowDialogAsync(title, message);
    }

    public async Task ShowErrorAsync(string message, string title = "Error")
    {
        await ShowDialogAsync(title, message);
    }

    public async Task ShowWarningAsync(string message, string title = "Warning")
    {
        await ShowDialogAsync(title, message);
    }

    public async Task ShowInformationAsync(string message, string title = "Information")
    {
        await ShowDialogAsync(title, message);
    }

    private async Task ShowDialogAsync(string title, string message)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            var messageBox = new Window
            {
                Title = title,
                Width = 450,
                Height = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            // Get theme-aware colors from resources
            var primaryText = Application.Current?.FindResource("PrimaryText") as IBrush ?? Brushes.Black;
            var mainBackground = Application.Current?.FindResource("MainBackground") as IBrush ?? Brushes.White;

            messageBox.Background = mainBackground;

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15
            };

            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = primaryText,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 13
            };

            stackPanel.Children.Add(textBlock);

            var button = new Button
            {
                Content = "OK",
                HorizontalAlignment = HorizontalAlignment.Center,
                MinWidth = 80,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 6)
            };

            button.Click += (_, _) => messageBox.Close();
            stackPanel.Children.Add(button);

            messageBox.Content = stackPanel;
            await messageBox.ShowDialog(mainWindow);
        }
    }

    public async Task<bool> ShowConfirmationAsync(string message, string title = "Confirmation")
    {
        var mainWindow = GetMainWindow();
        if (mainWindow != null)
        {
            var messageBox = new Window
            {
                Title = title,
                Width = 500,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            // Get theme-aware colors from resources
            var primaryText = Application.Current?.FindResource("PrimaryText") as IBrush ?? Brushes.Black;
            var mainBackground = Application.Current?.FindResource("MainBackground") as IBrush ?? Brushes.White;

            messageBox.Background = mainBackground;

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15
            };

            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = primaryText,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 13
            };

            stackPanel.Children.Add(textBlock);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
                Spacing = 10
            };

            bool result = false;

            var yesButton = new Button
            {
                Content = "Yes",
                MinWidth = 80,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 6)
            };
            yesButton.Click += (_, _) => { result = true; messageBox.Close(); };

            var noButton = new Button
            {
                Content = "No",
                MinWidth = 80,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 6)
            };
            noButton.Click += (_, _) => { result = false; messageBox.Close(); };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            stackPanel.Children.Add(buttonPanel);

            messageBox.Content = stackPanel;
            await messageBox.ShowDialog(mainWindow);
            return result;
        }

        return false;
    }
}
