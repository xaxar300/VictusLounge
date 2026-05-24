using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class NotificationCenterViewModel
{
    public NotificationCenterViewModel(Action toggle, Action markRead)
    {
        ToggleCommand = new RelayCommand(_ => toggle());
        MarkReadCommand = new RelayCommand(_ => markRead());
    }

    public ICommand ToggleCommand { get; }
    public ICommand MarkReadCommand { get; }
}
