using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace SheetAtlas.UI.Avalonia.Views;

public partial class ClosableTabHeader : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ClosableTabHeader, string>(nameof(Title), "Tab");

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<ClosableTabHeader, ICommand?>(nameof(CloseCommand));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public ClosableTabHeader()
    {
        InitializeComponent();
        this.PropertyChanged += OnPropertyChanged;
        UpdateControls();
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TitleProperty || e.Property == CloseCommandProperty)
        {
            UpdateControls();
        }
    }

    private void UpdateControls()
    {
        var titleTextBlock = this.FindControl<TextBlock>("TitleTextBlock");
        var closeButton = this.FindControl<Button>("CloseButton");

        if (titleTextBlock != null)
            titleTextBlock.Text = Title;

        if (closeButton != null)
            closeButton.Command = CloseCommand;
    }
}
