using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class ClubMapViewModel : ViewModelBase
{
    private Action<string>? _selectPc;
    private Action? _bookSelectedPc;
    private string? _selectedPc;
    private string? _selectedZone;
    private string? _selectedStatus;
    private string _detailTitle = "Выберите ПК";
    private string _detailSubtitle = "После выбора здесь появятся зона, статус, железо, тариф и ближайшие свободные интервалы.";
    private string _photoCaption = "Заглушка до загрузки реальных фото";
    private string _cpu = "—";
    private string _gpu = "—";
    private string _ram = "—";
    private string _monitor = "—";
    private string _intervals = "Свободные интервалы появятся после выбора места.";
    private string _bookButtonText = "Забронировать выбранный ПК";
    private bool _canBookSelectedPc;

    public ClubMapViewModel()
    {
        SelectPcCommand = new RelayCommand(parameter =>
        {
            if (parameter is string raw && !string.IsNullOrWhiteSpace(raw))
            {
                _selectPc?.Invoke(raw);
            }
        });
        BookSelectedPcCommand = new RelayCommand(_ => _bookSelectedPc?.Invoke());
    }

    public string? SelectedPc
    {
        get => _selectedPc;
        set => SetProperty(ref _selectedPc, value);
    }

    public string? SelectedZone
    {
        get => _selectedZone;
        set => SetProperty(ref _selectedZone, value);
    }

    public string? SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
    }

    public string DetailTitle
    {
        get => _detailTitle;
        set => SetProperty(ref _detailTitle, value);
    }

    public string DetailSubtitle
    {
        get => _detailSubtitle;
        set => SetProperty(ref _detailSubtitle, value);
    }

    public string PhotoCaption
    {
        get => _photoCaption;
        set => SetProperty(ref _photoCaption, value);
    }

    public string Cpu
    {
        get => _cpu;
        set => SetProperty(ref _cpu, value);
    }

    public string Gpu
    {
        get => _gpu;
        set => SetProperty(ref _gpu, value);
    }

    public string Ram
    {
        get => _ram;
        set => SetProperty(ref _ram, value);
    }

    public string Monitor
    {
        get => _monitor;
        set => SetProperty(ref _monitor, value);
    }

    public string Intervals
    {
        get => _intervals;
        set => SetProperty(ref _intervals, value);
    }

    public string BookButtonText
    {
        get => _bookButtonText;
        set => SetProperty(ref _bookButtonText, value);
    }

    public bool CanBookSelectedPc
    {
        get => _canBookSelectedPc;
        set
        {
            if (SetProperty(ref _canBookSelectedPc, value))
            {
                OnPropertyChanged(nameof(BookButtonOpacity));
            }
        }
    }

    public double BookButtonOpacity => CanBookSelectedPc ? 1 : 0.55;
    public ICommand SelectPcCommand { get; }
    public ICommand BookSelectedPcCommand { get; }

    public void ConfigureActions(Action<string> selectPc, Action bookSelectedPc)
    {
        _selectPc = selectPc;
        _bookSelectedPc = bookSelectedPc;
    }
}
