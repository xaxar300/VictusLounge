using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VictusLounge.Models;
using VictusLounge.Services;
using VictusLounge.Services.Facades;
using VictusLounge.ViewModels;

namespace VictusLounge;

public partial class MainWindow : Window
{
    private const string SearchPlaceholder = "Поиск: клиент, PC-04, бронь, турнир, платеж...";
    private const double AnnouncementSpeed = 55;
    private const double AnnouncementGap = 36;
    private readonly DispatcherTimer _toastTimer;
    private readonly DispatcherTimer _undoSnackbarTimer;
    private readonly DispatcherTimer _announcementTimer;
    private readonly DispatcherTimer _liveRefreshTimer;
    private readonly MainWindowViewModel _viewModel;
    private readonly AdminOperationsService _adminOperationsService = new();
    private readonly BookingFacade _bookingFacade = new();
    private readonly TopupFacade _topupFacade = new();
    private readonly UserSettingsStore _userSettingsStore = new();
    private readonly HashSet<string> _selectedSeats = [];
    private ResourceDictionary _languageStrings = new();
    private UserSettings _userSettings = UserSettings.Default;
    private Action? _pendingUndoAction;
    private DateTime _lastAnnouncementTick;
    private bool _isSidebarCollapsed;
    private string _currentView = "dashboard";
    private string _currentTheme = "BlackGold";
    private string _currentInterfaceSize = "normal";
    private string _currentLanguage = "ru";
    private string _currentRole = "client";
    private bool _confirmClientActions = true;
    private int _bookingDuration = 1;
    private bool _isCompanyBooking;
    private DateTime _bookingDate = DateTime.Today;
    private string _bookingZoneKey = "Standard";
    private string _bookingZoneName = "Standard Hall";
    private int _bookingTariff = 8;
    private int _bookingHour = 18;
    private int _bookingMinute;
    private string _bookingPackage = "regular";
    private string? _selectedMapPc;
    private string? _selectedMapZone;
    private string? _selectedMapStatus;
    private bool _isNotificationCenterOpen;
    private string _topupMethod = "card";
    private string? _appliedPromoCode;
    private decimal _balanceAmount;
    private int _unreadNotifications;
    private int _adminActiveSessions = 21;
    private int _adminPaymentQueue = 5;
    private int _adminFreePcs;
    private int _adminSupportQueue = 3;
    private decimal _shiftCash = 1248;
    private decimal _shiftOnline = 694;
    private bool _shiftClosed;
    private int _ownerRevenue;
    private int _ownerLoad;
    private int _ownerAverageCheck;
    private int _ownerRepeatRate;
    private int _standardRate = 8;
    private int _vipRate = 14;
    private int _royalRate = 24;
    private int _bootcampRate = 50;
    private string _ownerDemandMode = "normal";
    // UI state is mutated only on the WPF dispatcher thread.
    private readonly Dictionary<string, string> _pcStatusOverrides = new(StringComparer.Ordinal);
    private readonly List<Computer> _computers = [];
    private readonly List<Tariff> _tariffs = [];
    private int? _activeCabinetBookingId;
    private int? _activeCabinetSessionId;
    private int _currentUserId;
    private string _currentUserFullName = "Not signed in";
    private string _currentUserLogin = string.Empty;

