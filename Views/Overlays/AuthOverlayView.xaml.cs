using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VictusLounge.Views;

public partial class AuthOverlayView : UserControl
{
    public AuthOverlayView()
    {
        InitializeComponent();
    }

    private void AuthEnter_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(AuthEnter_Click), sender, e);
    }

    private void AuthRole_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(AuthRole_Click), sender, e);
    }

    private void ShowLoginAuth_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(ShowLoginAuth_Click), sender, e);
    }

    private void ShowRegisterAuth_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(ShowRegisterAuth_Click), sender, e);
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
