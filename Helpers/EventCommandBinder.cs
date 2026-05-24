using System.Windows;
using System.Windows.Input;

namespace VictusLounge.Helpers;

public static class EventCommandBinder
{
    public static readonly DependencyProperty PreviewKeyDownCommandProperty =
        DependencyProperty.RegisterAttached(
            "PreviewKeyDownCommand",
            typeof(ICommand),
            typeof(EventCommandBinder),
            new PropertyMetadata(null, OnPreviewKeyDownCommandChanged));

    public static readonly DependencyProperty PreviewMouseDownCommandProperty =
        DependencyProperty.RegisterAttached(
            "PreviewMouseDownCommand",
            typeof(ICommand),
            typeof(EventCommandBinder),
            new PropertyMetadata(null, OnPreviewMouseDownCommandChanged));

    public static ICommand? GetPreviewKeyDownCommand(DependencyObject dependencyObject)
    {
        return (ICommand?)dependencyObject.GetValue(PreviewKeyDownCommandProperty);
    }

    public static void SetPreviewKeyDownCommand(DependencyObject dependencyObject, ICommand? value)
    {
        dependencyObject.SetValue(PreviewKeyDownCommandProperty, value);
    }

    public static ICommand? GetPreviewMouseDownCommand(DependencyObject dependencyObject)
    {
        return (ICommand?)dependencyObject.GetValue(PreviewMouseDownCommandProperty);
    }

    public static void SetPreviewMouseDownCommand(DependencyObject dependencyObject, ICommand? value)
    {
        dependencyObject.SetValue(PreviewMouseDownCommandProperty, value);
    }

    private static void OnPreviewKeyDownCommandChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not UIElement element)
        {
            return;
        }

        element.PreviewKeyDown -= HandlePreviewKeyDown;
        if (e.NewValue is ICommand)
        {
            element.PreviewKeyDown += HandlePreviewKeyDown;
        }
    }

    private static void OnPreviewMouseDownCommandChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not UIElement element)
        {
            return;
        }

        element.PreviewMouseDown -= HandlePreviewMouseDown;
        if (e.NewValue is ICommand)
        {
            element.PreviewMouseDown += HandlePreviewMouseDown;
        }
    }

    private static void HandlePreviewKeyDown(object sender, KeyEventArgs e)
    {
        var command = GetPreviewKeyDownCommand((DependencyObject)sender);
        if (command?.CanExecute(e) == true)
        {
            command.Execute(e);
        }
    }

    private static void HandlePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var command = GetPreviewMouseDownCommand((DependencyObject)sender);
        if (command?.CanExecute(e) == true)
        {
            command.Execute(e);
        }
    }
}
