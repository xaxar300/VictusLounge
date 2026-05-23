using VictusLounge.Helpers;

namespace VictusLounge;

public partial class MainWindow
{
    private sealed record PcSpecs(string Cpu, string Gpu, string Ram, string Monitor);

    private sealed record SeatInfo(string Name, string Status)
    {
        public bool IsAvailable => Status == PcStatuses.Free;

        public string StatusLabel => Status switch
        {
            PcStatuses.Busy => "занят",
            PcStatuses.Reserved => "бронь",
            PcStatuses.Service => "сервис",
            _ => "свободен"
        };
    }
}

