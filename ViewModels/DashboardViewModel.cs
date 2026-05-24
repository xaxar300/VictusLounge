using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class DashboardViewModel
{
    public DashboardViewModel(Action<string> executeQuickAction, Action<string> selectZone)
    {
        QuickActionCommand = new RelayCommand(parameter =>
        {
            if (parameter is string action && !string.IsNullOrWhiteSpace(action))
            {
                executeQuickAction(action);
            }
        });

        SelectZoneCommand = new RelayCommand(parameter =>
        {
            if (parameter is string zone && !string.IsNullOrWhiteSpace(zone))
            {
                selectZone(zone);
            }
        });
    }

    public ICommand QuickActionCommand { get; }
    public ICommand SelectZoneCommand { get; }
}
