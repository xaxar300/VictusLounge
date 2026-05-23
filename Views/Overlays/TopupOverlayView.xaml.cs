using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VictusLounge.Views;

public partial class TopupOverlayView : UserControl
{
    public TopupOverlayView()
    {
        InitializeComponent();
    }

    private void CloseTopupOverlay_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(CloseTopupOverlay_Click), sender, e);
    }

    private void ConfirmTopup_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(ConfirmTopup_Click), sender, e);
    }

    private void TopupAmountPreset_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(TopupAmountPreset_Click), sender, e);
    }

    private void TopupMethod_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(TopupMethod_Click), sender, e);
    }

    private void TopupAmountBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ForwardToMainWindow(nameof(TopupAmountBox_TextChanged), sender, e);
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
