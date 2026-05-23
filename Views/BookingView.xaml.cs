using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VictusLounge.Views;

public partial class BookingView : UserControl
{
    public BookingView()
    {
        InitializeComponent();
    }

    private void BookingDate_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(BookingDate_Click), sender, e);
    }

    private void BookingMode_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(BookingMode_Click), sender, e);
    }

    private void BookingZone_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(BookingZone_Click), sender, e);
    }

    private void ClearBooking_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(ClearBooking_Click), sender, e);
    }

    private void ConfirmBooking_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(ConfirmBooking_Click), sender, e);
    }

    private void DurationButton_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(DurationButton_Click), sender, e);
    }

    private void ToggleTimePicker_Click(object sender, RoutedEventArgs e)
    {
        ForwardToMainWindow(nameof(ToggleTimePicker_Click), sender, e);
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
