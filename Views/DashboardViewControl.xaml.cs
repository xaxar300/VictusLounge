using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VictusLounge.Views;

public partial class DashboardViewControl : UserControl
{
    public DashboardViewControl()
    {
        InitializeComponent();
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(NavigationButton_Click), sender, e);
    }

    private void QuickAction_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(QuickAction_Click), sender, e);
    }

    private void ZoneCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ForwardToMainWindow(nameof(ZoneCard_MouseLeftButtonUp), sender, e);
    }
    private void ForwardToMainWindow(string handlerName, object sender, EventArgs e)
    {
        var owner = Window.GetWindow(this) as MainWindow;
        if (owner is null)
        {
            return;
        }

        var method = typeof(MainWindow).GetMethod(handlerName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        method?.Invoke(owner, new object[] { sender, e });
    }
}
