using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class DashboardViewModel
{
    public DashboardViewModel(Action<string> executeQuickAction, Action<string> selectZone)
    {
        QuickActionCommand = RelayCommand.ForString(executeQuickAction);
        SelectZoneCommand = RelayCommand.ForString(selectZone);
    }

    public ICommand QuickActionCommand { get; }
    public ICommand SelectZoneCommand { get; }
}
