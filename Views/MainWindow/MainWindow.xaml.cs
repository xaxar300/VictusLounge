using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using VictusLounge.Data;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.ViewModels;

namespace VictusLounge;

public partial class MainWindow : Window
{
    private const string SearchPlaceholder = "Поиск: клиент, PC-04, бронь, турнир, платеж...";
    private const double AnnouncementSpeed = 55;
    private const double AnnouncementGap = 36;
    private static readonly DependencyProperty LocalizationKeyProperty =
        DependencyProperty.RegisterAttached("LocalizationKey", typeof(string), typeof(MainWindow));

    private static readonly Dictionary<string, string> LocalizedTextKeys = new(StringComparer.Ordinal)
    {
        ["Elite Gaming Lounge Control Center"] = "Common.AppTitle",
        ["Поиск: клиент, PC-04, бронь, турнир, платеж..."] = "Common.SearchPlaceholder",
        ["Выйти"] = "Common.Logout",
        ["Главная"] = "Nav.Dashboard",
        ["Схема клуба"] = "Nav.Map",
        ["Бронирование"] = "Nav.Booking",
        ["Кабинет"] = "Nav.Cabinet",
        ["Баланс"] = "Nav.Balance",
        ["События"] = "Nav.Events",
        ["Админ"] = "Nav.Admin",
        ["Смена"] = "Nav.Shift",
        ["Настройки"] = "Nav.Settings",
        ["CLUB STATUS"] = "Dashboard.Kicker",
        ["Рабочая панель клуба"] = "Dashboard.Title",
        ["Состояние зала, броней и смены на сегодня."] = "Dashboard.Subtitle",
        ["Новая бронь"] = "Common.NewBooking",
        ["Схема"] = "Common.ClubMapShort",
        ["Свободно ПК"] = "Dashboard.FreePc",
        ["из 56"] = "Dashboard.OutOf56",
        ["Активные брони"] = "Dashboard.ActiveBookings",
        ["5 ожидают оплату"] = "Dashboard.PaymentWaiting",
        ["Активные сессии"] = "Dashboard.ActiveSessions",
        ["3 заканчиваются скоро"] = "Dashboard.EndingSoon",
        ["События сегодня"] = "Dashboard.TodayEvents",
        ["QUICK ACTIONS"] = "Common.QuickActions",
        ["Быстрые действия"] = "Common.QuickActions",
        ["Записаться на турнир"] = "Dashboard.JoinTournament",
        ["Купить пакет"] = "Dashboard.BuyPackage",
        ["Открыть профиль"] = "Dashboard.OpenProfile",
        ["Дата, зона, ПК, summary"] = "Dashboard.BookingHint",
        ["Dota 2, CS2, LAN Party"] = "Dashboard.TournamentHint",
        ["Quick Game / Night Pack"] = "Dashboard.PackageHint",
        ["Бонусы, сессии, брони"] = "Dashboard.ProfileHint",
        ["Сегодня в клубе"] = "Dashboard.TodayClub",
        ["Зоны клуба"] = "Dashboard.Zones",
        ["Последние уведомления"] = "Dashboard.LastNotifications",
        ["CLUB MAP"] = "Map.Kicker",
        ["Интерактивная схема клуба"] = "Map.Title",
        ["Зоны клуба, статусы ПК и ближайшие свободные интервалы видны без перехода в таблицы."] = "Map.Subtitle",
        ["ресепшен / турникет"] = "Map.EntranceSub",
        ["касса и старт сессий"] = "Map.AdminSub",
        ["склад / техника"] = "Map.ServiceSub",
        ["PC DETAIL"] = "Map.PcDetail",
        ["Выберите ПК"] = "Map.SelectPc",
        ["После выбора здесь появятся зона, статус, железо, тариф и ближайшие свободные интервалы."] = "Map.DetailPlaceholder",
        ["Фото места"] = "Map.PhotoPlace",
        ["Заглушка до загрузки реальных фото"] = "Map.PhotoPlaceholder",
        ["Свободные интервалы появятся после выбора места."] = "Map.FreeIntervals",
        ["Забронировать выбранный ПК"] = "Map.BookSelected",
        ["ПК недоступен для брони"] = "Map.PcUnavailable",
        ["BOOKING FLOW"] = "Booking.Kicker",
        ["Выберите дату, зону, один или несколько ПК, затем проверьте итог перед подтверждением."] = "Booking.Subtitle",
        ["Одиночная"] = "Booking.Single",
        ["Для компании"] = "Booking.Company",
        ["Зона"] = "Booking.Zone",
        ["Тариф"] = "Booking.Tariff",
        ["Начало"] = "Booking.Start",
        ["Выбор ПК"] = "Booking.SelectPc",
        ["SUMMARY"] = "Booking.Summary",
        ["Итог брони"] = "Booking.SummaryTitle",
        ["ПК"] = "Booking.Pc",
        ["Дата"] = "Booking.Date",
        ["Время"] = "Booking.Time",
        ["Длительность"] = "Booking.Duration",
        ["Без скидки"] = "Booking.BaseTotal",
        ["Скидка"] = "Booking.Discount",
        ["Итого"] = "Booking.Total",
        ["Подтвердить бронь"] = "Booking.Confirm",
        ["Очистить выбор"] = "Booking.Clear",
        ["1 час"] = "Booking.OneHour",
        ["2 часа"] = "Booking.TwoHours",
        ["3 часа"] = "Booking.ThreeHours",
        ["Часы"] = "Booking.Hours",
        ["Минуты"] = "Booking.Minutes",
        ["CLIENT PROFILE"] = "Cabinet.Kicker",
        ["Личный кабинет"] = "Cabinet.Title",
        ["Профиль игрока, лояльность, активные брони, сессии и быстрые действия."] = "Cabinet.Subtitle",
        ["Забронировать ПК"] = "Common.BookPc",
        ["Прогресс до Elite"] = "Cabinet.Progress",
        ["72% · осталось 420 бонусных очков"] = "Cabinet.ProgressValue",
        ["Активная бронь"] = "Cabinet.ActiveBooking",
        ["Следующая выгода"] = "Cabinet.NextBenefit",
        ["Подсказка обновится после загрузки тарифов."] = "Cabinet.NextBenefitText",
        ["Получено бонусов"] = "Cabinet.Bonuses",
        ["Наиграно"] = "Cabinet.Played",
        ["Любимая зона"] = "Cabinet.FavoriteZone",
        ["Пополнить баланс"] = "Cabinet.Topup",
        ["Текущая сессия"] = "Cabinet.LastSessions",
        ["Сумма"] = "Table.Amount",
        ["SETTINGS"] = "Nav.Settings",
        ["Смена темы приложения и языка интерфейса."] = "Settings.Subtitle",
        ["Тема приложения"] = "Settings.ThemeTitle",
        ["Black Gold оставляет премиальный стиль клуба, Graphite дает более холодный рабочий вид."] = "Settings.ThemeDescription",
        ["Язык интерфейса"] = "Settings.LanguageTitle",
        ["Переключает основные элементы навигации и страницу настроек."] = "Settings.LanguageDescription"
    };

