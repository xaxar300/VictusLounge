using System;
using System.Windows.Input;

namespace VictusLounge.ViewModels;

public sealed class ShellViewModel : ViewModelBase
{
    private readonly Action<string> _search;
    private string _searchQuery = string.Empty;

    public ShellViewModel(Action<string> search, Action<object?> handlePreviewKeyDown, Action<object?> handlePreviewMouseDown)
    {
        _search = search;
        PreviewKeyDownCommand = new RelayCommand(handlePreviewKeyDown);
        PreviewMouseDownCommand = new RelayCommand(handlePreviewMouseDown);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value) && _searchQuery.Trim().Length >= 2)
            {
                _search(_searchQuery.Trim());
            }
        }
    }

    public ICommand PreviewKeyDownCommand { get; }
    public ICommand PreviewMouseDownCommand { get; }
}
