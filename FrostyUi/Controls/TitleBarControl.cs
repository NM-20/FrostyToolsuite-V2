using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Frosty.Ui.Controls;

/// <summary>
/// Represents the title bar of a <see cref="Window"/>, albeit with the ability to include client-defined content.
/// </summary>
public sealed class TitleBarControl : UserControl
{
    #pragma warning disable IDE1006
    private Button? PART_CloseButton;

    private MenuItem? PART_CloseMenuItem;

    private Grid? PART_DragGrid;

    private Button? PART_MaximizeButton;
    private Button? PART_MinimizeButton;

    private MenuItem? PART_MaximizeMenuItem;
    private MenuItem? PART_MinimizeMenuItem;

    private MenuItem? PART_RestoreMenuItem;
    #pragma warning restore IDE1006

    protected override Type StyleKeyOverride => typeof(TitleBarControl);

    /// <summary>
    /// The underlying <see cref="StyledProperty{TValue}"/> for the <see cref="CanMaximize"/> instance property.
    /// </summary>
    public static readonly StyledProperty<bool> CanMaximizeProperty =
        AvaloniaProperty.Register<TitleBarControl, bool>(nameof(CanMaximize), true);

    /// <summary>
    /// The underlying <see cref="StyledProperty{TValue}"/> for the <see cref="CanMinimize"/> instance property.
    /// </summary>
    public static readonly StyledProperty<bool> CanMinimizeProperty =
        AvaloniaProperty.Register<TitleBarControl, bool>(nameof(CanMinimize), true);

    /// <summary>
    /// The underlying <see cref="StyledProperty{TValue}"/> for the <see cref="OwningWindow"/> instance property.
    /// </summary>
    public static readonly StyledProperty<Window?> OwningWindowProperty =
        AvaloniaProperty.Register<TitleBarControl, Window?>(nameof(OwningWindow));

    /// <summary>
    /// Specifies whether or not this <see cref="TitleBarControl"/> allows Maximize functionality for the owning
    /// <see cref="Window"/>.
    /// </summary>
    public bool CanMaximize
    {
        get => GetValue(CanMaximizeProperty);
        set => SetValue(CanMaximizeProperty, value);
    }

    /// <summary>
    /// Specifies whether or not this <see cref="TitleBarControl"/> allows Minimize functionality for the owning
    /// <see cref="Window"/>.
    /// </summary>
    public bool CanMinimize
    {
        get => GetValue(CanMinimizeProperty);
        set => SetValue(CanMinimizeProperty, value);
    }

    /// <summary>
    /// The <see cref="Window"/> in which this <see cref="TitleBarControl"/> is located. This must be set for the
    /// core window functionality to work.
    /// </summary>
    public Window? OwningWindow
    {
        get => GetValue(OwningWindowProperty);
        set => SetValue(OwningWindowProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        PART_CloseButton = (Button?)(e.NameScope.Find(nameof(PART_CloseButton)));
        PART_CloseButton?.AddHandler(Button.ClickEvent, PART_CloseButton_Click);

        PART_CloseMenuItem = (MenuItem?)(e.NameScope.Find(nameof(PART_CloseMenuItem)));
        PART_CloseMenuItem?.AddHandler(MenuItem.ClickEvent, PART_CloseMenuItem_Click);

        PART_DragGrid = (Grid?)(e.NameScope.Find(nameof(PART_DragGrid)));
        PART_DragGrid?.AddHandler(Grid.DoubleTappedEvent, PART_DragGrid_DoubleTapped);
        PART_DragGrid?.AddHandler(Grid.PointerPressedEvent, PART_DragGrid_PointerPressed);

        PART_MaximizeButton = (Button?)(e.NameScope.Find(nameof(PART_MaximizeButton)));
        PART_MaximizeButton?.AddHandler(Button.ClickEvent, PART_MaximizeButton_Click);

        PART_MinimizeButton = (Button?)(e.NameScope.Find(nameof(PART_MinimizeButton)));
        PART_MinimizeButton?.AddHandler(Button.ClickEvent, PART_MinimizeButton_Click);

        PART_MaximizeMenuItem = (MenuItem?)(e.NameScope.Find(nameof(PART_MaximizeMenuItem)));
        PART_MaximizeMenuItem?.AddHandler(MenuItem.ClickEvent, PART_MaximizeMenuItem_Click);

        PART_MinimizeMenuItem = (MenuItem?)(e.NameScope.Find(nameof(PART_MinimizeMenuItem)));
        PART_MinimizeMenuItem?.AddHandler(MenuItem.ClickEvent, PART_MinimizeMenuItem_Click);

        PART_RestoreMenuItem = (MenuItem?)(e.NameScope.Find(nameof(PART_RestoreMenuItem)));
        PART_RestoreMenuItem?.AddHandler(MenuItem.ClickEvent, PART_RestoreMenuItem_Click);
    }

    private void DoMinimize()
    {
        if (OwningWindow is not null && CanMinimize)
        {
            OwningWindow.WindowState = WindowState.Minimized;
        }
    }

    private void DoRestore()
    {
        if (OwningWindow is not null)
        {
            OwningWindow.WindowState = WindowState.Normal;
        }
    }

    private void ToggleMaximize()
    {
        if (OwningWindow is null || !CanMaximize)
        {
            return;
        }
;
        if (OwningWindow.WindowState is not WindowState.Normal)
        {
            OwningWindow.WindowState = WindowState.Normal;
        }
        else
        {
            OwningWindow.WindowState = WindowState.Maximized;
        }
    }

    private void PART_CloseButton_Click(object? sender, RoutedEventArgs e) => OwningWindow?.Close();

    private void PART_CloseMenuItem_Click(object? sender, RoutedEventArgs e) => OwningWindow?.Close();

    private void PART_DragGrid_DoubleTapped(object? sender, TappedEventArgs e) => ToggleMaximize();

    private void PART_DragGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        /*
         * It is important that we filter out other pointer buttons, as failing to do this will cause our defined
         * `ContextMenu` to break.
         */
        if (e.Properties.IsLeftButtonPressed)
        {
            OwningWindow?.BeginMoveDrag(e);
        }
    }

    private void PART_MaximizeButton_Click(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void PART_MaximizeMenuItem_Click(object? sender, RoutedEventArgs e) => ToggleMaximize();

    private void PART_MinimizeButton_Click(object? sender, RoutedEventArgs e) => DoMinimize();

    private void PART_MinimizeMenuItem_Click(object? sender, RoutedEventArgs e) => DoMinimize();

    private void PART_RestoreMenuItem_Click(object? sender, RoutedEventArgs e) => DoRestore();

    /*
     * While we do use conditions in our XAML to disable Maximize/Minimize depending on the associated properties,
     * it's important we implement these checks in code-behind as well, as XAML is customizable.
     */
}
