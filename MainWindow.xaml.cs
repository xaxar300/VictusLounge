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
        ["Бонусы"] = "Cabinet.Bonuses",
        ["Наиграно"] = "Cabinet.Played",
        ["Любимая зона"] = "Cabinet.FavoriteZone",
        ["Пополнить баланс"] = "Cabinet.Topup",
        ["Последние сессии"] = "Cabinet.LastSessions",
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
    private decimal _balanceAmount;
    private int _unreadNotifications;
    private bool _suppressNotificationWrite;
    private int _adminActiveSessions = 21;
    private int _adminPaymentQueue = 5;
    private int _adminFreePcs = 42;
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
    // UI state is mutated only on the WPF dispatcher thread; background DB access is not used in this demo.
    private readonly Dictionary<string, string> _pcStatusOverrides = new(StringComparer.Ordinal);
    private readonly List<Computer> _computers = [];
    private readonly List<Tariff> _tariffs = [];
    private int? _activeCabinetBookingId;
    private int _currentUserId;
    private string _currentUserFullName = "Not signed in";
    private string _currentUserLogin = string.Empty;

    public MainWindow()
    {
        ApplyThemeResources(_currentTheme);
        InitializeComponent();

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            StatusToast.Visibility = Visibility.Collapsed;
        };

        _announcementTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _announcementTimer.Tick += AnnouncementTimer_Tick;

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

            foreach (var computer in _computers)
            {
                _pcStatusOverrides[computer.Name] = NormalizePcStatus(computer.Status);
            }

            var activeSessions = dbContext.GameSessions.Count(session => session.EndTime == null && session.Status != SessionStatuses.Closed);
            var pendingBookings = dbContext.Bookings.Count(booking => booking.Status == BookingStatuses.PendingPayment);
            var pendingSessions = dbContext.GameSessions.Count(session => session.Status == SessionStatuses.AwaitingPayment);
            var freePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free);
            var servicePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Service);
            var today = DateTime.Today;
            var todayPayments = dbContext.Payments
                .AsNoTracking()
                .Where(payment => payment.CreatedAt.Date == today)
                .ToList();

            _adminActiveSessions = activeSessions;
            _adminPaymentQueue = pendingBookings + pendingSessions;
            _adminFreePcs = freePcs;
            _adminSupportQueue = servicePcs;
            _shiftCash = todayPayments
                .Where(payment => IsConfirmedCashPayment(payment.PaymentType))
                .Sum(payment => payment.Amount);
            _shiftOnline = todayPayments
                .Where(payment => IsConfirmedOnlinePayment(payment.PaymentType))
                .Sum(payment => payment.Amount);

            if (_currentUserId > 0)
            {
                var currentUser = dbContext.Users.AsNoTracking().FirstOrDefault(user => user.Id == _currentUserId);
                if (currentUser is not null)
                {
                    _currentUserFullName = currentUser.FullName;
                    _currentUserLogin = currentUser.Login;
                    _currentRole = NormalizeRole(currentUser.Role);
                    _balanceAmount = currentUser.Balance;
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
            UpdateDashboardLoadBars();
            UpdateAnnouncementText();
            UpdateCabinetNextBenefit();
            RebuildTodayClubList(dbContext);
            RebuildOwnerStaffList(dbContext);
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка загрузки БД", ex);
            // If SQL Server is unavailable, the screen keeps the current demo values.
        }
    }

    private void ShowDatabaseError(string title, Exception ex)
    {
        if (IsLoaded)
        {
            _suppressNotificationWrite = true;
            ShowStatus(title, $"Не удалось выполнить операцию SQL Server. Проверь строку подключения и доступность сервера. {ex.Message}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"{title}: {ex}");
        }
    }

    private void UpdateDashboardLoadBars()
    {
        if (!IsLoaded)
        {
            return;
        }

        StandardZoneLoadBar.Value = CalculateZoneLoad("Standard");
        VipZoneLoadBar.Value = CalculateZoneLoad("VIP");
        BootcampZoneLoadBar.Value = CalculateZoneLoad("Bootcamp");
        RoyalZoneLoadBar.Value = CalculateZoneLoad("Royal");
    }

    private double CalculateZoneLoad(string zonePart)
    {
        var zoneComputers = _computers
            .Where(computer => computer.Zone.Contains(zonePart, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (zoneComputers.Count == 0)
        {
            return 0;
        }

        var busyCount = zoneComputers.Count(computer =>
        {
            var status = NormalizePcStatus(computer.Status);
            return status is PcStatuses.Busy or PcStatuses.Reserved or PcStatuses.Service;
        });

        return Math.Round((double)busyCount / zoneComputers.Count * 100);
    }

    private void UpdateAnnouncementText()
    {
        if (!IsLoaded)
        {
            return;
        }

        var freePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free);
        var busyPcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Busy);
        var servicePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Service);
        AnnouncementTextA.Text =
            $"Свободно ПК: {freePcs} · занято: {busyPcs} · сервис: {servicePcs} · Standard {_standardRate} BYN/ч · VIP {_vipRate} BYN/ч · Royal {_royalRate} BYN/ч ·";
        AnnouncementTextB.Text = AnnouncementTextA.Text;
        ResetAnnouncementMarquee();
    }

    private void UpdateCabinetNextBenefit()
    {
        if (!IsLoaded)
        {
            return;
        }

        const decimal eveningPackPrice = 29m;
        var regularFourHours = _standardRate * 4m;
        var saving = regularFourHours - eveningPackPrice;
        CabinetNextBenefitText.Text = saving > 0
            ? $"Evening Pack выгоднее обычной оплаты на {saving:0.##} BYN при игре 4 часа."
            : $"Для игры на 4 часа сейчас выгоднее обычный тариф Standard: {regularFourHours:0.##} BYN.";
    }

    private void RebuildTodayClubList(AppDbContext dbContext)
    {
        if (!IsLoaded)
        {
            return;
        }

        while (TodayClubList.Children.Count > 1)
        {
            TodayClubList.Children.RemoveAt(1);
        }

        var today = DateTime.Today;
        var computers = _computers.ToDictionary(computer => computer.Id);
        var users = dbContext.Users.AsNoTracking().ToDictionary(user => user.Id);
        var items = dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.StartTime.Date == today && booking.Status != BookingStatuses.Cancelled)
            .OrderBy(booking => booking.StartTime)
            .Take(3)
            .ToList();

        if (items.Count == 0)
        {
            var activeSessions = dbContext.GameSessions
                .AsNoTracking()
                .Where(session => session.EndTime == null && session.Status != SessionStatuses.Closed)
                .OrderBy(session => session.StartTime)
                .Take(3)
                .ToList();

            foreach (var session in activeSessions)
            {
                computers.TryGetValue(session.ComputerId, out var computer);
                users.TryGetValue(session.UserId, out var user);
                AddTodayClubItem(
                    session.StartTime.ToString("HH:mm"),
                    $"{computer?.Name ?? "ПК"} · {user?.FullName ?? "клиент"}",
                    $"{computer?.Zone ?? "-"} · активная сессия");
            }
        }
        else
        {
            foreach (var booking in items)
            {
                computers.TryGetValue(booking.ComputerId, out var computer);
                users.TryGetValue(booking.UserId, out var user);
                AddTodayClubItem(
                    booking.StartTime.ToString("HH:mm"),
                    $"{computer?.Name ?? "ПК"} · {user?.FullName ?? "клиент"}",
                    $"{computer?.Zone ?? "-"} · {booking.Status}");
            }
        }

        if (TodayClubList.Children.Count == 1)
        {
            TodayClubList.Children.Add(new TextBlock
            {
                Text = "На сегодня нет активных броней и сессий.",
                Foreground = (Brush)FindResource("MutedBrush"),
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    private void RebuildOwnerStaffList(AppDbContext dbContext)
    {
        if (!IsLoaded || OwnerStaffList is null)
        {
            return;
        }

        OwnerStaffList.Children.Clear();
        var shifts = dbContext.Shifts
            .AsNoTracking()
            .OrderByDescending(shift => shift.StartTime)
            .Take(3)
            .ToList();

        if (shifts.Count == 0)
        {
            OwnerStaffList.Children.Add(new TextBlock
            {
                Text = "Смены пока не заведены.",
                Foreground = (Brush)FindResource("MutedBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var shift in shifts)
        {
            var endText = shift.EndTime?.ToString("HH:mm") ?? "открыта";
            OwnerStaffList.Children.Add(new TextBlock
            {
                Text = $"{shift.EmployeeName} · {shift.StartTime:dd.MM HH:mm}-{endText} · касса {shift.CashTotal:0.##} BYN",
                Foreground = (Brush)FindResource("MutedBrush"),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    private void AddTodayClubItem(string time, string title, string subtitle)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, TodayClubList.Children.Count < 4 ? 13 : 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        row.Children.Add(new TextBlock
        {
            Text = time,
            Foreground = (Brush)FindResource("GoldLightBrush"),
            FontWeight = FontWeights.Bold
        });

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        text.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        TodayClubList.Children.Add(row);
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
            || paymentType.Equals("Online", StringComparison.OrdinalIgnoreCase)
            || paymentType.Equals(PaymentTypes.Bonus, StringComparison.OrdinalIgnoreCase);
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

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        NavigateTo(element.Tag?.ToString() ?? "dashboard");
    }

    private void ShowLoginAuth_Click(object sender, RoutedEventArgs e)
    {
        ShowAuthView(isRegister: false);
    }

    private void ShowRegisterAuth_Click(object sender, RoutedEventArgs e)
    {
        ShowAuthView(isRegister: true);
    }

    private void AuthEnter_Click(object sender, RoutedEventArgs e)
    {
        if (RegisterAuthView.Visibility == Visibility.Visible)
        {
            RegisterAndLogin();
            return;
        }

        LoginFromDatabase();
    }

    private void LogoutText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _currentUserId = 0;
        _currentUserFullName = "Not signed in";
        _currentUserLogin = string.Empty;
        _currentRole = "client";
        UpdateCurrentUserUi();
        UpdateAuthRoleButtons();
        ApplyRoleAccess(false);
        ShowAuthView(isRegister: false);
        AuthOverlay.Visibility = Visibility.Visible;
    }

    private void AuthRole_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string role)
        {
            return;
        }

        _currentRole = role;
        UpdateAuthRoleButtons();
        ShowStatus("Role selected", $"After login: {GetRoleTitle(_currentRole)} mode.");
    }

    private void ShowAuthView(bool isRegister)
    {
        LoginAuthView.Visibility = isRegister ? Visibility.Collapsed : Visibility.Visible;
        RegisterAuthView.Visibility = isRegister ? Visibility.Visible : Visibility.Collapsed;
        AuthErrorText.Visibility = Visibility.Collapsed;
        RegisterErrorText.Visibility = Visibility.Collapsed;
        AuthWindowTitleText.Text = isRegister
            ? "Регистрация в Elite Gaming Lounge"
            : "Вход в Elite Gaming Lounge";
        UpdateAuthRoleButtons();
    }

    private void LoginFromDatabase()
    {
        var login = LoginBox.Text.Trim();
        var password = LoginPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            ShowAuthError("Введите логин и пароль.");
            return;
        }

        try
        {
            using var dbContext = new AppDbContext();
            var user = dbContext.Users.AsNoTracking().FirstOrDefault(item => item.Login == login);
            if (user is null || !PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                ShowAuthError("Неверный логин или пароль.");
                return;
            }

            SignInUser(user);
        }
        catch (Exception ex)
        {
            ShowAuthError($"Не удалось подключиться к SQL Server: {ex.Message}");
        }
    }

    private void RegisterAndLogin()
    {
        var fullName = RegisterFullNameBox.Text.Trim();
        var login = RegisterLoginBox.Text.Trim();
        var password = RegisterPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            ShowRegisterError("Заполните имя, логин и пароль.");
            return;
        }

        try
        {
            using var dbContext = new AppDbContext();
            if (dbContext.Users.Any(user => user.Login == login))
            {
                ShowRegisterError("Пользователь с таким логином уже есть.");
                return;
            }

            var user = new User
            {
                Id = dbContext.Users.Any() ? dbContext.Users.Max(item => item.Id) + 1 : 1,
                FullName = fullName,
                Login = login,
                PasswordHash = PasswordHasher.HashPassword(password),
                Role = "Client",
                Balance = 0m
            };

            dbContext.Users.Add(user);
            dbContext.SaveChanges();
            SignInUser(user);
        }
        catch (Exception ex)
        {
            ShowRegisterError($"Не удалось сохранить пользователя в SQL Server: {ex.Message}");
        }
    }

    private void SignInUser(User user)
    {
        _currentUserId = user.Id;
        _currentUserFullName = user.FullName;
        _currentUserLogin = user.Login;
        _currentRole = NormalizeRole(user.Role);
        _balanceAmount = user.Balance;

        AuthErrorText.Visibility = Visibility.Collapsed;
        RegisterErrorText.Visibility = Visibility.Collapsed;
        AuthOverlay.Visibility = Visibility.Collapsed;

        LoadDatabaseState();
        UpdateCurrentUserUi();
        UpdateAuthRoleButtons();
        ApplyRoleAccess();
        ShowStatus("Вход выполнен", $"{_currentUserFullName}: открыт режим {GetRoleTitle(_currentRole)}.");
    }

    private void ShowAuthError(string message)
    {
        AuthErrorText.Text = message;
        AuthErrorText.Visibility = Visibility.Visible;
    }

    private void ShowRegisterError(string message)
    {
        RegisterErrorText.Text = message;
        RegisterErrorText.Visibility = Visibility.Visible;
    }

    private void UpdateCurrentUserUi()
    {
        if (!IsLoaded)
        {
            return;
        }

        ProfileNameText.Text = _currentUserFullName;
        ProfileRoleText.Text = $"{GetRoleTitle(_currentRole)} · {_currentUserLogin}";
        ProfileInitialsText.Text = GetInitials(_currentUserFullName);
        WorkspaceText.Text = $"{GetRoleTitle(_currentRole)} workspace";
        BalanceAmountText.Text = $"{_balanceAmount:0.##} BYN";
    }

    private void RefreshClientUx(AppDbContext dbContext, User user)
    {
        if (!IsLoaded)
        {
            return;
        }

        var userSessions = dbContext.GameSessions
            .AsNoTracking()
            .Where(session => session.UserId == user.Id)
            .ToList();
        var computers = dbContext.Computers.AsNoTracking().ToDictionary(computer => computer.Id);
        var userPayments = dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.UserId == user.Id)
            .OrderByDescending(payment => payment.CreatedAt)
            .ToList();
        var activeBooking = dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.UserId == user.Id
                && booking.Status != BookingStatuses.Cancelled)
            .OrderByDescending(booking => booking.CreatedAt)
            .ThenByDescending(booking => booking.Id)
            .FirstOrDefault();

        var playedHours = userSessions
            .Where(session => session.EndTime is not null)
            .Sum(session => Math.Max(0, (session.EndTime!.Value - session.StartTime).TotalHours));
        var bonus = userPayments
            .Where(payment => payment.PaymentType.Equals(PaymentTypes.Bonus, StringComparison.OrdinalIgnoreCase))
            .Sum(payment => payment.Amount);
        var favoriteZone = userSessions
            .Where(session => computers.ContainsKey(session.ComputerId))
            .GroupBy(session => computers[session.ComputerId].Zone)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault() ?? "-";
        var progress = Math.Clamp((int)Math.Round((user.Balance + bonus) % 100), 0, 100);

        CabinetUserNameText.Text = user.FullName;
        CabinetTierText.Text = $"{GetClientTier(user.Balance)} · {user.Login}";
        CabinetProgressText.Text = $"{progress}% · бонусов: {bonus:0.##}";
        CabinetBalanceText.Text = $"{user.Balance:0.##} BYN";
        CabinetBonusText.Text = $"{bonus:0.##}";
        CabinetPlayedText.Text = $"{playedHours:0.#} ч";
        CabinetFavoriteZoneText.Text = favoriteZone;
        BalanceAmountText.Text = $"{user.Balance:0.##} BYN";
        BalanceBonusText.Text = $"Бонусный баланс: {bonus:0.##}";

        if (activeBooking is not null && computers.TryGetValue(activeBooking.ComputerId, out var bookingComputer))
        {
            var duration = Math.Max(1, (decimal)(activeBooking.EndTime - activeBooking.StartTime).TotalHours);
            var price = bookingComputer.HourPrice * duration;
            CabinetActiveBookingText.Text = $"{bookingComputer.Name} · {activeBooking.StartTime:dd.MM HH:mm}–{activeBooking.EndTime:HH:mm}";
            CabinetActiveBookingPriceText.Text = $"{price:0.##} BYN";
            CabinetCancelBookingButton.Visibility = Visibility.Visible;
            _activeCabinetBookingId = activeBooking.Id;
            CabinetBookingCardPcText.Text = bookingComputer.Name;
            CabinetBookingCardTimeText.Text = $"{activeBooking.StartTime:dd.MM HH:mm}–{activeBooking.EndTime:HH:mm}";
            CabinetBookingCardPriceText.Text = $"{bookingComputer.Zone} · {price:0.##} BYN";
        }
        else
        {
            CabinetActiveBookingText.Text = "Нет активной брони";
            CabinetActiveBookingPriceText.Text = "0 BYN";
            CabinetCancelBookingButton.Visibility = Visibility.Collapsed;
            _activeCabinetBookingId = null;
            CabinetBookingCardPcText.Text = "Нет брони";
            CabinetBookingCardTimeText.Text = string.Empty;
            CabinetBookingCardPriceText.Text = string.Empty;
        }

        RebuildCabinetSessionsGrid(userSessions, computers);
        RebuildBalanceHistoryGrid(userPayments);
    }

    private void CabinetCancelBooking_Click(object sender, RoutedEventArgs e)
    {
        if (_activeCabinetBookingId is null)
        {
            ShowStatus("Бронь не выбрана", "В кабинете нет активной брони для отмены.");
            return;
        }

        if (CancelBooking(_activeCabinetBookingId.Value))
        {
            LoadDatabaseState();
            RefreshAdminUx();
            ShowStatus("Бронь отменена", "Статус брони обновлен в базе данных.");
            return;
        }

        ShowStatus("Бронь не отменена", "Не удалось обновить статус брони в базе данных.");
    }

    private bool CancelBooking(int bookingId)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var booking = dbContext.Bookings.FirstOrDefault(item => item.Id == bookingId && item.UserId == _currentUserId);
            if (booking is null || booking.Status == BookingStatuses.Cancelled)
            {
                return false;
            }

            booking.Status = BookingStatuses.Cancelled;

            var hasOtherActiveBooking = dbContext.Bookings.Any(item =>
                item.Id != booking.Id
                && item.ComputerId == booking.ComputerId
                && item.Status != BookingStatuses.Cancelled
                && item.StartTime < booking.EndTime
                && item.EndTime > booking.StartTime);

            var computer = dbContext.Computers.FirstOrDefault(item => item.Id == booking.ComputerId);
            if (computer is not null && !hasOtherActiveBooking)
            {
                computer.Status = PcStatuses.Free;
            }

            dbContext.SaveChanges();
            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка отмены брони", ex);
            return false;
        }
    }

    private void RebuildCabinetSessionsGrid(IReadOnlyCollection<GameSession> sessions, IReadOnlyDictionary<int, Computer> computers)
    {
        CabinetSessionsGrid.Children.Clear();
        CabinetSessionsGrid.ColumnDefinitions.Clear();
        CabinetSessionsGrid.RowDefinitions.Clear();

        foreach (var width in new[] { "0.7*", "0.8*", "1.2*", "0.7*", "0.8*" })
        {
            CabinetSessionsGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = (GridLength)new GridLengthConverter().ConvertFromString(width)!
            });
        }

        AddCabinetSessionRow(0, "Дата", "ПК", "Зона", "Время", "Сумма", true);

        var rows = sessions
            .OrderByDescending(session => session.StartTime)
            .Take(3)
            .ToList();

        if (rows.Count == 0)
        {
            AddCabinetSessionRow(1, "-", "Нет сессий", "-", "-", "-", false);
            return;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var session = rows[i];
            computers.TryGetValue(session.ComputerId, out var computer);
            var hours = session.EndTime is null
                ? Math.Max(1, (DateTime.Now - session.StartTime).TotalHours)
                : Math.Max(1, (session.EndTime.Value - session.StartTime).TotalHours);

            AddCabinetSessionRow(
                i + 1,
                session.StartTime.ToString("dd.MM"),
                computer?.Name ?? "-",
                computer?.Zone ?? "-",
                $"{hours:0.#} ч",
                $"{session.TotalPrice:0.##} BYN",
                false);
        }
    }

    private void AddCabinetSessionRow(int row, string date, string pc, string zone, string time, string amount, bool isHeader)
    {
        CabinetSessionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddCabinetSessionCell(row, 0, date, isHeader, false);
        AddCabinetSessionCell(row, 1, pc, isHeader, false);
        AddCabinetSessionCell(row, 2, zone, isHeader, false);
        AddCabinetSessionCell(row, 3, time, isHeader, false);
        AddCabinetSessionCell(row, 4, amount, isHeader, true);
    }

    private void AddCabinetSessionCell(int row, int column, string text, bool isHeader, bool alignRight)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
            Foreground = (Brush)FindResource(isHeader ? "GoldLightBrush" : column is 0 or 2 ? "MutedBrush" : "TextBrush"),
            Margin = row == 0 ? new Thickness(0) : new Thickness(0, 12, 0, 0),
            HorizontalAlignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left
        };

        Grid.SetRow(textBlock, row);
        Grid.SetColumn(textBlock, column);
        CabinetSessionsGrid.Children.Add(textBlock);
    }

    private void RefreshBalanceHistoryFromDatabase()
    {
        if (BalanceHistoryGrid is null)
        {
            return;
        }

        if (_currentUserId <= 0)
        {
            RebuildBalanceHistoryGrid(Array.Empty<Payment>());
            return;
        }

        try
        {
            using var dbContext = new AppDbContext();
            var payments = dbContext.Payments
                .AsNoTracking()
                .Where(payment => payment.UserId == _currentUserId)
                .OrderByDescending(payment => payment.CreatedAt)
                .Take(8)
                .ToList();
            RebuildBalanceHistoryGrid(payments);
        }
        catch
        {
            RebuildBalanceHistoryGrid(Array.Empty<Payment>());
        }
    }

    private void RebuildBalanceHistoryGrid(IReadOnlyList<Payment> payments)
    {
        if (BalanceHistoryGrid is null)
        {
            return;
        }

        var headerChildren = BalanceHistoryGrid.Children
            .OfType<UIElement>()
            .Where(child => Grid.GetRow(child) == 0)
            .ToArray();
        BalanceHistoryGrid.Children.Clear();
        BalanceHistoryGrid.RowDefinitions.Clear();
        BalanceHistoryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        foreach (var child in headerChildren)
        {
            BalanceHistoryGrid.Children.Add(child);
        }

        if (payments.Count == 0)
        {
            BalanceHistoryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var emptyText = new TextBlock
            {
                Text = "Пока нет операций по балансу.",
                Foreground = (Brush)FindResource("MutedBrush"),
                Margin = new Thickness(0, 13, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(emptyText, 1);
            Grid.SetColumn(emptyText, 0);
            Grid.SetColumnSpan(emptyText, 5);
            BalanceHistoryGrid.Children.Add(emptyText);
            return;
        }

        var visible = payments.Take(8).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            var payment = visible[i];
            var rowIndex = i + 1;
            BalanceHistoryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var status = FormatPaymentStatus(payment);
            var (amountBrush, statusBrush) = ResolveBalanceHistoryBrushes(payment, status);

            AddBalanceHistoryCell(rowIndex, 0, payment.CreatedAt.ToString("dd.MM"), "MutedBrush", FontWeights.Normal);
            AddBalanceHistoryCell(rowIndex, 1, FormatPaymentOperation(payment), "TextBrush", FontWeights.Bold);
            AddBalanceHistoryCell(rowIndex, 2, FormatPaymentMethod(payment), "MutedBrush", FontWeights.Normal);
            AddBalanceHistoryCell(rowIndex, 3, FormatPaymentAmount(payment), amountBrush, FontWeights.Bold);
            AddBalanceHistoryCell(rowIndex, 4, status, statusBrush, FontWeights.Bold, alignRight: true);
        }
    }

    private void AddBalanceHistoryCell(int row, int column, string text, string brushKey, FontWeight weight, bool alignRight = false)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource(brushKey),
            FontWeight = weight,
            Margin = new Thickness(0, 13, 0, 0),
            HorizontalAlignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        BalanceHistoryGrid.Children.Add(block);
    }

    private static string FormatPaymentAmount(Payment payment)
    {
        if (payment.Amount > 0)
        {
            return $"+{payment.Amount:0.##} BYN";
        }
        if (payment.Amount < 0)
        {
            return $"{payment.Amount:0.##} BYN";
        }
        return "0 BYN";
    }

    private static (string AmountBrush, string StatusBrush) ResolveBalanceHistoryBrushes(Payment payment, string status)
    {
        var amountBrush = string.Equals(payment.PaymentType, "Bonus", StringComparison.OrdinalIgnoreCase)
            ? "GoldLightBrush"
            : payment.Amount < 0
                ? "DangerBrush"
                : "OkBrush";
        var statusBrush = status switch
        {
            "Ожидает" => "WaitBrush",
            "Начислено" => "GoldLightBrush",
            _ => "OkBrush"
        };
        return (amountBrush, statusBrush);
    }

    private static string FormatPaymentOperation(Payment payment)
    {
        var comment = payment.Comment ?? string.Empty;
        if (string.IsNullOrWhiteSpace(comment))
        {
            return string.Equals(payment.PaymentType, "Bonus", StringComparison.OrdinalIgnoreCase)
                ? "Бонус"
                : "Операция";
        }
        if (comment.StartsWith("Pending balance top-up", StringComparison.OrdinalIgnoreCase))
        {
            return "Ожидание пополнения";
        }
        if (comment.Contains("Balance top-up", StringComparison.OrdinalIgnoreCase))
        {
            return "Пополнение баланса";
        }
        if (comment.StartsWith("Package purchase", StringComparison.OrdinalIgnoreCase))
        {
            var separator = comment.IndexOf(';');
            var head = separator > 0 ? comment[..separator] : comment;
            return head.Replace("Package purchase", "Покупка пакета", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Guest session", StringComparison.OrdinalIgnoreCase))
        {
            return comment.Replace("Guest session", "Гостевая сессия", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Session extension", StringComparison.OrdinalIgnoreCase))
        {
            return comment.Replace("Session extension", "Продление сессии", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Payment confirmed", StringComparison.OrdinalIgnoreCase))
        {
            return comment.Replace("Payment confirmed", "Оплата сессии", StringComparison.OrdinalIgnoreCase);
        }
        if (comment.StartsWith("Shift expense", StringComparison.OrdinalIgnoreCase))
        {
            return "Расход смены";
        }
        if (comment.StartsWith("Bulk payment", StringComparison.OrdinalIgnoreCase))
        {
            return "Подтверждение очереди оплат";
        }
        return comment.Length > 60 ? comment[..60] + "…" : comment;
    }

    private static string FormatPaymentMethod(Payment payment)
    {
        var paymentType = payment.PaymentType ?? string.Empty;
        return paymentType switch
        {
            "Card" => "Карта",
            "Cash" => "Наличные",
            "Online" => "Онлайн",
            "Bonus" => "Бонусы",
            "PendingErip" => "ЕРИП",
            "PendingCash" => "Наличные",
            _ when paymentType.StartsWith("Pending", StringComparison.OrdinalIgnoreCase) => "Ожидание",
            _ => paymentType
        };
    }

    private static string FormatPaymentStatus(Payment payment)
    {
        var paymentType = payment.PaymentType ?? string.Empty;
        if (paymentType.StartsWith("Pending", StringComparison.OrdinalIgnoreCase))
        {
            return "Ожидает";
        }
        if (string.Equals(paymentType, "Bonus", StringComparison.OrdinalIgnoreCase))
        {
            return "Начислено";
        }
        return payment.Amount < 0 ? "Списано" : "Успешно";
    }

    private static string GetClientTier(decimal balance)
    {
        return balance switch
        {
            >= 150 => "Elite",
            >= 75 => "Gold",
            >= 25 => "Silver",
            _ => "Bronze"
        };
    }

    private static string NormalizeRole(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "admin" => "admin",
            "owner" => "owner",
            _ => "client"
        };
    }

    private static string GetInitials(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "??";
        }

        var initials = string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
        return initials.Length == 1 ? $"{initials}." : initials;
    }

    private void UpdateAuthRoleButtons()
    {
        if (!IsLoaded)
        {
            return;
        }

        foreach (var button in new[]
        {
            LoginRoleClientButton,
            LoginRoleAdminButton,
            LoginRoleOwnerButton,
            RegisterRoleClientButton,
            RegisterRoleAdminButton,
            RegisterRoleOwnerButton
        })
        {
            button.Style = (Style)FindResource(button.Tag?.ToString() == _currentRole ? "PrimaryButtonStyle" : "GhostButtonStyle");
        }
    }

    private void ApplyRoleAccess(bool navigateIfNeeded = true)
    {
        BookingNavButton.Visibility = _currentRole == "owner" ? Visibility.Collapsed : Visibility.Visible;
        CabinetNavButton.Visibility = _currentRole == "client" ? Visibility.Visible : Visibility.Collapsed;
        BalanceNavButton.Visibility = _currentRole == "client" ? Visibility.Visible : Visibility.Collapsed;
        AdminNavButton.Visibility = _currentRole is "admin" or "owner" ? Visibility.Visible : Visibility.Collapsed;
        ShiftNavButton.Visibility = _currentRole is "admin" or "owner" ? Visibility.Visible : Visibility.Collapsed;
        OwnerNavButton.Visibility = _currentRole == "owner" ? Visibility.Visible : Visibility.Collapsed;
        SettingsNavButton.Visibility = Visibility.Visible;

        WorkspaceText.Text = $"{GetRoleTitle(_currentRole)} workspace";

        if (navigateIfNeeded && !IsViewAllowedForRole(_currentView))
        {
            NavigateTo("dashboard");
        }

        ApplySidebarState(false);
    }

    private bool IsViewAllowedForRole(string view)
    {
        return _currentRole switch
        {
            "client" => view is "dashboard" or "map" or "booking" or "cabinet" or "balance" or "events" or "settings",
            "admin" => view is "dashboard" or "map" or "booking" or "events" or "admin" or "shift" or "settings",
            "owner" => view is "dashboard" or "map" or "events" or "admin" or "shift" or "owner" or "settings",
            _ => view is "dashboard" or "settings"
        };
    }

    private static string GetRoleTitle(string role)
    {
        return role switch
        {
            "admin" => "Admin",
            "owner" => "Owner",
            _ => "Client"
        };
    }
    private void ApplySidebarState(bool focusToggle = true)
    {
        SidebarColumn.Width = new GridLength(_isSidebarCollapsed ? 112 : 264);
        Sidebar.Padding = _isSidebarCollapsed ? new Thickness(10, 19, 10, 19) : new Thickness(19);

        BrandTitle.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        BrandSubtitle.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        BrandTextPanel.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        SidebarProfileCard.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        ProfileText.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        LogoutText.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        SidebarVersionText.HorizontalAlignment = _isSidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        SidebarToggle.Content = _isSidebarCollapsed ? ">" : "<";

        foreach (var button in new[]
        {
            DashboardNavButton,
            MapNavButton,
            BookingNavButton,
            CabinetNavButton,
            BalanceNavButton,
            EventsNavButton,
            AdminNavButton,
            ShiftNavButton,
            OwnerNavButton,
            SettingsNavButton
        })
        {
            ApplySidebarButtonState(button);
        }

        if (focusToggle)
        {
            SidebarToggle.Focus();
        }
    }

    private void ApplySidebarButtonState(Button button)
    {
        if (button.Content is not StackPanel stack)
        {
            return;
        }

        stack.HorizontalAlignment = _isSidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;

        var textBlocks = stack.Children.OfType<TextBlock>().ToArray();
        if (textBlocks.Length == 0)
        {
            return;
        }

        textBlocks[0].Width = _isSidebarCollapsed ? 44 : 30;
        textBlocks[0].TextAlignment = TextAlignment.Center;

        foreach (var label in textBlocks.Skip(1))
        {
            label.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void NavigateTo(string view)
    {
        if (!IsViewAllowedForRole(view))
        {
            ShowStatus("Access denied", $"{GetRoleTitle(_currentRole)} role cannot open this section.");
            return;
        }

        _currentView = view;
        DashboardView.Visibility = view == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        MapView.Visibility = view == "map" ? Visibility.Visible : Visibility.Collapsed;
        BookingView.Visibility = view == "booking" ? Visibility.Visible : Visibility.Collapsed;
        CabinetView.Visibility = view == "cabinet" ? Visibility.Visible : Visibility.Collapsed;
        BalanceView.Visibility = view == "balance" ? Visibility.Visible : Visibility.Collapsed;
        EventsView.Visibility = view == "events" ? Visibility.Visible : Visibility.Collapsed;
        AdminView.Visibility = view == "admin" ? Visibility.Visible : Visibility.Collapsed;
        ShiftView.Visibility = view == "shift" ? Visibility.Visible : Visibility.Collapsed;
        OwnerView.Visibility = view == "owner" ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = view == "settings" ? Visibility.Visible : Visibility.Collapsed;

        SetNavState(DashboardNavButton, view == "dashboard");
        SetNavState(MapNavButton, view == "map");
        SetNavState(BookingNavButton, view == "booking");
        SetNavState(CabinetNavButton, view == "cabinet");
        SetNavState(BalanceNavButton, view == "balance");
        SetNavState(EventsNavButton, view == "events");
        SetNavState(AdminNavButton, view == "admin");
        SetNavState(ShiftNavButton, view == "shift");
        SetNavState(OwnerNavButton, view == "owner");
        SetNavState(SettingsNavButton, view == "settings");

        var title = view switch
        {
            "map" => T("Nav.Map"),
            "booking" => T("Nav.Booking"),
            "cabinet" => T("Nav.Cabinet"),
            "balance" => T("Nav.Balance"),
            "events" => T("Nav.Events"),
            "admin" => T("Nav.Admin"),
            "shift" => T("Nav.Shift"),
            "owner" => T("Nav.Owner"),
            "settings" => T("Nav.Settings"),
            _ => T("Nav.Dashboard")
        };
        CurrentViewText.Text = title;
        ShowStatus(title, $"Открыт раздел: {title}.");
    }

    private void SetNavState(Button button, bool isActive)
    {
        var accent = (Color)Application.Current.Resources["GoldColor"];
        button.Background = isActive
            ? new SolidColorBrush(Color.FromArgb(0x2B, accent.R, accent.G, accent.B))
            : Brushes.Transparent;
        button.Foreground = (Brush)FindResource(isActive ? "TextBrush" : "MutedBrush");
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string theme)
        {
            ApplyTheme(theme);
        }
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string language)
        {
            ApplyLanguage(language);
        }
    }

    private void ApplyTheme(string theme, bool showToast = true)
    {
        _currentTheme = theme;
        ApplyThemeResources(theme);
        UpdateThemeButtons();
        SetNavState(DashboardNavButton, _currentView == "dashboard");
        SetNavState(MapNavButton, _currentView == "map");
        SetNavState(BookingNavButton, _currentView == "booking");
        SetNavState(CabinetNavButton, _currentView == "cabinet");
        SetNavState(BalanceNavButton, _currentView == "balance");
        SetNavState(EventsNavButton, _currentView == "events");
        SetNavState(AdminNavButton, _currentView == "admin");
        SetNavState(ShiftNavButton, _currentView == "shift");
        SetNavState(OwnerNavButton, _currentView == "owner");
        SetNavState(SettingsNavButton, _currentView == "settings");

        if (showToast)
        {
            ShowStatus(T("Settings.Applied"), T("Settings.ThemeApplied"));
        }
    }

    private void ApplyThemeResources(string theme)
    {
        var themeDictionary = LoadDictionary($"Resources/Themes.{theme}.xaml");

        SetThemeColor(themeDictionary, "BgColor");
        SetThemeColor(themeDictionary, "ShellColor");
        SetThemeColor(themeDictionary, "PanelColor");
        SetThemeColor(themeDictionary, "SurfaceColor");
        SetThemeColor(themeDictionary, "TextColor");
        SetThemeColor(themeDictionary, "MutedColor");
        SetThemeColor(themeDictionary, "DimColor");
        SetThemeColor(themeDictionary, "GoldColor");
        SetThemeColor(themeDictionary, "GoldLightColor");
        SetThemeColor(themeDictionary, "GoldDarkColor");
        SetThemeColor(themeDictionary, "LineColor");
        SetThemeColor(themeDictionary, "LineSoftColor");
        SetThemeColor(themeDictionary, "OkColor");
        SetThemeColor(themeDictionary, "DangerColor");
        SetThemeColor(themeDictionary, "WaitColor");

        SetBrushColor("ShellBrush", "ShellColor");
        SetBrushColor("PanelBrush", "PanelColor");
        SetBrushColor("SurfaceBrush", "SurfaceColor");
        SetBrushColor("TextBrush", "TextColor");
        SetBrushColor("MutedBrush", "MutedColor");
        SetBrushColor("DimBrush", "DimColor");
        SetBrushColor("GoldBrush", "GoldColor");
        SetBrushColor("GoldLightBrush", "GoldLightColor");
        SetBrushColor("LineBrush", "LineColor");
        SetBrushColor("LineSoftBrush", "LineSoftColor");
        SetBrushColor("OkBrush", "OkColor");
        SetBrushColor("DangerBrush", "DangerColor");
        SetBrushColor("WaitBrush", "WaitColor");
        SetGradient("WindowBackgroundBrush", "BgColor", "ShellColor", "BgColor");
        SetGradient("ShellBackgroundBrush", "ShellColor", "BgColor");
        SetGradient("TitlebarBrush", "ShellColor", "SurfaceColor");
        SetGradient("GoldButtonBrush", "GoldLightColor", "GoldDarkColor");
        SetGradient("PanelGradientBrush", "PanelColor", "SurfaceColor");
    }

    private void UpdateThemeButtons()
    {
        BlackGoldThemeButton.Style = (Style)FindResource(_currentTheme == "BlackGold" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        GraphiteThemeButton.Style = (Style)FindResource(_currentTheme == "Graphite" ? "PrimaryButtonStyle" : "GhostButtonStyle");
    }

    private void ApplyLanguage(string language, bool showToast = true)
    {
        _currentLanguage = language;
        _languageStrings = LoadDictionary($"Resources/Strings.{language}.xaml");

        TopLanguageLabel.Text = T("Top.Language");
        WorkspaceText.Text = T("Top.Workspace");
        if (GlobalSearchBox.Text == SearchPlaceholder ||
            GlobalSearchBox.Text == "Search: client, PC-04, booking, tournament, payment...")
        {
            GlobalSearchBox.Text = T("Common.SearchPlaceholder");
        }

        SettingsTitleText.Text = T("Settings.Title");
        SettingsSubtitleText.Text = T("Settings.Subtitle");
        SettingsThemeTitleText.Text = T("Settings.ThemeTitle");
        SettingsThemeDescriptionText.Text = T("Settings.ThemeDescription");
        SettingsLanguageTitleText.Text = T("Settings.LanguageTitle");
        SettingsLanguageDescriptionText.Text = T("Settings.LanguageDescription");
        BlackGoldThemeButton.Content = T("Settings.BlackGold");
        GraphiteThemeButton.Content = T("Settings.Graphite");
        ApplyBalanceLanguage();

        SetNavButtonText(DashboardNavButton, T("Nav.Dashboard"));
        SetNavButtonText(MapNavButton, T("Nav.Map"));
        SetNavButtonText(BookingNavButton, T("Nav.Booking"));
        SetNavButtonText(CabinetNavButton, T("Nav.Cabinet"));
        SetNavButtonText(BalanceNavButton, T("Nav.Balance"));
        SetNavButtonText(EventsNavButton, T("Nav.Events"));
        SetNavButtonText(AdminNavButton, T("Nav.Admin"));
        SetNavButtonText(ShiftNavButton, T("Nav.Shift"));
        SetNavButtonText(OwnerNavButton, T("Nav.Owner"));
        SetNavButtonText(SettingsNavButton, T("Nav.Settings"));

        RuLanguageButton.Style = (Style)FindResource(language == "ru" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        EnLanguageButton.Style = (Style)FindResource(language == "en" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        TopRuButton.Style = (Style)FindResource(language == "ru" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        TopEnButton.Style = (Style)FindResource(language == "en" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        CurrentViewText.Text = _currentView switch
        {
            "map" => T("Nav.Map"),
            "booking" => T("Nav.Booking"),
            "cabinet" => T("Nav.Cabinet"),
            "balance" => T("Nav.Balance"),
            "events" => T("Nav.Events"),
            "admin" => T("Nav.Admin"),
            "shift" => T("Nav.Shift"),
            "owner" => T("Nav.Owner"),
            "settings" => T("Nav.Settings"),
            _ => T("Nav.Dashboard")
        };
        LocalizeVisualTree(this);
        ApplyRoleAccess(false);
        ApplyMapPcButtonStatuses();
        RebuildBookingSeatGrid();
        UpdateBookingSeatButtons();

        if (showToast)
        {
            ShowStatus(T("Settings.Applied"), T("Settings.LanguageApplied"));
        }
    }

    private static ResourceDictionary LoadDictionary(string path)
    {
        return (ResourceDictionary)Application.LoadComponent(new Uri(path, UriKind.Relative));
    }

    private string T(string key)
    {
        return _languageStrings.Contains(key) ? _languageStrings[key]?.ToString() ?? key : key;
    }

    private void ApplyBalanceLanguage()
    {
        if (!IsLoaded)
        {
            return;
        }

        BalanceKickerText.Text = T("Balance.Kicker");
        BalanceTitleText.Text = T("Balance.Title");
        BalanceSubtitleText.Text = T("Balance.Subtitle");
        BalanceTopupCardButton.Content = T("Balance.Topup");
        BalanceCurrentLabelText.Text = T("Balance.Current");
        BalanceBonusText.Text = T("Balance.Bonus");
        BalancePromoLabelText.Text = T("Balance.Promo");
        BalanceApplyPromoButton.Content = T("Balance.Apply");
        BalancePackagesTitleText.Text = T("Balance.Packages");
        QuickGamePackageText.Text = T("Balance.QuickGame");
        EveningPackageText.Text = T("Balance.EveningPack");
        NightPackageText.Text = T("Balance.NightPack");
        BootcampPackageText.Text = T("Balance.BootcampTraining");
        WeekendPackageText.Text = T("Balance.WeekendPass");
        QuickGameBuyButton.Content = T("Balance.Buy");
        EveningBuyButton.Content = T("Balance.Buy");
        NightBuyButton.Content = T("Balance.Buy");
        BootcampBuyButton.Content = T("Balance.Buy");
        WeekendBuyButton.Content = T("Balance.Buy");
        BalancePersonalOfferLabelText.Text = T("Balance.PersonalOffer");
        BalancePersonalOfferText.Text = T("Balance.PersonalOfferText");
        BalanceOfferButton.Content = T("Balance.Activate");
        BalanceHistoryTitleText.Text = T("Balance.History");
        BalanceReceiptButton.Content = T("Balance.Export");
        BalanceDateHeaderText.Text = T("Table.Date");
        BalanceOperationHeaderText.Text = T("Balance.Operation");
        BalanceMethodHeaderText.Text = T("Balance.Method");
        BalanceSumHeaderText.Text = T("Table.Amount");
        BalanceStatusHeaderText.Text = T("Balance.Status");
        RefreshBalanceHistoryFromDatabase();
    }

    private string GetStatusText(string status, bool useMaintenanceWord = false)
    {
        return status switch
        {
            PcStatuses.Free => T("Status.Free"),
            PcStatuses.Busy => T("Status.Busy"),
            PcStatuses.Reserved => T("Status.Reserved"),
            PcStatuses.Service => T(useMaintenanceWord ? "Status.Maintenance" : "Status.Service"),
            _ => status
        };
    }

    private static void SetThemeColor(ResourceDictionary themeDictionary, string colorKey)
    {
        Application.Current.Resources[colorKey] = (Color)themeDictionary[$"Theme.{colorKey}"];
    }

    private static void SetBrushColor(string brushKey, string colorKey)
    {
        var color = (Color)Application.Current.Resources[colorKey];
        if (Application.Current.Resources[brushKey] is SolidColorBrush brush)
        {
            if (brush.IsFrozen)
            {
                Application.Current.Resources[brushKey] = new SolidColorBrush(color);
                return;
            }

            brush.Color = color;
            return;
        }

        Application.Current.Resources[brushKey] = new SolidColorBrush(color);
    }

    private static void SetGradient(string brushKey, params string[] colorKeys)
    {
        if (Application.Current.Resources[brushKey] is not LinearGradientBrush brush)
        {
            Application.Current.Resources[brushKey] = CreateGradient(colorKeys);
            return;
        }

        if (brush.IsFrozen)
        {
            Application.Current.Resources[brushKey] = CreateGradient(colorKeys);
            return;
        }

        for (var i = 0; i < brush.GradientStops.Count && i < colorKeys.Length; i++)
        {
            brush.GradientStops[i].Color = (Color)Application.Current.Resources[colorKeys[i]];
        }
    }

    private static LinearGradientBrush CreateGradient(params string[] colorKeys)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = colorKeys.Length > 2 ? new Point(1, 1) : new Point(0, 1)
        };

        for (var i = 0; i < colorKeys.Length; i++)
        {
            var offset = colorKeys.Length == 1 ? 0 : (double)i / (colorKeys.Length - 1);
            brush.GradientStops.Add(new GradientStop((Color)Application.Current.Resources[colorKeys[i]], offset));
        }

        return brush;
    }

    private static void SetNavButtonText(Button button, string text)
    {
        if (button.Content is not StackPanel stack)
        {
            return;
        }

        foreach (var child in stack.Children.OfType<TextBlock>().Skip(1).Take(1))
        {
            child.Text = text;
        }
    }

    private void LocalizeVisualTree(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            switch (child)
            {
                case TextBlock textBlock:
                    LocalizeTextBlock(textBlock);
                    break;
                case ContentControl { Content: string } contentControl:
                    LocalizeContentControl(contentControl);
                    break;
            }

            LocalizeVisualTree(child);
        }
    }

    private void LocalizeTextBlock(TextBlock textBlock)
    {
        var key = textBlock.GetValue(LocalizationKeyProperty) as string;
        if (key is null && LocalizedTextKeys.TryGetValue(textBlock.Text, out var detectedKey))
        {
            key = detectedKey;
            textBlock.SetValue(LocalizationKeyProperty, key);
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            textBlock.Text = T(key);
        }
    }

    private void LocalizeContentControl(ContentControl contentControl)
    {
        var content = contentControl.Content as string;
        if (content is null)
        {
            return;
        }

        var key = contentControl.GetValue(LocalizationKeyProperty) as string;
        if (key is null && LocalizedTextKeys.TryGetValue(content, out var detectedKey))
        {
            key = detectedKey;
            contentControl.SetValue(LocalizationKeyProperty, key);
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            contentControl.Content = T(key);
        }
    }

    private void QuickAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var action = button.Tag?.ToString();
        switch (action)
        {
            case "tournament":
                ShowStatus("Запись на турнир", "Пользователь выбран как участник Dota 2 Weekend Cup. Осталось 5 мест.");
                break;
            case "package":
                NavigateTo("balance");
                ShowStatus("Оплата пакета", "Открыта страница баланса с пакетами времени.");
                break;
            case "profile":
                NavigateTo("cabinet");
                ShowStatus("Профиль клиента", $"Открыт кабинет {_currentUserFullName}.");
                break;
            default:
                NavigateTo(action ?? "dashboard");
                break;
        }
    }

    private void CabinetAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        switch (button.Tag?.ToString())
        {
            case "topup":
                NavigateTo("balance");
                OpenTopupOverlay();
                ShowStatus("Пополнение баланса", "Открыта страница баланса и форма оплаты.");
                break;
            case "package":
                NavigateTo("balance");
                ShowStatus("Оплата пакета", "Выбери пакет времени. После оплаты активная бронь станет игровой сессией.");
                break;
            case "events":
                NavigateTo("events");
                break;
            default:
                ShowStatus("Кабинет", "Действие пока доступно как быстрый сценарий личного кабинета.");
                break;
        }
    }

    private void EventFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string filter)
        {
            return;
        }

        EventFilterAllButton.Style = (Style)FindResource(filter == "all" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        EventFilterDotaButton.Style = (Style)FindResource(filter == "dota" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        EventFilterCsButton.Style = (Style)FindResource(filter == "cs2" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        EventFilterLanButton.Style = (Style)FindResource(filter == "lan" ? "PrimaryButtonStyle" : "GhostButtonStyle");

        DotaEventCard.Visibility = filter is "all" or "dota" ? Visibility.Visible : Visibility.Collapsed;
        CsEventCard.Visibility = filter is "all" or "cs2" ? Visibility.Visible : Visibility.Collapsed;
        LanEventCard.Visibility = filter is "all" or "lan" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EventJoin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string raw)
        {
            return;
        }

        var parts = raw.Split('|');
        var eventName = parts.ElementAtOrDefault(0) ?? "Событие";
        var category = parts.ElementAtOrDefault(1) ?? "Event";
        var time = parts.ElementAtOrDefault(2) ?? "--:--";

        button.Content = "Заявка отправлена";
        button.IsEnabled = false;
        button.Opacity = 0.65;
        EventApplicationsText.Text = $"{category}: {eventName} · {time}";
        ShowStatus("Заявка отправлена", $"{eventName}: заявка добавлена в мои события.");
    }

    private void BalanceAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        switch (button.Tag?.ToString())
        {
            case "topup":
                OpenTopupOverlay();
                break;
            case "promo":
                var promo = string.IsNullOrWhiteSpace(PromoCodeBox.Text) ? "ELITE-NIGHT" : PromoCodeBox.Text.Trim();
                ShowStatus(T("Balance.PromoChecked"), string.Format(T("Balance.PromoToast"), promo));
                break;
            case "offer":
                ShowStatus(T("Balance.PersonalOffer"), T("Balance.OfferToast"));
                break;
            case "export":
                ShowStatus(T("Balance.Export"), T("Balance.ExportToast"));
                break;
            default:
                ShowStatus(T("Nav.Balance"), T("Balance.ActionToast"));
                break;
        }
    }

    private void BalancePackage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string raw)
        {
            return;
        }

        var parts = raw.Split('|');
        var packageName = parts.ElementAtOrDefault(0) ?? T("Balance.Package");
        var price = parts.ElementAtOrDefault(1) ?? string.Empty;
        if (!TryParseMoney(price, out var amount))
        {
            ShowStatus("Оплата пакета", "Не удалось определить стоимость пакета.");
            return;
        }

        if (SavePackagePurchase(packageName, amount, out var resultMessage))
        {
            BalanceAmountText.Text = $"{_balanceAmount:0.##} BYN";
            UpdateCurrentUserUi();
            LoadDatabaseState();
            RefreshAdminUx();
            ShowStatus("Пакет оплачен", resultMessage);
            return;
        }

        ShowStatus("Оплата не выполнена", resultMessage);
        if (resultMessage.Contains("Недостаточно", StringComparison.OrdinalIgnoreCase))
        {
            TopupAmountBox.Text = Math.Max(1, amount - _balanceAmount).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            OpenTopupOverlay();
        }
    }

    private bool SavePackagePurchase(string packageName, decimal amount, out string message)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var user = dbContext.Users.FirstOrDefault(item => item.Id == _currentUserId);
            if (user is null)
            {
                message = "Пользователь не найден в базе данных.";
                return false;
            }

            if (user.Balance < amount)
            {
                message = $"Недостаточно средств: нужно {amount:0.##} BYN, доступно {user.Balance:0.##} BYN.";
                return false;
            }

            var booking = dbContext.Bookings
                .Where(item => item.UserId == user.Id
                    && item.Status != BookingStatuses.Cancelled
                    && item.EndTime >= DateTime.Today)
                .OrderBy(item => item.StartTime < DateTime.Now ? 1 : 0)
                .ThenBy(item => item.StartTime)
                .ThenByDescending(item => item.Id)
                .FirstOrDefault();
            if (booking is null)
            {
                message = "Сначала забронируй ПК, потом оплачивай пакет.";
                return false;
            }

            var computer = dbContext.Computers.FirstOrDefault(item => item.Id == booking.ComputerId);
            if (computer is null)
            {
                message = "ПК из брони не найден в базе данных.";
                return false;
            }

            var durationHours = GetPackageDurationHours(packageName, booking);
            var startTime = booking.StartTime;
            var endTime = startTime.AddHours(durationHours);
            user.Balance -= amount;

            booking.Status = BookingStatuses.Confirmed;
            computer.Status = PcStatuses.Busy;

            var activeSession = dbContext.GameSessions
                .Where(item => item.UserId == user.Id && item.ComputerId == computer.Id && item.EndTime == null)
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault();
            if (activeSession is null)
            {
                dbContext.GameSessions.Add(new GameSession
                {
                    Id = GetNextId(dbContext.GameSessions, session => session.Id),
                    UserId = user.Id,
                    ComputerId = computer.Id,
                    StartTime = startTime,
                    EndTime = endTime,
                    TotalPrice = amount,
                    Status = SessionStatuses.Active
                });
            }
            else
            {
                activeSession.TotalPrice += amount;
                activeSession.StartTime = startTime;
                activeSession.EndTime = endTime;
                activeSession.Status = SessionStatuses.Active;
            }

            dbContext.Payments.Add(new Payment
            {
                Id = GetNextId(dbContext.Payments, payment => payment.Id),
                UserId = user.Id,
                Amount = amount,
                PaymentType = PaymentTypes.Online,
                CreatedAt = DateTime.Now,
                Comment = $"Package purchase: {packageName}; session started on {computer.Name}"
            });

            dbContext.SaveChanges();
            _balanceAmount = user.Balance;
            message = $"{packageName}: списано {amount:0.##} BYN, начата сессия на {computer.Name} до {endTime:HH:mm}.";
            return true;
        }
        catch (Exception ex)
        {
            message = "Не удалось сохранить оплату пакета в базе данных.";
            ShowDatabaseError("Ошибка оплаты пакета", ex);
            return false;
        }
    }

    private static double GetPackageDurationHours(string packageName, Booking booking)
    {
        if (packageName.Contains("Quick", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (packageName.Contains("Evening", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (packageName.Contains("Night", StringComparison.OrdinalIgnoreCase))
        {
            return 8;
        }

        if (packageName.Contains("Weekend", StringComparison.OrdinalIgnoreCase))
        {
            return 12;
        }

        return Math.Max(1, (booking.EndTime - booking.StartTime).TotalHours);
    }

    private void BalancePackageCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
        {
            return;
        }

        if (sender is not FrameworkElement element || element.Tag is not string raw)
        {
            return;
        }

        ShowBalancePackageStatus(raw);
    }

    private void ShowBalancePackageStatus(string raw)
    {
        var parts = raw.Split('|');
        var packageName = parts.ElementAtOrDefault(0) ?? T("Balance.Package");
        var price = parts.ElementAtOrDefault(1) ?? string.Empty;
        var message = string.IsNullOrWhiteSpace(price)
            ? string.Format(T("Balance.PackageToast"), packageName)
            : string.Format(T("Balance.PackageToastWithPrice"), packageName, price);
        ShowStatus(T("Balance.PackageSelected"), message);
    }

    private void SaveGuestSession(string computerName, decimal amount)
    {
        if (!EnsureSignedInForDatabaseWrite())
        {
            return;
        }

        try
        {
            using var dbContext = new AppDbContext();
            var computer = dbContext.Computers.FirstOrDefault(item => item.Name == computerName);
            if (computer is null)
            {
                return;
            }

            dbContext.GameSessions.Add(new GameSession
            {
                Id = GetNextId(dbContext.GameSessions, session => session.Id),
                UserId = _currentUserId,
                ComputerId = computer.Id,
                StartTime = DateTime.Now,
                EndTime = null,
                TotalPrice = amount,
                Status = SessionStatuses.Active
            });

            dbContext.Payments.Add(new Payment
            {
                Id = GetNextId(dbContext.Payments, payment => payment.Id),
                UserId = _currentUserId,
                Amount = amount,
                PaymentType = PaymentTypes.Cash,
                CreatedAt = DateTime.Now,
                Comment = $"Guest session: {computerName}"
            });

            computer.Status = PcStatuses.Busy;
            dbContext.SaveChanges();
            LoadDatabaseState();
            var currentUser = dbContext.Users.AsNoTracking().FirstOrDefault(user => user.Id == _currentUserId);
            if (currentUser is not null)
            {
                RefreshClientUx(dbContext, currentUser);
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка сохранения сессии", ex);
        }
    }

    private void SavePaymentConfirmation(string computerName, decimal amount)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var computer = dbContext.Computers.FirstOrDefault(item => item.Name == computerName);
            if (computer is null)
            {
                return;
            }

            var session = dbContext.GameSessions
                .Where(item => item.ComputerId == computer.Id && item.EndTime == null)
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault();
            if (session is not null)
            {
                session.Status = SessionStatuses.Active;
                session.TotalPrice += amount;
            }

            var booking = dbContext.Bookings
                .Where(item => item.ComputerId == computer.Id && item.Status == BookingStatuses.PendingPayment)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefault();
            if (booking is not null)
            {
                booking.Status = BookingStatuses.Confirmed;
            }

            var pendingPayment = dbContext.Payments
                .Where(item => item.PaymentType.StartsWith(PaymentTypes.Pending) && item.Amount == amount)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefault();

            if (pendingPayment is not null)
            {
                pendingPayment.PaymentType = PaymentTypes.Cash;
                pendingPayment.Comment = $"Payment confirmed: {computerName}";
            }
            else
            {
                var paymentUserId = session?.UserId ?? booking?.UserId ?? ResolveCurrentOrAdminUserId(dbContext);
                if (paymentUserId is null)
                {
                    ShowStatus("Войдите в систему", "Оплата не сохранена: не найден пользователь для записи платежа.");
                    return;
                }

                dbContext.Payments.Add(new Payment
                {
                    Id = GetNextId(dbContext.Payments, payment => payment.Id),
                    UserId = paymentUserId.Value,
                    Amount = amount,
                    PaymentType = PaymentTypes.Cash,
                    CreatedAt = DateTime.Now,
                    Comment = $"Payment confirmed: {computerName}"
                });
            }

            dbContext.SaveChanges();
            LoadDatabaseState();

            var currentUser = dbContext.Users.AsNoTracking().FirstOrDefault(user => user.Id == _currentUserId);
            if (currentUser is not null)
            {
                RefreshClientUx(dbContext, currentUser);
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка подтверждения оплаты", ex);
        }
    }

    private void SaveAllPendingPaymentsAsCash(decimal amountPerPayment)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var pendingBookings = dbContext.Bookings
                .Where(item => item.Status == BookingStatuses.PendingPayment
                    && item.CreatedAt >= today
                    && item.CreatedAt < tomorrow)
                .ToList();
            foreach (var booking in pendingBookings)
            {
                booking.Status = BookingStatuses.Confirmed;
            }

            var pendingSessions = dbContext.GameSessions
                .Where(item => item.Status == SessionStatuses.AwaitingPayment
                    && item.StartTime >= today
                    && item.StartTime < tomorrow)
                .ToList();
            foreach (var session in pendingSessions)
            {
                session.Status = SessionStatuses.Active;
            }

            var pendingPayments = dbContext.Payments
                .Where(item => item.PaymentType.StartsWith(PaymentTypes.Pending)
                    && item.CreatedAt >= today
                    && item.CreatedAt < tomorrow)
                .ToList();
            foreach (var payment in pendingPayments)
            {
                payment.PaymentType = PaymentTypes.Cash;
                payment.Comment = $"{payment.Comment}; confirmed by admin";
            }

            dbContext.SaveChanges();
            LoadDatabaseState();

            var currentUser = dbContext.Users.AsNoTracking().FirstOrDefault(user => user.Id == _currentUserId);
            if (currentUser is not null)
            {
                RefreshClientUx(dbContext, currentUser);
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка закрытия оплат", ex);
        }
    }

    private void SaveSessionClosed(string computerName)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var computer = dbContext.Computers.FirstOrDefault(item => item.Name == computerName);
            if (computer is null)
            {
                return;
            }

            var session = dbContext.GameSessions
                .Where(item => item.ComputerId == computer.Id && item.EndTime == null)
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault();
            if (session is not null)
            {
                session.EndTime = DateTime.Now;
                session.Status = SessionStatuses.Closed;
            }

            computer.Status = PcStatuses.Free;
            dbContext.SaveChanges();
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка закрытия сессии", ex);
        }
    }

    private void SaveSessionExtension(string computerName, decimal amount)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var computer = dbContext.Computers.FirstOrDefault(item => item.Name == computerName);
            if (computer is null)
            {
                return;
            }

            var session = dbContext.GameSessions
                .Where(item => item.ComputerId == computer.Id && item.EndTime == null)
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault();
            if (session is not null)
            {
                session.TotalPrice += amount;
            }

            var paymentUserId = session?.UserId ?? ResolveCurrentOrAdminUserId(dbContext);
            if (paymentUserId is null)
            {
                ShowStatus("Войдите в систему", "Продление не сохранено: не найден пользователь для записи платежа.");
                return;
            }

            dbContext.Payments.Add(new Payment
            {
                Id = GetNextId(dbContext.Payments, payment => payment.Id),
                UserId = paymentUserId.Value,
                Amount = amount,
                PaymentType = PaymentTypes.Online,
                CreatedAt = DateTime.Now,
                Comment = $"Session extension: {computerName}"
            });

            dbContext.SaveChanges();
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка продления сессии", ex);
        }
    }

    private void SaveShiftState(bool closeShift)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var shift = dbContext.Shifts
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault(item => item.EndTime == null)
                ?? dbContext.Shifts.OrderByDescending(item => item.StartTime).FirstOrDefault();

            if (shift is null)
            {
                shift = new Shift
                {
                    Id = GetNextId(dbContext.Shifts, item => item.Id),
                    EmployeeName = _currentUserFullName,
                    StartTime = DateTime.Now,
                    CashTotal = _shiftCash
                };
                dbContext.Shifts.Add(shift);
            }

            shift.EmployeeName = _currentUserFullName;
            shift.CashTotal = _shiftCash;
            shift.EndTime = closeShift ? DateTime.Now : null;
            dbContext.SaveChanges();
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка сохранения смены", ex);
        }
    }

    private void SaveShiftExpense(decimal amount, string comment)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var paymentUserId = ResolveCurrentOrAdminUserId(dbContext);
            if (paymentUserId is null)
            {
                ShowStatus("Войдите в систему", "Расход смены не сохранен: не найден пользователь для записи платежа.");
                return;
            }

            // Demo finance model: negative Payment.Amount marks cash expenses.
            // Income/expense separation is documented in README as a production improvement.
            dbContext.Payments.Add(new Payment
            {
                Id = GetNextId(dbContext.Payments, payment => payment.Id),
                UserId = paymentUserId.Value,
                Amount = -amount,
                PaymentType = PaymentTypes.Cash,
                CreatedAt = DateTime.Now,
                Comment = comment
            });

            var shift = dbContext.Shifts
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault(item => item.EndTime == null);
            if (shift is not null)
            {
                shift.CashTotal = Math.Max(0, shift.CashTotal - amount);
            }

            dbContext.SaveChanges();
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка сохранения расхода", ex);
        }
    }

    private void SaveTariffRate(string namePart, decimal price)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var tariff = dbContext.Tariffs.FirstOrDefault(item => item.Name.Contains(namePart));
            if (tariff is not null)
            {
                tariff.PricePerHour = price;
            }

            var zone = namePart switch
            {
                "Standard" => "Standard",
                "VIP" => "VIP",
                "Royal" => "Royal VIP",
                "Bootcamp" => "Bootcamp",
                _ => namePart
            };

            foreach (var computer in dbContext.Computers.Where(item => item.Zone == zone))
            {
                computer.HourPrice = price;
            }

            dbContext.SaveChanges();
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка сохранения тарифа", ex);
        }
    }

    private void AdminAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var action = element.Tag?.ToString() ?? "admin-action";
        if (HandleAdminSessionAction(action))
        {
            return;
        }

        switch (action)
        {
            case "admin-new-session":
                _adminActiveSessions++;
                _adminFreePcs = Math.Max(0, _adminFreePcs - 1);
                _shiftCash += 8;
                SaveGuestSession("STD-13", 8m);
                SetPcStatus("STD-13", PcStatuses.Busy);
                RefreshAdminUx();
                AddAdminLog("STD-13 started as guest session");
                ShowStatus("Новая сессия", "Запущена гостевая сессия на STD-13. Карта и бронь обновлены.");
                break;

            case "admin-payment":
            case "admin-pay-std10":
                PayFirstPendingAdminSession();
                break;

            case "admin-settle-all":
                SaveAllPendingPaymentsAsCash(18m);
                _shiftCash += _adminPaymentQueue * 18;
                _adminPaymentQueue = 0;
                RefreshAdminUx();
                AddAdminLog("All pending payments settled");
                ShowStatus("Оплаты закрыты", "Все ожидающие платежи отмечены как оплаченные, касса пересчитана.");
                break;

            case "admin-close-vip03":
                CloseAdminSession("VIP-03");
                break;

            case "admin-extend-ba01":
                ExtendAdminSession("BA-01");
                break;

            case "admin-service":
                _adminSupportQueue++;
                _adminFreePcs = Math.Max(0, _adminFreePcs - 1);
                SetPcStatus("STD-06", PcStatuses.Service);
                RefreshAdminUx();
                AddAdminLog("STD-06 moved to service");
                ShowStatus("Сервис", "STD-06 переведен в обслуживание. Карта и выбор брони обновлены.");
                break;

            case "admin-clear-service":
                _adminSupportQueue = Math.Max(0, _adminSupportQueue - 1);
                _adminFreePcs++;
                SetPcStatus("STD-07", PcStatuses.Free);
                SetPcStatus("STD-06", PcStatuses.Free);
                RefreshAdminUx();
                AddAdminLog("Service released STD-06/STD-07");
                ShowStatus("Сервис снят", "ПК вернулся из обслуживания и доступен для брони.");
                break;

            case "shift-close":
                _shiftClosed = !_shiftClosed;
                SaveShiftState(_shiftClosed);
                RefreshAdminUx();
                AddAdminLog(_shiftClosed ? "Shift closed" : "Shift reopened");
                ShowStatus(_shiftClosed ? "Смена закрыта" : "Смена снова активна", _shiftClosed ? "Касса заблокирована для новых расходов, отчет готов." : "Операции смены снова доступны.");
                break;

            case "shift-expense":
                if (_shiftClosed)
                {
                    ShowStatus("Смена закрыта", "Нельзя внести расход после закрытия смены.");
                    break;
                }
                _shiftCash = Math.Max(0, _shiftCash - 35);
                SaveShiftExpense(35m, "Shift expense: расходники");
                RefreshAdminUx();
                AddAdminLog("Expense added: -35 BYN");
                ShowStatus("Расход внесен", "В кассу добавлен расход на расходники: -35 BYN.");
                break;

            case "shift-report":
                AddAdminLog("Shift report generated");
                ShowStatus("Отчет смены", $"Касса: {_shiftCash:0} BYN, онлайн: {_shiftOnline:0} BYN, ожидают оплату: {_adminPaymentQueue}.");
                break;

            case "shift-incident":
                AddIncident($"{DateTime.Now:HH:mm} · Ручная запись смены добавлена администратором");
                _adminSupportQueue++;
                RefreshAdminUx();
                AddAdminLog("Incident added to shift journal");
                ShowStatus("Инцидент добавлен", "Запись появилась в журнале, очередь поддержки увеличена.");
                break;

            case "owner-peak":
                _ownerDemandMode = _ownerDemandMode == "peak" ? "normal" : "peak";
                if (_ownerDemandMode == "peak")
                {
                    _vipRate = Math.Max(_vipRate, 16);
                    _royalRate = Math.Max(_royalRate, 28);
                    SaveTariffRate("VIP", _vipRate);
                    SaveTariffRate("Royal", _royalRate);
                }
                RefreshAdminUx();
                AddAdminLog($"Owner scenario applied: {_ownerDemandMode}");
                ShowStatus("Режим спроса", $"Активный режим: {_ownerDemandMode}. Метрики пересчитаны из тарифов и загрузки.");
                break;

            case "owner-night":
                _ownerDemandMode = _ownerDemandMode == "night" ? "normal" : "night";
                if (_ownerDemandMode == "night")
                {
                    _standardRate = 7;
                    SaveTariffRate("Standard", _standardRate);
                }
                RefreshAdminUx();
                AddAdminLog($"Owner scenario applied: {_ownerDemandMode}");
                ShowStatus("Режим спроса", $"Активный режим: {_ownerDemandMode}. Метрики пересчитаны без ручного накручивания.");
                break;

            case "owner-export":
                AddAdminLog("Owner report exported");
                ShowStatus("Отчет владельца", $"Сводка: выручка {_ownerRevenue} BYN, загрузка {_ownerLoad}%, средний чек {_ownerAverageCheck} BYN.");
                break;

            case "owner-schedule":
                _ownerDemandMode = "loyalty";
                RefreshAdminUx();
                AddAdminLog("Staff schedule reviewed");
                ShowStatus("Расписание открыто", "Включен сценарий лояльности: повторы растут, но выручка считается по текущим тарифам.");
                break;

            case "owner-standard":
                _standardRate = _standardRate == 7 ? 8 : ToggleRate(_standardRate, 8, 9);
                SaveTariffRate("Standard", _standardRate);
                RefreshAdminUx();
                AddAdminLog($"Standard rate changed to {_standardRate} BYN/h");
                ShowStatus("Standard обновлен", $"Новый тариф Standard: {_standardRate} BYN/час. Метрики пересчитаны.");
                break;

            case "owner-vip":
                _vipRate = ToggleRate(_vipRate, 14, 16);
                SaveTariffRate("VIP", _vipRate);
                RefreshAdminUx();
                AddAdminLog($"VIP rate changed to {_vipRate} BYN/h");
                ShowStatus("VIP обновлен", $"Новый тариф VIP: {_vipRate} BYN/час. Метрики пересчитаны.");
                break;

            case "owner-bootcamp":
                _bootcampRate = ToggleRate(_bootcampRate, 50, 60);
                SaveTariffRate("Bootcamp", _bootcampRate);
                RefreshAdminUx();
                AddAdminLog($"Bootcamp hourly rate changed to {_bootcampRate} BYN/h");
                ShowStatus("Bootcamp обновлен", $"Новый тариф Bootcamp: {_bootcampRate} BYN/час. Метрики пересчитаны.");
                break;

            case "owner-royal":
                _royalRate = ToggleRate(_royalRate, 24, 28);
                SaveTariffRate("Royal", _royalRate);
                RefreshAdminUx();
                AddAdminLog($"Royal VIP rate changed to {_royalRate} BYN/h");
                ShowStatus("Royal VIP обновлен", $"Новый тариф Royal VIP: {_royalRate} BYN/час. Метрики пересчитаны.");
                break;

            default:
                ShowStatus("Действие", "Команда выполнена в локальном UX-прототипе.");
                break;
        }
    }
    private void ShiftTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
        {
            return;
        }

        var done = checkBox.IsChecked == true;
        ShowStatus(done ? "Задача выполнена" : "Задача возвращена", checkBox.Content?.ToString() ?? "Задача смены");
    }

    private bool HandleAdminSessionAction(string action)
    {
        var parts = action.Split('|', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        switch (parts[0])
        {
            case "admin-close-session":
                CloseAdminSession(parts[1]);
                return true;

            case "admin-pay-session":
                PayAdminSession(parts[1]);
                return true;

            case "admin-extend-session":
                ExtendAdminSession(parts[1]);
                return true;

            default:
                return false;
        }
    }

    private void CloseAdminSession(string computerName)
    {
        _adminActiveSessions = Math.Max(0, _adminActiveSessions - 1);
        _adminFreePcs++;
        SaveSessionClosed(computerName);
        SetPcStatus(computerName, PcStatuses.Free);
        RefreshAdminUx();
        AddAdminLog($"{computerName} closed and released");
        ShowStatus("Сессия закрыта", $"{computerName} освобожден и стал доступен на карте клуба.");
    }

    private void PayAdminSession(string computerName)
    {
        var amount = GetOpenSessionAmount(computerName) ?? 0m;
        if (amount <= 0)
        {
            ShowStatus("Оплата не найдена", $"{computerName}: нет ожидающей оплаты в активных сессиях.");
            return;
        }

        _adminPaymentQueue = Math.Max(0, _adminPaymentQueue - 1);
        _shiftCash += amount;
        SavePaymentConfirmation(computerName, amount);
        RefreshAdminUx();
        AddAdminLog($"{computerName} payment confirmed");
        ShowStatus("Оплата принята", $"{computerName}: касса +{amount:0.##} BYN.");
    }

    private void PayFirstPendingAdminSession()
    {
        try
        {
            using var dbContext = new AppDbContext();
            var session = dbContext.GameSessions
                .AsNoTracking()
                .Where(item => item.EndTime == null && item.Status == SessionStatuses.AwaitingPayment)
                .OrderBy(item => item.StartTime)
                .FirstOrDefault();
            if (session is null)
            {
                ShowStatus("Оплата не найдена", "В БД нет активных сессий, ожидающих оплату.");
                return;
            }

            var computerName = dbContext.Computers
                .AsNoTracking()
                .Where(item => item.Id == session.ComputerId)
                .Select(item => item.Name)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(computerName))
            {
                ShowStatus("Оплата не найдена", "ПК для ожидающей оплаты не найден в БД.");
                return;
            }

            PayAdminSession(computerName);
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка поиска оплаты", ex);
        }
    }

    private void ExtendAdminSession(string computerName)
    {
        const decimal extensionPrice = 36m;
        _shiftOnline += extensionPrice;
        SaveSessionExtension(computerName, extensionPrice);
        RefreshAdminUx();
        AddAdminLog($"{computerName} extended");
        ShowStatus("Сессия продлена", $"{computerName}: онлайн +{extensionPrice:0.##} BYN.");
    }

    private decimal? GetOpenSessionAmount(string computerName)
    {
        try
        {
            using var dbContext = new AppDbContext();
            return dbContext.GameSessions
                .AsNoTracking()
                .Where(session => session.EndTime == null
                    && dbContext.Computers.Any(computer => computer.Id == session.ComputerId && computer.Name == computerName))
                .OrderByDescending(session => session.StartTime)
                .Select(session => (decimal?)session.TotalPrice)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка чтения сессии", ex);
            return null;
        }
    }

    private void RefreshAdminUx()
    {
        RecalculateOwnerMetrics();
        RebuildAdminSessionsGrid();

        AdminActiveSessionsValue.Text = _adminActiveSessions.ToString();
        AdminPaymentQueueValue.Text = _adminPaymentQueue.ToString();
        AdminFreePcsValue.Text = _adminFreePcs.ToString();
        AdminSupportValue.Text = _adminSupportQueue.ToString();
        ShiftCashValue.Text = $"{_shiftCash:0} BYN";
        ShiftOnlineValue.Text = $"{_shiftOnline:0} BYN";
        OwnerRevenueValue.Text = $"{_ownerRevenue:N0} BYN".Replace(',', ' ');
        OwnerLoadValue.Text = $"{_ownerLoad}%";
        OwnerLoadBar.Value = _ownerLoad;
        OwnerAverageValue.Text = $"{_ownerAverageCheck} BYN";
        OwnerRepeatValue.Text = $"{_ownerRepeatRate}%";
        OwnerStandardPriceText.Text = $"{_standardRate} BYN/час · 14 ПК";
        OwnerVipPriceText.Text = $"{_vipRate} BYN/час · 8 ПК";
        OwnerBootcampPriceText.Text = $"{_bootcampRate} BYN/час · 5 ПК";
        OwnerRoyalPriceText.Text = $"{_royalRate} BYN/час · 5 ПК";
        OwnerPeakModeButton.Style = (Style)FindResource(_ownerDemandMode == "peak" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        OwnerNightModeButton.Style = (Style)FindResource(_ownerDemandMode == "night" ? "PrimaryButtonStyle" : "GhostButtonStyle");
    }

    private void RebuildAdminSessionsGrid()
    {
        if (!IsLoaded || AdminSessionsGrid is null)
        {
            return;
        }

        while (AdminSessionsGrid.Children.Count > 4)
        {
            AdminSessionsGrid.Children.RemoveAt(4);
        }

        while (AdminSessionsGrid.RowDefinitions.Count > 1)
        {
            AdminSessionsGrid.RowDefinitions.RemoveAt(AdminSessionsGrid.RowDefinitions.Count - 1);
        }

        try
        {
            using var dbContext = new AppDbContext();
            var sessions = dbContext.GameSessions
                .AsNoTracking()
                .Where(session => session.EndTime == null && session.Status != SessionStatuses.Closed)
                .OrderBy(session => session.StartTime)
                .Take(5)
                .ToList();
            var computers = dbContext.Computers.AsNoTracking().ToDictionary(computer => computer.Id);
            var users = dbContext.Users.AsNoTracking().ToDictionary(user => user.Id);

            if (sessions.Count == 0)
            {
                AdminSessionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var emptyText = new TextBlock
                {
                    Text = "Нет активных сессий в базе данных.",
                    Foreground = (Brush)FindResource("MutedBrush"),
                    Margin = new Thickness(0, 14, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(emptyText, 1);
                Grid.SetColumnSpan(emptyText, 5);
                AdminSessionsGrid.Children.Add(emptyText);
                return;
            }

            for (var i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                var row = i + 1;
                computers.TryGetValue(session.ComputerId, out var computer);
                users.TryGetValue(session.UserId, out var user);

                var computerName = computer?.Name ?? $"ПК-{session.ComputerId}";
                var clientName = user?.FullName ?? $"User #{session.UserId}";
                var endText = session.EndTime?.ToString("HH:mm") ?? "открыта";
                var statusText = FormatAdminSessionStatus(session.Status);
                var statusBrush = ResolveAdminSessionStatusBrush(session.Status);

                AdminSessionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddAdminSessionCell(row, 0, computerName, "TextBrush", FontWeights.Bold);
                AddAdminSessionCell(row, 1, clientName, "MutedBrush", FontWeights.Normal);
                AddAdminSessionCell(row, 2, endText, "TextBrush", FontWeights.Normal);
                AddAdminSessionCell(row, 3, statusText, statusBrush, FontWeights.Bold);
                AddAdminSessionButton(row, computerName, session.Status);
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка загрузки сессий", ex);
        }
    }

    private void AddAdminSessionCell(int row, int column, string text, string brushKey, FontWeight weight)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource(brushKey),
            FontWeight = weight,
            Margin = new Thickness(0, 14, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        AdminSessionsGrid.Children.Add(block);
    }

    private void AddAdminSessionButton(int row, string computerName, string status)
    {
        var isAwaitingPayment = string.Equals(status, SessionStatuses.AwaitingPayment, StringComparison.OrdinalIgnoreCase);
        var isTeamSession = string.Equals(status, SessionStatuses.Team, StringComparison.OrdinalIgnoreCase);
        var button = new Button
        {
            Content = isAwaitingPayment ? "Оплата" : isTeamSession ? "Продлить" : "Закрыть",
            Style = (Style)FindResource(isAwaitingPayment ? "PrimaryButtonStyle" : "GhostButtonStyle"),
            Tag = isAwaitingPayment
                ? $"admin-pay-session|{computerName}"
                : isTeamSession
                    ? $"admin-extend-session|{computerName}"
                    : $"admin-close-session|{computerName}",
            MinHeight = 30,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(10, 8, 0, 0)
        };
        button.Click += AdminAction_Click;
        Grid.SetRow(button, row);
        Grid.SetColumn(button, 4);
        AdminSessionsGrid.Children.Add(button);
    }

    private static string FormatAdminSessionStatus(string status)
    {
        return status switch
        {
            SessionStatuses.AwaitingPayment => "Ожидает",
            SessionStatuses.Team => "Команда",
            SessionStatuses.Active => "Оплачено",
            _ => status
        };
    }

    private static string ResolveAdminSessionStatusBrush(string status)
    {
        return status switch
        {
            SessionStatuses.AwaitingPayment => "WaitBrush",
            SessionStatuses.Team => "GoldLightBrush",
            SessionStatuses.Active => "OkBrush",
            _ => "MutedBrush"
        };
    }

    private void RecalculateOwnerMetrics()
    {
        const int totalPcs = 56;
        var occupiedPcs = Math.Clamp(totalPcs - _adminFreePcs - _adminSupportQueue, 0, totalPcs);
        var demandMultiplier = _ownerDemandMode switch
        {
            "peak" => 1.18m,
            "night" => 0.88m,
            "loyalty" => 1.05m,
            _ => 1m
        };
        var loadBonus = _ownerDemandMode switch
        {
            "peak" => 7,
            "night" => 3,
            "loyalty" => 2,
            _ => 0
        };

        var standardRevenue = 14 * 3.2m * _standardRate;
        var vipRevenue = 8 * 2.8m * _vipRate;
        var royalRevenue = 5 * 2.4m * _royalRate;
        var bootcampRevenue = _bootcampRate * 0.75m;
        var packageRevenue = _shiftOnline * 0.35m;
        var pendingPenalty = _adminPaymentQueue * 12m;
        var servicePenalty = _adminSupportQueue * 18m;

        _ownerRevenue = (int)Math.Round((standardRevenue + vipRevenue + royalRevenue + bootcampRevenue + packageRevenue + _shiftCash - pendingPenalty - servicePenalty) * demandMultiplier);
        _ownerLoad = Math.Clamp((int)Math.Round(occupiedPcs * 100m / totalPcs) + loadBonus, 0, 100);

        var paidSessions = Math.Max(1, _adminActiveSessions - _adminPaymentQueue);
        _ownerAverageCheck = Math.Max(0, (int)Math.Round(_ownerRevenue / (decimal)paidSessions));
        _ownerRepeatRate = Math.Clamp(58 + (_ownerDemandMode == "loyalty" ? 6 : 0) + (_ownerDemandMode == "night" ? 3 : 0) - Math.Max(0, _adminSupportQueue - 3), 0, 99);
    }
    private void AddIncident(string text)
    {
        var row = new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(0, 0, 0, 10)
        };
        ShiftIncidentList.Children.Insert(Math.Min(1, ShiftIncidentList.Children.Count), row);
    }

    private void AddAdminLog(string text)
    {
        if (!IsLoaded)
        {
            return;
        }

        var row = new TextBlock
        {
            Text = $"{DateTime.Now:HH:mm} · {text}",
            Foreground = (Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        };

        AdminOperationLogList.Children.Insert(0, row);
        while (AdminOperationLogList.Children.Count > 6)
        {
            AdminOperationLogList.Children.RemoveAt(AdminOperationLogList.Children.Count - 1);
        }
    }

    private static int ToggleRate(int current, int normal, int raised)
    {
        return current == normal ? raised : normal;
    }
    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void OpenTopupOverlay()
    {
        TopupOverlay.Visibility = Visibility.Visible;
        ShellRoot.Effect = new BlurEffect { Radius = 7 };
        SelectTopupMethod("card");
        UpdateTopupSummary();
        TopupAmountBox.Focus();
        TopupAmountBox.SelectAll();
    }

    private void CloseTopupOverlay_Click(object sender, RoutedEventArgs e)
    {
        CloseTopupOverlay();
    }

    private void CloseTopupOverlay()
    {
        TopupOverlay.Visibility = Visibility.Collapsed;
        ShellRoot.Effect = null;
        TopupErrorText.Visibility = Visibility.Collapsed;
    }

    private void TopupMethod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string method)
        {
            return;
        }

        SelectTopupMethod(method);
    }

    private void SelectTopupMethod(string method)
    {
        _topupMethod = method;
        TopupCardMethodButton.Style = (Style)FindResource(method == "card" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        TopupEripMethodButton.Style = (Style)FindResource(method == "erip" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        TopupCashMethodButton.Style = (Style)FindResource(method == "cash" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        TopupCardFields.Visibility = method == "card" ? Visibility.Visible : Visibility.Collapsed;
        TopupMethodHintText.Text = method switch
        {
            "erip" => "ЕРИП: будет создан код оплаты. Баланс пополнится после внешнего подтверждения.",
            "cash" => "Наличные: создается заявка для администратора кассы без мгновенного зачисления.",
            _ => "Карта: мгновенное зачисление в демо-режиме."
        };
        ConfirmTopupButton.Content = method == "card" ? "Пополнить" : "Создать заявку";
        TopupErrorText.Visibility = Visibility.Collapsed;
        UpdateTopupSummary();
    }

    private void TopupAmountPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string amount)
        {
            TopupAmountBox.Text = amount;
            UpdateTopupSummary();
        }
    }

    private void TopupAmountBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TopupSummaryText is null)
        {
            return;
        }

        UpdateTopupSummary();
    }

    private void UpdateTopupSummary()
    {
        if (TopupSummaryText is null)
        {
            return;
        }

        if (!TryReadTopupAmount(out var amount))
        {
            TopupSummaryText.Text = "0 BYN";
            TopupBonusText.Text = "Введите сумму больше 0";
            return;
        }

        var bonus = CalculateTopupBonus(amount);
        TopupSummaryText.Text = $"{amount:0.##} BYN";
        TopupBonusText.Text = bonus > 0
            ? $"+{bonus:0.##} бонусов по Gold"
            : "Бонусы начисляются от 50 BYN";
    }

    private bool TryReadTopupAmount(out decimal amount)
    {
        return decimal.TryParse(
            TopupAmountBox.Text.Replace(',', '.'),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out amount) && amount > 0;
    }

    private bool SaveBalanceTopup(decimal amount, decimal bonus)
    {
        try
        {
            using var dbContext = new AppDbContext();
            var user = dbContext.Users.FirstOrDefault(item => item.Id == _currentUserId);
            if (user is null)
            {
                return false;
            }

            user.Balance += amount;
            var nextPaymentId = GetNextId(dbContext.Payments, payment => payment.Id);
            dbContext.Payments.Add(new Payment
            {
                Id = nextPaymentId++,
                UserId = user.Id,
                Amount = amount,
                PaymentType = PaymentTypes.Card,
                CreatedAt = DateTime.Now,
                Comment = bonus > 0
                    ? $"Balance top-up. Bonus: {bonus:0.##} BYN"
                    : "Balance top-up"
            });

            if (bonus > 0)
            {
                dbContext.Payments.Add(new Payment
                {
                    Id = nextPaymentId,
                    UserId = user.Id,
                    Amount = bonus,
                    PaymentType = PaymentTypes.Bonus,
                    CreatedAt = DateTime.Now,
                    Comment = $"Gold bonus from top-up {amount:0.##} BYN"
                });
            }

            dbContext.SaveChanges();
            _balanceAmount = user.Balance;
            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка пополнения баланса", ex);
            return false;
        }
    }

    private bool SavePendingTopupRequest(decimal amount, string method)
    {
        try
        {
            using var dbContext = new AppDbContext();
            if (!dbContext.Users.Any(user => user.Id == _currentUserId))
            {
                return false;
            }

            dbContext.Payments.Add(new Payment
            {
                Id = GetNextId(dbContext.Payments, payment => payment.Id),
                UserId = _currentUserId,
                Amount = amount,
                PaymentType = method == "erip" ? PaymentTypes.PendingErip : PaymentTypes.PendingCash,
                CreatedAt = DateTime.Now,
                Comment = "Pending balance top-up request"
            });

            dbContext.SaveChanges();
            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка заявки на пополнение", ex);
            return false;
        }
    }

    private void ConfirmTopup_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTopupAmount(out var amount))
        {
            TopupErrorText.Text = "Введите корректную сумму пополнения.";
            TopupErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (_topupMethod != "card")
        {
            var requestType = _topupMethod == "erip" ? "ЕРИП" : "наличными";
            if (!SavePendingTopupRequest(amount, _topupMethod))
            {
                TopupErrorText.Text = "Не удалось сохранить заявку в базе данных.";
                TopupErrorText.Visibility = Visibility.Visible;
                return;
            }

            CloseTopupOverlay();
            ShowStatus("Заявка создана", $"Пополнение {requestType} на {amount:0.##} BYN ожидает подтверждения. Баланс пока не изменен.");
            return;
        }

        if (!IsValidDemoCardNumber(TopupCardNumberBox.Text))
        {
            TopupErrorText.Text = "Введите корректный номер карты для демо-оплаты.";
            TopupErrorText.Visibility = Visibility.Visible;
            return;
        }

        var bonus = CalculateTopupBonus(amount);
        if (!SaveBalanceTopup(amount, bonus))
        {
            TopupErrorText.Text = "Не удалось обновить баланс в базе данных.";
            TopupErrorText.Visibility = Visibility.Visible;
            return;
        }

        BalanceAmountText.Text = $"{_balanceAmount:0.##} BYN";
        UpdateCurrentUserUi();
        LoadDatabaseState();
        RefreshAdminUx();
        CloseTopupOverlay();
        ShowStatus("Баланс пополнен", $"Картой зачислено {amount:0.##} BYN. Бонусы: +{bonus:0.##}. Новый баланс: {_balanceAmount:0.##} BYN.");
    }

    private void ZoneCard_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        ShowStatus($"Выбрана зона {element.Tag}", GetZoneDetails(element.Tag?.ToString() ?? "Standard"));
    }

    private void PcButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string raw)
        {
            return;
        }

        var parts = raw.Split('|');
        if (parts.Length < 3)
        {
            return;
        }

        var pc = parts[0];
        var zone = parts[1];
        var status = GetPcStatus(pc, parts[2]);
        _selectedMapPc = pc;
        _selectedMapZone = zone;
        _selectedMapStatus = status;
        var statusText = GetStatusText(status, true);

        PcDetailTitle.Text = pc;
        PcDetailSubtitle.Text = $"{zone}: статус — {statusText}.";
        PcPhotoCaption.Text = $"{pc} · {zone}";
        var specs = GetPcSpecs(zone);
        PcCpuText.Text = specs.Cpu;
        PcGpuText.Text = specs.Gpu;
        PcRamText.Text = specs.Ram;
        PcMonitorText.Text = specs.Monitor;
        PcIntervalsText.Text = status == PcStatuses.Free
            ? "Свободно сегодня: 18:00-20:00, 21:00-23:00."
            : "Ближайший свободный интервал появится после завершения текущего статуса.";
        BookSelectedPcButton.IsEnabled = status == PcStatuses.Free;
        BookSelectedPcButton.Opacity = status == PcStatuses.Free ? 1 : 0.55;
        BookSelectedPcButton.Content = status == PcStatuses.Free
            ? T("Map.BookSelected")
            : T("Map.PcUnavailable");

        ShowStatus($"Выбран {pc}", $"{zone}, статус: {statusText}.");
    }

    private void BookSelectedPc_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedMapPc) || string.IsNullOrWhiteSpace(_selectedMapZone))
        {
            ShowStatus("ПК не выбран", "Сначала выберите место на схеме клуба.");
            return;
        }

        if (_selectedMapStatus != PcStatuses.Free)
        {
            ShowStatus("ПК недоступен", "Этот ПК сейчас нельзя забронировать: он занят, в брони или на обслуживании.");
            return;
        }

        ApplyZoneFromMap(_selectedMapZone);
        _selectedSeats.Clear();
        _selectedSeats.Add(_selectedMapPc);
        RebuildBookingSeatGrid();
        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        NavigateTo("booking");
        ShowStatus("ПК перенесен в бронь", $"{_selectedMapPc} уже выбран в форме бронирования.");
    }

    private void BookingMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        _isCompanyBooking = button.Tag?.ToString() == "company";
        SingleModeButton.Style = (Style)FindResource(_isCompanyBooking ? "GhostButtonStyle" : "PrimaryButtonStyle");
        CompanyModeButton.Style = (Style)FindResource(_isCompanyBooking ? "PrimaryButtonStyle" : "GhostButtonStyle");

        if (!_isCompanyBooking && _selectedSeats.Count > 1)
        {
            var firstSeat = _selectedSeats.First();
            _selectedSeats.Clear();
            _selectedSeats.Add(firstSeat);
        }

        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        ShowStatus(
            _isCompanyBooking ? "Бронь для компании" : "Одиночная бронь",
            _isCompanyBooking ? "Можно выбрать несколько ПК." : "Активен выбор одного ПК.");
    }

    private void BookingDate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && DateTime.TryParse(button.Tag?.ToString(), out var date))
        {
            _bookingDate = date;
            SetActiveButton(button, DateTodayButton, DateTomorrowButton, DateThirdButton, DateCustomButton);
            UpdateBookingSummary();
            ShowStatus("Дата изменена", $"Бронь перенесена на {_bookingDate:yyyy-MM-dd}.");
        }
    }

    private void BookingZone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string raw)
        {
            return;
        }

        var parts = raw.Split('|');
        if (parts.Length != 3 || !int.TryParse(parts[2], out var tariff))
        {
            return;
        }

        _bookingZoneKey = parts[0];
        _bookingZoneName = parts[1];
        _bookingTariff = tariff;
        _selectedSeats.Clear();
        SetActiveButton(button, ZoneStandardButton, ZoneVipButton, ZoneBootcampButton, ZoneRoyalButton);
        RebuildBookingSeatGrid();
        UpdateBookingSummary();
        ShowStatus("Зона изменена", $"Показаны ПК для тарифа: {_bookingZoneName}.");
    }

    private void ToggleTimePicker_Click(object sender, RoutedEventArgs e)
    {
        TimePickerPanel.Visibility = TimePickerPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void BookingHour_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var hour))
        {
            return;
        }

        _bookingHour = hour;
        UpdateBookingTimeButtons();
        UpdateBookingSummary();
        ShowStatus("Время изменено", $"Начало брони: {GetBookingStartTime()}.");
    }

    private void BookingMinute_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !int.TryParse(button.Tag?.ToString(), out var minute))
        {
            return;
        }

        if (!IsMinuteAllowedForCurrentPackage(minute))
        {
            ShowStatus("Минуты недоступны", "Пакетные тарифы стартуют ровно в выбранный час.");
            return;
        }

        _bookingMinute = minute;
        UpdateBookingTimeButtons();
        UpdateBookingSummary();
        ShowStatus("Минуты изменены", $"Начало брони: {GetBookingStartTime()}.");
    }

    private void DurationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var tag = button.Tag?.ToString();
        switch (tag)
        {
            case "night":
                _bookingPackage = "night";
                _bookingDuration = 8;
                _bookingMinute = 0;
                if (!IsNightPackHour(_bookingHour))
                {
                    _bookingHour = 22;
                }
                break;
            case "morning":
                _bookingPackage = "morning";
                _bookingDuration = 3;
                _bookingMinute = 0;
                if (!IsMorningPackHour(_bookingHour))
                {
                    _bookingHour = 6;
                }
                break;
            default:
                if (!int.TryParse(tag, out var duration))
                {
                    return;
                }
                _bookingPackage = "regular";
                _bookingDuration = duration;
                break;
        }

        SetActiveButton(button, Duration1Button, Duration2Button, Duration3Button, MorningPackButton, Duration8Button);
        UpdateBookingTimeButtons();
        UpdateBookingSummary();
        ShowStatus("Тариф обновлен", GetPackageDescription());
    }

    private void BookingSeat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var seat = button.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(seat))
        {
            return;
        }

        if (!_isCompanyBooking)
        {
            _selectedSeats.Clear();
        }

        if (!_selectedSeats.Add(seat))
        {
            _selectedSeats.Remove(seat);
        }

        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        ShowStatus("Выбор ПК обновлен", _selectedSeats.Count == 0 ? "ПК пока не выбран." : $"Выбрано: {string.Join(", ", _selectedSeats)}.");
    }

    private void BookingInput_Changed(object sender, EventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdateBookingSummary();
    }

    private void ConfirmBooking_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSeats.Count == 0)
        {
            BookingErrorText.Text = "Выберите хотя бы один свободный ПК перед подтверждением.";
            BookingErrorText.Visibility = Visibility.Visible;
            ShowStatus("Нужен ПК", "Выберите хотя бы один свободный ПК перед подтверждением.");
            return;
        }

        BookingErrorText.Visibility = Visibility.Collapsed;

        var start = _bookingDate.Date.AddHours(_bookingHour).AddMinutes(_bookingMinute);
        var end = start.AddHours(_bookingDuration);
        if (start <= DateTime.Now.AddMinutes(-15) || end <= start)
        {
            BookingErrorText.Text = "Нельзя создать бронь на прошедшее или некорректное время.";
            BookingErrorText.Visibility = Visibility.Visible;
            ShowStatus("Некорректное время", "Выберите актуальное время начала и длительность брони.");
            return;
        }

        BookingConfirmText.Text =
            $"ПК: {SummarySeatsText.Text}\n" +
            $"Зона: {SummaryZoneText.Text}\n" +
            $"Дата: {SummaryDateText.Text}\n" +
            $"Время: {SummaryTimeText.Text}\n" +
            $"Тариф: {SummaryTariffText.Text}\n" +
            $"Итого: {SummaryTotalText.Text}";

        if (!SaveBookingSelectionToDatabase())
        {
            return;
        }

        ApplyMapPcButtonStatuses();
        RebuildBookingSeatGrid();
        RefreshAdminUx();
        BookingConfirmOverlay.Visibility = Visibility.Visible;
        ShowStatus("Бронь подтверждена", $"{SummarySeatsText.Text}, {SummaryDateText.Text}, {SummaryTimeText.Text}. Итог: {SummaryTotalText.Text}.");
    }

    private bool SaveBookingSelectionToDatabase()
    {
        if (!EnsureSignedInForDatabaseWrite())
        {
            BookingErrorText.Text = "Войдите в систему перед бронированием.";
            BookingErrorText.Visibility = Visibility.Visible;
            return false;
        }

        var start = _bookingDate.Date.AddHours(_bookingHour).AddMinutes(_bookingMinute);
        var end = start.AddHours(_bookingDuration);

        if (end <= start)
        {
            BookingErrorText.Text = "Окончание брони должно быть позже её начала.";
            BookingErrorText.Visibility = Visibility.Visible;
            return false;
        }

        if (start < DateTime.Now.AddMinutes(-15))
        {
            BookingErrorText.Text = "Нельзя бронировать на прошедшее время. Выберите ближайший свободный час.";
            BookingErrorText.Visibility = Visibility.Visible;
            return false;
        }

        try
        {
            using var dbContext = new AppDbContext();

            var seats = _selectedSeats.Order().ToArray();
            var resolvedComputers = new Dictionary<string, Computer>(StringComparer.Ordinal);
            foreach (var seat in seats)
            {
                var computer = dbContext.Computers.FirstOrDefault(item => item.Name == seat);
                if (computer is null)
                {
                    BookingErrorText.Text = $"ПК {seat} не найден в базе данных.";
                    BookingErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                var hasConflict = dbContext.Bookings.Any(booking =>
                    booking.ComputerId == computer.Id
                    && booking.Status != BookingStatuses.Cancelled
                    && booking.StartTime < end
                    && booking.EndTime > start);
                if (hasConflict)
                {
                    BookingErrorText.Text = $"ПК {seat} уже занят на это время. Выберите другой интервал или место.";
                    BookingErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                resolvedComputers[seat] = computer;
            }

            var nextBookingId = dbContext.Bookings.Any() ? dbContext.Bookings.Max(booking => booking.Id) + 1 : 1;
            var isImminent = start.Date == DateTime.Today && start <= DateTime.Now.AddMinutes(15);

            foreach (var seat in seats)
            {
                var computer = resolvedComputers[seat];

                dbContext.Bookings.Add(new Booking
                {
                    Id = nextBookingId++,
                    UserId = _currentUserId,
                    ComputerId = computer.Id,
                    StartTime = start,
                    EndTime = end,
                    Status = BookingStatuses.PendingPayment,
                    CreatedAt = DateTime.Now
                });

                if (isImminent)
                {
                    computer.Status = PcStatuses.Reserved;
                }
            }

            dbContext.SaveChanges();
            LoadDatabaseState();

            var currentUser = dbContext.Users.AsNoTracking().FirstOrDefault(user => user.Id == _currentUserId);
            if (currentUser is not null)
            {
                RefreshClientUx(dbContext, currentUser);
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка сохранения брони", ex);
            BookingErrorText.Text = "Не удалось сохранить бронь: проверьте подключение к SQL Server.";
            BookingErrorText.Visibility = Visibility.Visible;
            return false;
        }
    }

    private void CloseBookingConfirm_Click(object sender, RoutedEventArgs e)
    {
        BookingConfirmOverlay.Visibility = Visibility.Collapsed;
    }

    private void ClearBooking_Click(object sender, RoutedEventArgs e)
    {
        _selectedSeats.Clear();
        UpdateBookingSeatButtons();
        UpdateBookingSummary();
        ShowStatus("Выбор очищен", "Можно собрать бронь заново.");
    }

    private void UpdateBookingSummary()
    {
        if (!IsLoaded)
        {
            return;
        }

        var start = _bookingDate.Date.AddHours(_bookingHour).AddMinutes(_bookingMinute);
        var end = start.AddHours(_bookingDuration);
        var seatsCount = Math.Max(_selectedSeats.Count, 1);
        var total = _bookingTariff * _bookingDuration * seatsCount * GetDiscountFactor();
        var baseTotal = _bookingTariff * _bookingDuration * seatsCount;
        var discount = baseTotal - total;

        SummarySeatsText.Text = _selectedSeats.Count == 0 ? "—" : string.Join(", ", _selectedSeats.Order());
        SummaryZoneText.Text = _bookingZoneName;
        SummaryDateText.Text = _bookingDate.ToString("yyyy-MM-dd");
        SummaryTimeText.Text = $"{start:HH:mm}-{end:HH:mm}";
        SummaryDurationText.Text = $"{_bookingDuration} ч";
        SummaryTariffText.Text = $"{_bookingTariff} BYN/час · {GetTariffLabel()}";
        SummaryBaseTotalText.Text = _selectedSeats.Count == 0 ? "0 BYN" : $"{baseTotal:0.##} BYN";
        SummaryDiscountText.Text = _selectedSeats.Count == 0 ? GetTariffLabel() : $"{GetTariffLabel()} · −{discount:0.##} BYN";
        SummaryTotalText.Text = _selectedSeats.Count == 0 ? "0 BYN" : $"{total:0.##} BYN";
        TimePickerToggleButton.Content = $"Выбрать время: {GetBookingStartTime()}";
        PackageHintText.Text = GetPackageDescription();
        BookingWarningText.Visibility = end.Date > start.Date ? Visibility.Visible : Visibility.Collapsed;
        BookingWarningText.Text = $"Бронь закончится на следующий день: {end:dd.MM HH:mm}.";
        BookingErrorText.Visibility = Visibility.Collapsed;
    }

    private void RebuildBookingSeatGrid()
    {
        if (!IsLoaded)
        {
            return;
        }

        BookingSeatGrid.Children.Clear();
        BookingSeatGrid.Columns = _bookingZoneKey switch
        {
            "VIP" => 4,
            "Bootcamp" => 5,
            "Royal VIP" => 5,
            _ => 7
        };

        foreach (var seat in GetSeatsForZone(_bookingZoneKey))
        {
            var button = new Button
            {
                Content = seat.IsAvailable ? seat.Name : $"{seat.Name}\n{GetStatusText(seat.Status)}",
                Tag = seat.Name,
                Style = (Style)FindResource("PcButtonStyle"),
                Margin = new Thickness(0, 0, 8, 8),
                IsEnabled = seat.IsAvailable,
                ToolTip = seat.IsAvailable ? T("Status.AvailableTooltip") : $"{T("Status.UnavailableTooltip")}: {GetStatusText(seat.Status)}"
            };
            if (!seat.IsAvailable)
            {
                button.Style = (Style)FindResource("UnavailablePcButtonStyle");
            }
            button.Click += BookingSeat_Click;
            BookingSeatGrid.Children.Add(button);
        }

        UpdateBookingSeatButtons();
    }

    private void RebuildBookingTimePicker()
    {
        BookingHourGrid.Children.Clear();
        for (var hour = 0; hour < 24; hour++)
        {
            var button = new Button
            {
                Content = $"{hour:00}",
                Tag = hour,
                Style = (Style)FindResource(hour == _bookingHour ? "SelectedTimeButtonStyle" : "TimeButtonStyle"),
                Margin = new Thickness(0, 0, 8, 8),
                IsEnabled = IsHourAllowedForCurrentPackage(hour)
            };
            if (!button.IsEnabled)
            {
                button.Style = (Style)FindResource("UnavailablePcButtonStyle");
            }
            button.Click += BookingHour_Click;
            BookingHourGrid.Children.Add(button);
        }

        BookingMinuteGrid.Children.Clear();
        foreach (var minute in new[] { 0, 15, 30, 45 })
        {
            var button = new Button
            {
                Content = $"{minute:00}",
                Tag = minute,
                Style = (Style)FindResource(minute == _bookingMinute ? "SelectedTimeButtonStyle" : "TimeButtonStyle"),
                Margin = new Thickness(0, 0, 0, 8)
            };
            button.Click += BookingMinute_Click;
            BookingMinuteGrid.Children.Add(button);
        }
    }

    private void UpdateBookingTimeButtons()
    {
        foreach (var child in BookingHourGrid.Children)
        {
            if (child is Button button)
            {
                var hour = int.TryParse(button.Tag?.ToString(), out var parsedHour) ? parsedHour : -1;
                button.IsEnabled = IsHourAllowedForCurrentPackage(hour);
                button.Style = (Style)FindResource(!button.IsEnabled
                    ? "UnavailablePcButtonStyle"
                    : hour == _bookingHour ? "SelectedTimeButtonStyle" : "TimeButtonStyle");
            }
        }

        foreach (var child in BookingMinuteGrid.Children)
        {
            if (child is Button button)
            {
                var minute = int.TryParse(button.Tag?.ToString(), out var parsedMinute) ? parsedMinute : -1;
                button.IsEnabled = IsMinuteAllowedForCurrentPackage(minute);
                button.Style = (Style)FindResource(!button.IsEnabled
                    ? "UnavailablePcButtonStyle"
                    : minute == _bookingMinute ? "SelectedTimeButtonStyle" : "TimeButtonStyle");
            }
        }
    }

    private SeatInfo[] GetSeatsForZone(string zone)
    {
        var databaseSeats = _computers
            .Where(computer => computer.Zone.Equals(zone, StringComparison.OrdinalIgnoreCase))
            .OrderBy(computer => computer.Id)
            .Select(computer => new SeatInfo(computer.Name, NormalizePcStatus(computer.Status)))
            .ToArray();

        return databaseSeats
            .Select(seat => seat with { Status = GetPcStatus(seat.Name, seat.Status) })
            .ToArray();
    }

    private string GetPcStatus(string pc, string fallback)
    {
        return _pcStatusOverrides.TryGetValue(pc, out var status) ? NormalizePcStatus(status) : NormalizePcStatus(fallback);
    }

    private void SetPcStatus(string pc, string status)
    {
        status = NormalizePcStatus(status);
        _pcStatusOverrides[pc] = status;
        SaveComputerStatus(pc, status);
        ApplyMapPcButtonStatuses();
        RebuildBookingSeatGrid();

        if (_selectedMapPc == pc)
        {
            _selectedMapStatus = status;
        }
    }

    private void SaveComputerStatus(string pc, string status)
    {
        var localComputer = _computers.FirstOrDefault(computer => computer.Name == pc);
        if (localComputer is not null)
        {
            localComputer.Status = status;
        }

        try
        {
            using var dbContext = new AppDbContext();
            var computer = dbContext.Computers.FirstOrDefault(item => item.Name == pc);
            if (computer is null)
            {
                return;
            }

            computer.Status = status;
            dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("Ошибка сохранения статуса ПК", ex);
        }
    }

    private static string NormalizePcStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "available" => PcStatuses.Free,
            "active" => PcStatuses.Busy,
            "occupied" => PcStatuses.Busy,
            "maintenance" => PcStatuses.Service,
            "" => PcStatuses.Free,
            var value => value
        };
    }

    private void SetActiveButton(Button activeButton, params Button[] buttons)
    {
        foreach (var button in buttons)
        {
            button.Style = (Style)FindResource(ReferenceEquals(button, activeButton) ? "PrimaryButtonStyle" : "GhostButtonStyle");
        }
    }

    private string GetBookingStartTime()
    {
        return $"{_bookingHour:00}:{_bookingMinute:00}";
    }

    private bool IsHourAllowedForCurrentPackage(int hour)
    {
        return _bookingPackage switch
        {
            "night" => IsNightPackHour(hour),
            "morning" => IsMorningPackHour(hour),
            _ => true
        };
    }

    private bool IsMinuteAllowedForCurrentPackage(int minute)
    {
        return _bookingPackage == "regular" || minute == 0;
    }

    private static bool IsNightPackHour(int hour)
    {
        return hour is 22 or 23 or 0;
    }

    private static bool IsMorningPackHour(int hour)
    {
        return hour is 6 or 7 or 8;
    }

    private decimal GetDiscountFactor()
    {
        return _bookingPackage switch
        {
            "night" => 0.75m,
            "morning" => 0.8m,
            _ => 0.9m
        };
    }

    private string GetTariffLabel()
    {
        return _bookingPackage switch
        {
            "night" => "Night Pack −25%",
            "morning" => "Morning Pack −20%",
            _ => "Gold −10%"
        };
    }

    private string GetPackageDescription()
    {
        return _bookingPackage switch
        {
            "night" => "Night Pack: 8 часов, старт только 22:00, 23:00 или 00:00, скидка 25%.",
            "morning" => "Morning Pack: 3 часа, старт только 06:00, 07:00 или 08:00, скидка 20%.",
            _ => $"Обычный тариф: {_bookingDuration} ч, скидка Gold 10%."
        };
    }

    private void UpdateBookingSeatButtons()
    {
        foreach (var child in BookingSeatGrid.Children)
        {
            if (child is Button button)
            {
                if (!button.IsEnabled)
                {
                    button.Style = (Style)FindResource("UnavailablePcButtonStyle");
                    continue;
                }

                var isSelected = _selectedSeats.Contains(button.Tag?.ToString() ?? string.Empty);
                button.Style = (Style)FindResource(isSelected ? "PrimaryButtonStyle" : "PcButtonStyle");
            }
        }
    }

    private void ApplyZoneFromMap(string zone)
    {
        Button button = zone switch
        {
            "VIP" => ZoneVipButton,
            "Bootcamp" => ZoneBootcampButton,
            "Royal VIP" => ZoneRoyalButton,
            _ => ZoneStandardButton
        };

        if (button.Tag is not string raw)
        {
            return;
        }

        var parts = raw.Split('|');
        if (parts.Length != 3 || !int.TryParse(parts[2], out var tariff))
        {
            return;
        }

        _bookingZoneKey = parts[0];
        _bookingZoneName = parts[1];
        _bookingTariff = tariff;
        SetActiveButton(button, ZoneStandardButton, ZoneVipButton, ZoneBootcampButton, ZoneRoyalButton);
    }

    private void ApplyMapPcButtonStatuses()
    {
        foreach (var button in FindVisualChildren<Button>(MapView))
        {
            if (button.Tag is not string raw)
            {
                continue;
            }

            var parts = raw.Split('|');
            if (parts.Length != 3)
            {
                continue;
            }

            var pc = parts[0];
            var status = GetPcStatus(pc, parts[2]);
            var seat = new SeatInfo(pc, status);
            button.Content = seat.IsAvailable ? pc : $"{pc}\n{GetStatusText(seat.Status)}";
            button.Style = (Style)FindResource(seat.IsAvailable ? "PcButtonStyle" : "UnavailablePcButtonStyle");
            button.ToolTip = seat.IsAvailable ? T("Status.AvailableTooltip") : $"{T("Status.UnavailableTooltip")}: {GetStatusText(seat.Status)}";
            button.IsEnabled = true;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null)
        {
            yield break;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private string GetZoneDetails(string zone)
    {
        return zone switch
        {
            "Standard" => "Standard: 18 свободных ПК, тариф 8 BYN/час.",
            "VIP" => "VIP: 4 свободных ПК, тариф 14 BYN/час.",
            "Bootcamp" => "Bootcamp: 1 свободная комната, тариф 50 BYN/час.",
            "Royal VIP" => "Royal VIP: 3 свободных ПК, тариф 18 BYN/час.",
            _ => "Зона выбрана."
        };
    }

    private static PcSpecs GetPcSpecs(string zone)
    {
        return zone switch
        {
            "Standard" => new PcSpecs("Intel Core i5-13400F", "GeForce RTX 4060", "16 GB DDR4", "24\" 144 Hz"),
            "VIP" => new PcSpecs("AMD Ryzen 5 7600X", "GeForce RTX 4070 Super", "32 GB DDR5", "27\" 180 Hz"),
            "Bootcamp" => new PcSpecs("Intel Core i7-13700KF", "GeForce RTX 4070 Ti", "32 GB DDR5", "27\" 240 Hz"),
            "Royal VIP" => new PcSpecs("AMD Ryzen 7 7800X3D", "GeForce RTX 4080 Super", "64 GB DDR5", "32\" 240 Hz"),
            _ => new PcSpecs("Intel Core i5", "GeForce RTX", "16 GB", "144 Hz")
        };
    }

    private void NotificationButton_Click(object sender, RoutedEventArgs e)
    {
        _isNotificationCenterOpen = !_isNotificationCenterOpen;
        NotificationCenter.Visibility = _isNotificationCenterOpen ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void MarkNotifications_Click(object sender, RoutedEventArgs e)
    {
        _unreadNotifications = 0;
        UpdateNotificationBadge();

        foreach (var dot in FindVisualChildren<Border>(NotificationList).Where(border => Equals(border.Tag, "notification-dot")))
        {
            dot.Visibility = Visibility.Collapsed;
        }

        NotificationUnreadBookingDot.Visibility = Visibility.Collapsed;
        NotificationUnreadSessionDot.Visibility = Visibility.Collapsed;
        NotificationUnreadTournamentDot.Visibility = Visibility.Collapsed;
        NotificationEmptyText.Visibility = NotificationList.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        _suppressNotificationWrite = true;
        ShowStatus("Уведомления прочитаны", "Новых уведомлений нет, список остался как история действий.");
        e.Handled = true;
    }
    private void RootGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isNotificationCenterOpen || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsDescendantOf(source, NotificationCenter) || IsDescendantOf(source, NotificationButton))
        {
            return;
        }

        CloseNotificationCenter();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && TopupOverlay.Visibility == Visibility.Visible)
        {
            CloseTopupOverlay();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape || !_isNotificationCenterOpen)
        {
            return;
        }

        CloseNotificationCenter();
        e.Handled = true;
    }

    private void CloseNotificationCenter()
    {
        _isNotificationCenterOpen = false;
        NotificationCenter.Visibility = Visibility.Collapsed;
    }

    private static bool IsDescendantOf(DependencyObject source, DependencyObject parent)
    {
        var current = source;
        while (current is not null)
        {
            if (ReferenceEquals(current, parent))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
    private void GlobalSearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (GlobalSearchBox.Text != SearchPlaceholder)
        {
            return;
        }

        GlobalSearchBox.Text = string.Empty;
        GlobalSearchBox.Foreground = (Brush)FindResource("TextBrush");
    }

    private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || GlobalSearchBox.Text == SearchPlaceholder)
        {
            return;
        }

        var query = GlobalSearchBox.Text.Trim();
        if (query.Length < 2)
        {
            return;
        }

        var freePcs = _computers.Count(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free);
        var activeBookings = 0;
        try
        {
            using var dbContext = new AppDbContext();
            activeBookings = dbContext.Bookings.Count(booking => booking.Status != BookingStatuses.Cancelled);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search database lookup failed: {ex}");
            activeBookings = _adminPaymentQueue;
        }

        var result = query.ToLowerInvariant() switch
        {
            var text when text.Contains("pc") || text.Contains("пк") || text.Contains("vip") =>
                $"Демо-поиск: найдено свободных ПК: {freePcs}. Открой схему клуба для выбора места.",
            var text when text.Contains("брон") || text.Contains("плат") || text.Contains("баланс") =>
                $"Демо-поиск: активных броней в БД: {activeBookings}, очередь оплат: {_adminPaymentQueue}.",
            _ => $"Демо-поиск обработал \"{query}\". Для курсовой подключены быстрые ветки по ПК, броням и платежам."
        };

        _suppressNotificationWrite = true;
        ShowStatus("Результат поиска", result);
    }

    private void ShowStatus(string title, string body)
    {
        StatusTitleText.Text = title;
        StatusBodyText.Text = body;
        StatusToast.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();

        if (_suppressNotificationWrite)
        {
            _suppressNotificationWrite = false;
            return;
        }

        AddNotification(title, body);
    }

    private void AddNotification(string title, string body)
    {
        if (!IsLoaded)
        {
            return;
        }

        NotificationEmptyText.Visibility = Visibility.Collapsed;

        var dot = new Border
        {
            Tag = "notification-dot",
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Background = (Brush)FindResource("GoldLightBrush"),
            Margin = new Thickness(0, 7, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };

        var content = new StackPanel { Margin = new Thickness(0) };
        content.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = (Brush)FindResource("GoldLightBrush"),
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = body,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = DateTime.Now.ToString("HH:mm"),
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 12,
            Margin = new Thickness(0, 5, 0, 0)
        });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(content, 1);
        grid.Children.Add(dot);
        grid.Children.Add(content);

        var card = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderBrush = (Brush)FindResource("LineBrush"),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(0x14, 0xD4, 0xAF, 0x37)),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 9),
            Child = grid
        };

        NotificationList.Children.Insert(0, card);
        while (NotificationList.Children.Count > 12)
        {
            NotificationList.Children.RemoveAt(NotificationList.Children.Count - 1);
        }

        _unreadNotifications = Math.Min(99, _unreadNotifications + 1);
        UpdateNotificationBadge();
    }

    private void UpdateNotificationBadge()
    {
        NotificationCountText.Text = _unreadNotifications.ToString();
        NotificationBadge.Visibility = _unreadNotifications > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void StartAnnouncementMarquee()
    {
        AnnouncementTextA.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        AnnouncementTextB.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        ResetAnnouncementMarquee();
        _lastAnnouncementTick = DateTime.Now;
        _announcementTimer.Start();
    }

    private void ResetAnnouncementMarquee()
    {
        var textWidth = GetAnnouncementTextWidth();
        Canvas.SetLeft(AnnouncementTextA, 0);
        Canvas.SetLeft(AnnouncementTextB, textWidth + AnnouncementGap);
    }

    private void AnnouncementTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var elapsedSeconds = (now - _lastAnnouncementTick).TotalSeconds;
        _lastAnnouncementTick = now;

        var distance = AnnouncementSpeed * elapsedSeconds;
        MoveAnnouncementText(AnnouncementTextA, distance);
        MoveAnnouncementText(AnnouncementTextB, distance);
    }

    private void MoveAnnouncementText(TextBlock textBlock, double distance)
    {
        var textWidth = GetAnnouncementTextWidth();
        var left = Canvas.GetLeft(textBlock);
        if (double.IsNaN(left))
        {
            left = 0;
        }

        left -= distance;
        if (left <= -(textWidth + AnnouncementGap))
        {
            var otherLeft = ReferenceEquals(textBlock, AnnouncementTextA)
                ? Canvas.GetLeft(AnnouncementTextB)
                : Canvas.GetLeft(AnnouncementTextA);
            left = otherLeft + textWidth + AnnouncementGap;
        }

        Canvas.SetLeft(textBlock, left);
    }

    private double GetAnnouncementTextWidth()
    {
        AnnouncementTextA.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Max(AnnouncementTextA.DesiredSize.Width, 1);
    }

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
