using Avalonia;
using Avalonia.Controls;

namespace SheetAtlas.UI.Avalonia.Views;

public partial class EmptyStateView : UserControl
{
    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<EmptyStateView, string>(nameof(Icon), "");

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<EmptyStateView, string>(nameof(Title), "Empty");

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<EmptyStateView, string>(nameof(Message), "No data available");

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public EmptyStateView()
    {
        InitializeComponent();
        this.PropertyChanged += OnPropertyChanged;
        UpdateTexts();
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IconProperty || e.Property == TitleProperty || e.Property == MessageProperty)
        {
            UpdateTexts();
        }
    }

    private void UpdateTexts()
    {
        var iconTextBlock = this.FindControl<TextBlock>("IconTextBlock");
        var titleTextBlock = this.FindControl<TextBlock>("TitleTextBlock");
        var messageTextBlock = this.FindControl<TextBlock>("MessageTextBlock");

        if (iconTextBlock != null)
            iconTextBlock.Text = Icon;

        if (titleTextBlock != null)
            titleTextBlock.Text = Title;

        if (messageTextBlock != null)
            messageTextBlock.Text = Message;
    }
}
