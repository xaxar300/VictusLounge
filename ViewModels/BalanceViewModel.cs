using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VictusLounge.Services;

namespace VictusLounge.ViewModels;

public sealed class BalanceViewModel : ViewModelBase
{
    private readonly BalanceService _balanceService;
    private readonly Action<string?> _promoChanged;
    private readonly Action<string, string> _showStatus;
    private readonly Action _reloadState;
    private readonly Action _openTopup;
    private readonly Func<string> _exportHistory;
    private readonly Action<string> _buyPackage;
    private readonly Action<string> _showPackage;
    private decimal _currentBalance;
    private decimal _bonusAmount;
    private string _promoCode = string.Empty;
    private string? _appliedPromoCode;
    private string _personalOfferText = string.Empty;
    private string _packagesTitle = "Пакеты времени";
    private string _quickPackageTitle = "Quick Game";
    private string _quickPackageText = "2 часа · 15 BYN";
    private string _quickPackageButtonText = "Купить";
    private string _quickPackageTag = "Quick Game|15 BYN";
    private bool _isQuickPackageVisible = true;
    private bool _isRegularPackagesVisible = true;

    public BalanceViewModel(
        BalanceService balanceService,
        Action<string?> promoChanged,
        Action<string, string> showStatus,
        Action reloadState,
        Action openTopup,
        Func<string> exportHistory,
        Action<string> buyPackage,
        Action<string> showPackage)
    {
        _balanceService = balanceService;
        _promoChanged = promoChanged;
        _showStatus = showStatus;
        _reloadState = reloadState;
        _openTopup = openTopup;
        _exportHistory = exportHistory;
        _buyPackage = buyPackage;
        _showPackage = showPackage;

        ApplyPromoCommand = new RelayCommand(ApplyPromo);
        ClearPromoCommand = new RelayCommand(() => ClearPromo(showStatus: true));
        OpenTopupCommand = new RelayCommand(_openTopup);
        ExportHistoryCommand = new RelayCommand(() =>
        {
            var exportPath = _exportHistory();
            _showStatus("Экспорт", $"История операций выгружена: {exportPath}");
        });
        ShowOfferCommand = new RelayCommand(() =>
            _showStatus("Персональная акция", "Бонус статуса применяется автоматически при пополнении и не требует активации."));
        BuyPackageCommand = RelayCommand.ForString(_buyPackage);
        ShowPackageCommand = RelayCommand.ForString(_showPackage);
    }

    public ObservableCollection<BalanceHistoryItemViewModel> History { get; } = [];

    public decimal CurrentBalance
    {
        get => _currentBalance;
        set
        {
            if (SetProperty(ref _currentBalance, value))
            {
                OnPropertyChanged(nameof(CurrentBalanceText));
            }
        }
    }

    public string CurrentBalanceText => $"{CurrentBalance:0.##} BYN";

    public decimal BonusAmount
    {
        get => _bonusAmount;
        set
        {
            if (SetProperty(ref _bonusAmount, value))
            {
                OnPropertyChanged(nameof(BonusText));
            }
        }
    }

    public string BonusText => $"Получено бонусов: {BonusAmount:0.##}";

    public string PromoCode
    {
        get => _promoCode;
        set => SetProperty(ref _promoCode, value);
    }

    public string? AppliedPromoCode
    {
        get => _appliedPromoCode;
        private set => SetProperty(ref _appliedPromoCode, value);
    }

    public string PersonalOfferText
    {
        get => _personalOfferText;
        set => SetProperty(ref _personalOfferText, value);
    }

    public string PackagesTitle
    {
        get => _packagesTitle;
        set => SetProperty(ref _packagesTitle, value);
    }

    public string QuickPackageTitle
    {
        get => _quickPackageTitle;
        set => SetProperty(ref _quickPackageTitle, value);
    }

    public string QuickPackageText
    {
        get => _quickPackageText;
        set => SetProperty(ref _quickPackageText, value);
    }

