using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class ClientCabinetViewModel
{
    public ClientCabinetViewModel(Action<string> executeAction, Action cancelBooking, Action endSession)
    {
        ActionCommand = RelayCommand.ForString(executeAction);
        CancelBookingCommand = new RelayCommand(cancelBooking);
        EndSessionCommand = new RelayCommand(endSession);
    }

    public ICommand ActionCommand { get; }
    public ICommand CancelBookingCommand { get; }
    public ICommand EndSessionCommand { get; }
}
