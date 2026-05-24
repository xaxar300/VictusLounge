using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class EventsViewModel
{
    public EventsViewModel(Action<string> applyFilter, Action<string> joinEvent)
    {
        FilterCommand = new RelayCommand(parameter =>
        {
            if (parameter is string filter && !string.IsNullOrWhiteSpace(filter))
            {
                applyFilter(filter);
            }
        });

        JoinCommand = new RelayCommand(parameter =>
        {
            if (parameter is string eventTag && !string.IsNullOrWhiteSpace(eventTag))
            {
                joinEvent(eventTag);
            }
        });
    }

    public ICommand FilterCommand { get; }
    public ICommand JoinCommand { get; }
}
