using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VictusLounge.Views;

public partial class BalanceView : UserControl
{
    public BalanceView()
    {
        InitializeComponent();
    }

    private void BalanceAction_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(BalanceAction_Click), sender, e);
    }

    private void BalancePackage_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(BalancePackage_Click), sender, e);
    }

    private void BalancePackageCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ForwardToMainWindow(nameof(BalancePackageCard_MouseLeftButtonUp), sender, e);
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
