using System.Linq;

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
        set
        {
            if (SetProperty(ref _fullName, value))
            {
                OnPropertyChanged(nameof(Initials));
            }
        }
    }

    public string Login
    {
        get => _login;
        set
        {
            if (SetProperty(ref _login, value))
            {
                OnPropertyChanged(nameof(RoleAndLogin));
            }
        }
    }

    public string Role
    {
        get => _role;
        set
        {
            if (SetProperty(ref _role, value))
            {
                OnPropertyChanged(nameof(RoleTitle));
                OnPropertyChanged(nameof(RoleAndLogin));
            }
        }
    }

    public decimal Balance
    {
        get => _balance;
        set => SetProperty(ref _balance, value);
    }

    public string RoleTitle => Role switch
    {
        "admin" => "Admin",
        "owner" => "Owner",
        _ => "Client"
    };

    public string RoleAndLogin => $"{RoleTitle} · {Login}";

    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "??";
            }

            var initials = string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
            return initials.Length == 1 ? $"{initials}." : initials;
        }
    }
}
