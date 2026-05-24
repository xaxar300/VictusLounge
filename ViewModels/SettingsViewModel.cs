using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly Action<bool> _setActionConfirmation;
    private bool _requireActionConfirmation = true;

    public SettingsViewModel(
        Action<string> applyTheme,
        Action<string> applyLanguage,
        Action<string> applyInterfaceSize,
        Action<bool> setActionConfirmation)
    {
        _setActionConfirmation = setActionConfirmation;

        ThemeCommand = new RelayCommand(parameter =>
        {
            if (parameter is string theme && !string.IsNullOrWhiteSpace(theme))
            {
                applyTheme(theme);
            }
        });

        LanguageCommand = new RelayCommand(parameter =>
        {
            if (parameter is string language && !string.IsNullOrWhiteSpace(language))
            {
                applyLanguage(language);
            }
        });

        InterfaceSizeCommand = new RelayCommand(parameter =>
        {
            if (parameter is string size && !string.IsNullOrWhiteSpace(size))
            {
                applyInterfaceSize(size);
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ThemeCommand { get; }
    public ICommand LanguageCommand { get; }
    public ICommand InterfaceSizeCommand { get; }

    public bool RequireActionConfirmation
    {
        get => _requireActionConfirmation;
        set
        {
            if (_requireActionConfirmation == value)
            {
                return;
            }

            _requireActionConfirmation = value;
            _setActionConfirmation(value);
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