    public string QuickPackageButtonText
    {
        get => _quickPackageButtonText;
        set => SetProperty(ref _quickPackageButtonText, value);
    }

    public string QuickPackageTag
    {
        get => _quickPackageTag;
        set => SetProperty(ref _quickPackageTag, value);
    }

    public bool IsQuickPackageVisible
    {
        get => _isQuickPackageVisible;
        set => SetProperty(ref _isQuickPackageVisible, value);
    }

    public bool IsRegularPackagesVisible
    {
        get => _isRegularPackagesVisible;
        set => SetProperty(ref _isRegularPackagesVisible, value);
    }

    public ICommand ApplyPromoCommand { get; }
    public ICommand ClearPromoCommand { get; }
    public ICommand OpenTopupCommand { get; }
    public ICommand ExportHistoryCommand { get; }
    public ICommand ShowOfferCommand { get; }
    public ICommand BuyPackageCommand { get; }
    public ICommand ShowPackageCommand { get; }

    public void SetHistory(IEnumerable<BalanceHistoryItemViewModel> items)
    {
        History.Clear();
        foreach (var item in items)
        {
            History.Add(item);
        }
    }

    public void ShowDefaultPackageOffer(string title, string quickText, string buyText)
    {
        PackagesTitle = title;
        QuickPackageTitle = "Quick Game";
        QuickPackageText = quickText;
        QuickPackageButtonText = buyText;
        QuickPackageTag = "Quick Game|15 BYN";
        IsQuickPackageVisible = true;
        IsRegularPackagesVisible = true;
    }

    public void ShowBookingRequiredOffer()
    {
        PackagesTitle = "Оплата брони";
        QuickPackageTitle = "Нет активной брони";
        QuickPackageText = "Сначала забронируйте ПК";
        QuickPackageButtonText = "Перейти к брони";
        QuickPackageTag = "booking";
        IsQuickPackageVisible = true;
        IsRegularPackagesVisible = false;
    }

    public void ShowActiveBookingOffer(string title, string packageTitle, string packageText, string buttonText, string tag)
    {
        PackagesTitle = title;
        QuickPackageTitle = packageTitle;
        QuickPackageText = packageText;
        QuickPackageButtonText = buttonText;
        QuickPackageTag = tag;
        IsQuickPackageVisible = true;
        IsRegularPackagesVisible = false;
    }

    private void ApplyPromo()
    {
        if (string.IsNullOrWhiteSpace(PromoCode))
        {
            ClearPromo(showStatus: true);
            return;
        }

        var promoCode = _balanceService.GetActivePromoCode(PromoCode);
        if (promoCode is null)
        {
            AppliedPromoCode = null;
            _promoChanged(null);
            _reloadState();
            _showStatus("Промокод не найден", "Проверьте код или активность промокода в базе.");
            return;
        }

        PromoCode = promoCode.Code;
        AppliedPromoCode = promoCode.Code;
        _promoChanged(promoCode.Code);
        _reloadState();
        _showStatus(
            "Промокод применен",
            $"{promoCode.Code}: -{promoCode.BookingDiscountRate * 100:0}% к оплате брони или +{promoCode.TopupBonusRate * 100:0}% к пополнению от {promoCode.MinTopupAmount:0.##} BYN.");
    }

    private void ClearPromo(bool showStatus)
    {
        PromoCode = string.Empty;
        AppliedPromoCode = null;
        _promoChanged(null);
        _reloadState();

        if (showStatus)
        {
            _showStatus("Промокод снят", "Скидка к оплате брони и бонус к пополнению отключены.");
        }
    }
}

public sealed class BalanceHistoryItemViewModel
{
    public required string Date { get; init; }
    public required string Operation { get; init; }
    public required string Method { get; init; }
    public required string Amount { get; init; }
    public required string Status { get; init; }
    public required Brush AmountBrush { get; init; }
    public required Brush StatusBrush { get; init; }
}
