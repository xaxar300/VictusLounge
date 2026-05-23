using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VictusLounge.Views;

public partial class ClientCabinetView : UserControl
{
    public ClientCabinetView()
    {
        InitializeComponent();
    }

    private void CabinetAction_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(CabinetAction_Click), sender, e);
    }

    private void CabinetCancelBooking_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(CabinetCancelBooking_Click), sender, e);
    }

    private void CabinetEndSession_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(CabinetEndSession_Click), sender, e);
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(NavigationButton_Click), sender, e);
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
