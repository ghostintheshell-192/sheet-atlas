using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace SheetAtlas.UI.Avalonia.Controls;

/// <summary>
/// A reusable collapsible section control with customizable header and content.
/// Provides consistent styling across SearchView and ComparisonView.
/// </summary>
public class CollapsibleSection : TemplatedControl
{
    private Border? _headerBorder;

    /// <summary>
    /// Defines the IsExpanded property.
    /// </summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<CollapsibleSection, bool>(nameof(IsExpanded), defaultValue: true);

    /// <summary>
    /// Defines the Header property (content displayed in the header area).
    /// </summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<CollapsibleSection, object?>(nameof(Header));

    /// <summary>
    /// Defines the HeaderBackground property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> HeaderBackgroundProperty =
        AvaloniaProperty.Register<CollapsibleSection, IBrush?>(nameof(HeaderBackground));

    /// <summary>
    /// Defines the HeaderForeground property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> HeaderForegroundProperty =
        AvaloniaProperty.Register<CollapsibleSection, IBrush?>(nameof(HeaderForeground));

    /// <summary>
    /// Defines the HeaderBorderBrush property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> HeaderBorderBrushProperty =
        AvaloniaProperty.Register<CollapsibleSection, IBrush?>(nameof(HeaderBorderBrush));

    /// <summary>
    /// Defines the Content property (content displayed when expanded).
    /// </summary>
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<CollapsibleSection, object?>(nameof(Content));

    /// <summary>
    /// Defines the ShowExpandIcon property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowExpandIconProperty =
        AvaloniaProperty.Register<CollapsibleSection, bool>(nameof(ShowExpandIcon), defaultValue: true);

    static CollapsibleSection()
    {
        IsExpandedProperty.Changed.AddClassHandler<CollapsibleSection>((x, _) => x.UpdateExpandedPseudoClass());
    }

    public CollapsibleSection()
    {
        UpdateExpandedPseudoClass();
    }

    /// <summary>
    /// Gets or sets whether the section is expanded.
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Gets or sets the header content.
    /// </summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>
    /// Gets or sets the header background brush.
    /// </summary>
    public IBrush? HeaderBackground
    {
        get => GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the header foreground brush.
    /// </summary>
    public IBrush? HeaderForeground
    {
        get => GetValue(HeaderForegroundProperty);
        set => SetValue(HeaderForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the header border brush.
    /// </summary>
    public IBrush? HeaderBorderBrush
    {
        get => GetValue(HeaderBorderBrushProperty);
        set => SetValue(HeaderBorderBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the content displayed when expanded.
    /// </summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the expand/collapse icon.
    /// </summary>
    public bool ShowExpandIcon
    {
        get => GetValue(ShowExpandIconProperty);
        set => SetValue(ShowExpandIconProperty, value);
    }

    /// <summary>
    /// Toggles the expanded state.
    /// </summary>
    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Unsubscribe from old header if any
        if (_headerBorder != null)
        {
            _headerBorder.PointerPressed -= OnHeaderPointerPressed;
        }

        // Find and subscribe to new header
        _headerBorder = e.NameScope.Find<Border>("PART_HeaderBorder");
        if (_headerBorder != null)
        {
            _headerBorder.PointerPressed += OnHeaderPointerPressed;
        }
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only toggle on left click
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            ToggleExpanded();
            e.Handled = true;
        }
    }

    private void UpdateExpandedPseudoClass()
    {
        PseudoClasses.Set(":expanded", IsExpanded);
    }
}
