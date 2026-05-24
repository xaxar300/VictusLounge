using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class ClientCabinetViewModel
{
    public ClientCabinetViewModel(Action<string> executeAction, Action cancelBooking, Action endSession)
    {
        ActionCommand = new RelayCommand(parameter =>
        {
            if (parameter is string action && !string.IsNullOrWhiteSpace(action))
            {
                executeAction(action);
            }
        });
        CancelBookingCommand = new RelayCommand(_ => cancelBooking());
        EndSessionCommand = new RelayCommand(_ => endSession());
    }

    public ICommand ActionCommand { get; }
    public ICommand CancelBookingCommand { get; }
    public ICommand EndSessionCommand { get; }
}