    public MainWindow()
    {
        _userSettings = _userSettingsStore.Load();
        _currentTheme = _userSettings.Theme;
        _currentInterfaceSize = _userSettings.InterfaceSize;
        _currentLanguage = _userSettings.Language;
        _confirmClientActions = _userSettings.ConfirmClientActions;

        _viewModel = new MainWindowViewModel(
            new AuthViewModel(new AuthService(), SignInUser, ApplyAuthViewState, SelectAuthRole),
            new BalanceViewModel(
                new BalanceService(),
                code => _appliedPromoCode = code,
                ShowStatus,
                () =>
                {
                    UpdateTopupSummary();
                    LoadDatabaseState();
                },
                OpenTopupOverlay,
                ExportBalanceHistory,
                HandleBalancePackagePurchase,
                ShowBalancePackageStatus),
            new DashboardViewModel(ExecuteQuickAction, SelectDashboardZone),
            new ClientCabinetViewModel(ExecuteCabinetAction, CancelCabinetBooking, EndCabinetSession),
            new EventsViewModel(ApplyEventFilter, JoinEvent),
            new SettingsViewModel(
                theme => ApplyTheme(theme),
                language => ApplyLanguage(language),
                size => ApplyInterfaceSize(size),
                SetActionConfirmation),
            new NotificationCenterViewModel(ToggleNotificationCenter, MarkNotificationsRead),
            new ShellViewModel(ExecuteGlobalSearch, HandleShellPreviewKeyDown, HandleShellPreviewMouseDown),
            ExecuteAdminAction,
            ExecuteAdminAction,
            ExecuteShiftTask);
        ApplyThemeResources(_currentTheme);
        InitializeComponent();
        DataContext = _viewModel;
        ConfigureNavigationCommands();
        ConfigureBookingCommands();
        ConfigureTopupCommands();
        _viewModel.Settings.RequireActionConfirmation = _confirmClientActions;
        UndoSnackbarButton.Click += (_, _) => ExecutePendingUndo();

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            StatusToast.Visibility = Visibility.Collapsed;
        };
        _undoSnackbarTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        _undoSnackbarTimer.Tick += (_, _) => HideUndoSnackbar();

        _announcementTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _announcementTimer.Tick += AnnouncementTimer_Tick;
        _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _liveRefreshTimer.Tick += (_, _) => LoadDatabaseState();

        ApplyLanguage(_currentLanguage, false);
        UpdateThemeButtons();
        ApplyInterfaceSize(_currentInterfaceSize, false);

        Loaded += (_, _) =>
        {
            LoadDatabaseState();
            UpdateAuthRoleButtons();
            ApplyRoleAccess(false);
            ApplySidebarState(false);
            UpdateNotificationBadge();
            StartAnnouncementMarquee();
            ApplyMapPcButtonStatuses();
            InitializeBookingDates();
            RebuildBookingTimePicker();
            RebuildBookingSeatGrid();
            UpdateBookingSummary();
            RefreshAdminUx();
            _liveRefreshTimer.Start();
        };
        AnnouncementBar.SizeChanged += (_, _) => ResetAnnouncementMarquee();
    }

    private void InitializeBookingDates()
    {
        var today = DateTime.Today;
        _bookingDate = today;

        ConfigureBookingDateButton(DateTodayButton, "Сегодня", today);
        ConfigureBookingDateButton(DateTomorrowButton, GetShortRuDay(today.AddDays(1)), today.AddDays(1));
        ConfigureBookingDateButton(DateThirdButton, GetShortRuDay(today.AddDays(2)), today.AddDays(2));
        ConfigureBookingDateButton(DateCustomButton, GetShortRuDay(today.AddDays(3)), today.AddDays(3));
        SetTaggedChoiceButtonStyles(DateTodayButton.Tag?.ToString() ?? string.Empty, DateTodayButton, DateTomorrowButton, DateThirdButton, DateCustomButton);
    }

    private void RefreshBookingDatesIfStale()
    {
        if (DateTodayButton.Tag?.ToString() == DateTime.Today.ToString("yyyy-MM-dd"))
        {
            return;
        }

        InitializeBookingDates();
        RebuildBookingTimePicker();
        UpdateBookingSummary();
    }

    private static void ConfigureBookingDateButton(Button button, string title, DateTime date)
    {
        button.Tag = date.ToString("yyyy-MM-dd");
        button.Content = $"{title}\n{date:dd.MM}";
    }

    private static string GetShortRuDay(DateTime date)
    {
        return date.DayOfWeek switch
        {
            DayOfWeek.Monday => "Пн",
            DayOfWeek.Tuesday => "Вт",
            DayOfWeek.Wednesday => "Ср",
            DayOfWeek.Thursday => "Чт",
            DayOfWeek.Friday => "Пт",
            DayOfWeek.Saturday => "Сб",
            _ => "Вс"
        };
    }

}