    private readonly DispatcherTimer _toastTimer;
    private readonly DispatcherTimer _announcementTimer;
    private readonly DispatcherTimer _liveRefreshTimer;
    private readonly MainWindowViewModel _viewModel = new();
    private readonly HashSet<string> _selectedSeats = [];
    private ResourceDictionary _languageStrings = new();
    private DateTime _lastAnnouncementTick;
    private bool _isSidebarCollapsed;
    private string _currentView = "dashboard";
    private string _currentTheme = "BlackGold";
    private string _currentLanguage = "ru";
    private string _currentRole = "client";
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
        ApplyThemeResources(_currentTheme);
        InitializeComponent();
        DataContext = _viewModel;

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            StatusToast.Visibility = Visibility.Collapsed;
        };

        _announcementTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _announcementTimer.Tick += AnnouncementTimer_Tick;
        _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _liveRefreshTimer.Tick += (_, _) => LoadDatabaseState();

        ApplyLanguage(_currentLanguage, false);
        UpdateThemeButtons();

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

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;

        ApplySidebarState();

        ShowStatus(
            _isSidebarCollapsed ? "Меню свернуто" : "Меню раскрыто",
            _isSidebarCollapsed ? "Навигация оставлена в компактном режиме." : "Полная навигация снова доступна.");
    }

    private void LoadDatabaseState()
    {
        try
        {
            using var dbContext = new AppDbContext();
            NormalizeDatabaseState(dbContext);
            if (IsLoaded)
            {
                RefreshBookingDatesIfStale();
            }

            _computers.Clear();
            _computers.AddRange(dbContext.Computers
                .AsNoTracking()
                .OrderBy(computer => computer.Id)
                .ToList());

            _tariffs.Clear();
            _tariffs.AddRange(dbContext.Tariffs
                .AsNoTracking()
                .Where(tariff => tariff.IsActive)
                .OrderBy(tariff => tariff.Id)
                .ToList());

            RefreshEffectiveComputerStatuses(dbContext);

            var now = DateTime.Now;
            var activeSessions = dbContext.GameSessions.Count(session => session.Status != SessionStatuses.Closed
                && session.StartTime <= now
                && (session.EndTime == null || session.EndTime > now));
            var pendingBookings = dbContext.Bookings.Count(booking => booking.Status == BookingStatuses.PendingPayment
                && booking.EndTime > now);
            var pendingSessions = dbContext.GameSessions.Count(session => session.Status == SessionStatuses.AwaitingPayment
                && (session.EndTime == null || session.EndTime > now));
            var pendingTopups = dbContext.Payments.Count(payment =>
                payment.PaymentType.StartsWith(PaymentTypes.Pending)
                && payment.CreatedAt.Date == DateTime.Today);
            var freePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free);
            var servicePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Service);
            var today = DateTime.Today;
            var todayPayments = dbContext.Payments
                .AsNoTracking()
                .Where(payment => payment.CreatedAt.Date == today)
                .ToList();

            _adminActiveSessions = activeSessions;
            _adminPaymentQueue = pendingBookings + pendingSessions + pendingTopups;
            _adminFreePcs = freePcs;
            _adminSupportQueue = servicePcs;
            _shiftCash = todayPayments
                .Where(payment => IsConfirmedCashPayment(payment.PaymentType))
                .Sum(payment => payment.Amount);
            _shiftOnline = todayPayments
                .Where(payment => payment.Amount > 0 && IsConfirmedOnlinePayment(payment.PaymentType))
                .Sum(payment => payment.Amount);
            SyncAdminViewModel();

            UpdateDashboardSummary(dbContext);
            if (IsLoaded)
            {
                AdminPaymentQueueHintText.Text = $"{pendingBookings} броней, {pendingSessions} сессий, {pendingTopups} пополнений";
            }

            if (_currentUserId > 0)
            {
                var currentUser = dbContext.Users.AsNoTracking().FirstOrDefault(user => user.Id == _currentUserId);
                if (currentUser is not null)
                {
                    _currentUserFullName = currentUser.FullName;
                    _currentUserLogin = currentUser.Login;
                    _currentRole = NormalizeRole(currentUser.Role);
                    _balanceAmount = currentUser.Balance;
                    SyncCurrentUserViewModel();
                    BalanceAmountText.Text = $"{_balanceAmount:0.##} BYN";
                    UpdateCurrentUserUi();
                    RefreshClientUx(dbContext, currentUser);
                }
            }

            _standardRate = GetTariffPrice("Standard", _standardRate);
            _vipRate = GetTariffPrice("VIP", _vipRate);
            _royalRate = GetTariffPrice("Royal", _royalRate);
            _bootcampRate = GetTariffPrice("Bootcamp", _bootcampRate);
            _bookingTariff = GetTariffPrice(_bookingZoneKey, _bookingTariff);
            SyncBookingViewModel();
            UpdateDashboardLoadBars();
            UpdateAnnouncementText();
            UpdateCabinetNextBenefit();
            RebuildTodayClubList(dbContext);
            RebuildOwnerStaffList(dbContext);
            RefreshLiveViewsAfterDatabaseChange();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка загрузки БД", ex);
            // If SQL Server is unavailable, the screen keeps the last loaded values.
        }
    }

    private void ShowDatabaseError(string title, Exception ex)
    {
        if (IsLoaded)
        {
            ShowStatus(title, $"Не удалось выполнить операцию SQL Server. Проверь строку подключения и доступность сервера. {ex.Message}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"{title}: {ex}");
        }
    }

    private void SyncCurrentUserViewModel()
    {
        _viewModel.CurrentUser.Id = _currentUserId;
        _viewModel.CurrentUser.FullName = _currentUserFullName;
        _viewModel.CurrentUser.Login = _currentUserLogin;
        _viewModel.CurrentUser.Role = _currentRole;
        _viewModel.CurrentUser.Balance = _balanceAmount;
    }

    private void SyncBookingViewModel()
    {
        _viewModel.Booking.Date = _bookingDate;
        _viewModel.Booking.ZoneKey = _bookingZoneKey;
        _viewModel.Booking.ZoneName = _bookingZoneName;
        _viewModel.Booking.Tariff = _bookingTariff;
        _viewModel.Booking.Duration = _bookingDuration;
        _viewModel.Booking.Hour = _bookingHour;
        _viewModel.Booking.Minute = _bookingMinute;
        _viewModel.Booking.Package = _bookingPackage;
        _viewModel.Booking.IsCompanyBooking = _isCompanyBooking;
    }

    private void SyncAdminViewModel()
    {
        _viewModel.Admin.ActiveSessions = _adminActiveSessions;
        _viewModel.Admin.PaymentQueue = _adminPaymentQueue;
        _viewModel.Admin.FreePcs = _adminFreePcs;
        _viewModel.Admin.SupportQueue = _adminSupportQueue;
        _viewModel.Admin.ShiftCash = _shiftCash;
        _viewModel.Admin.ShiftOnline = _shiftOnline;
        _viewModel.Admin.ShiftClosed = _shiftClosed;
    }

    private void SyncOwnerViewModel()
    {
        _viewModel.Owner.Revenue = _ownerRevenue;
        _viewModel.Owner.Load = _ownerLoad;
        _viewModel.Owner.AverageCheck = _ownerAverageCheck;
        _viewModel.Owner.RepeatRate = _ownerRepeatRate;
        _viewModel.Owner.StandardRate = _standardRate;
        _viewModel.Owner.VipRate = _vipRate;
        _viewModel.Owner.RoyalRate = _royalRate;
        _viewModel.Owner.BootcampRate = _bootcampRate;
        _viewModel.Owner.DemandMode = _ownerDemandMode;
    }

    private void InitializeBookingDates()
    {
        var today = DateTime.Today;
        _bookingDate = today;

        ConfigureBookingDateButton(DateTodayButton, "Сегодня", today);
        ConfigureBookingDateButton(DateTomorrowButton, GetShortRuDay(today.AddDays(1)), today.AddDays(1));
        ConfigureBookingDateButton(DateThirdButton, GetShortRuDay(today.AddDays(2)), today.AddDays(2));
        ConfigureBookingDateButton(DateCustomButton, GetShortRuDay(today.AddDays(3)), today.AddDays(3));
        SetActiveButton(DateTodayButton, DateTodayButton, DateTomorrowButton, DateThirdButton, DateCustomButton);
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

    private int GetTariffPrice(string namePart, int fallback)
    {
        var tariff = _tariffs.FirstOrDefault(item =>
            item.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase));

        return tariff is null ? fallback : (int)Math.Round(tariff.PricePerHour);
    }

    private static bool IsConfirmedCashPayment(string paymentType)
    {
        return paymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfirmedOnlinePayment(string paymentType)
    {
        return paymentType.Equals("Card", StringComparison.OrdinalIgnoreCase)
            || paymentType.Equals("Online", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetNextId<TEntity>(DbSet<TEntity> dbSet, Expression<Func<TEntity, int>> idSelector)
        where TEntity : class
    {
        return dbSet.Any() ? dbSet.Max(idSelector) + 1 : 1;
    }

    private bool EnsureSignedInForDatabaseWrite()
    {
        if (_currentUserId > 0)
        {
            return true;
        }

        ShowStatus("Войдите в систему", "Операция не сохранена: пользователь не авторизован.");
        return false;
    }

    private int? ResolveCurrentOrAdminUserId(AppDbContext dbContext)
    {
        if (_currentUserId > 0 && dbContext.Users.Any(user => user.Id == _currentUserId))
        {
            return _currentUserId;
        }

        var adminId = dbContext.Users
            .Where(user => user.Role == "Admin")
            .OrderBy(user => user.Id)
            .Select(user => (int?)user.Id)
            .FirstOrDefault();
        return adminId;
    }
}
