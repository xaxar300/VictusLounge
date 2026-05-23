namespace VictusLounge.ViewModels;

public sealed class CurrentUserViewModel : ViewModelBase
{
    private int _id;
    private string _fullName = "Not signed in";
    private string _login = string.Empty;
    private string _role = "client";
    private decimal _balance;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Login
    {
        get => _login;
        set => SetProperty(ref _login, value);
    }

    public string Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    public decimal Balance
    {
        get => _balance;
        set => SetProperty(ref _balance, value);
    }
}
