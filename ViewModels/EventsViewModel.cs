using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class EventsViewModel
{
    public EventsViewModel(Action<string> applyFilter, Action<string> joinEvent)
    {
        FilterCommand = RelayCommand.ForString(applyFilter);
        JoinCommand = RelayCommand.ForString(joinEvent);
    }

    public ICommand FilterCommand { get; }
    public ICommand JoinCommand { get; }
}
