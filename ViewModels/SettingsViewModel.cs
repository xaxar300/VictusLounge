using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class SettingsViewModel
{
    public SettingsViewModel(Action<string> applyTheme, Action<string> applyLanguage)
    {
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
    }

    public ICommand ThemeCommand { get; }
    public ICommand LanguageCommand { get; }
}
