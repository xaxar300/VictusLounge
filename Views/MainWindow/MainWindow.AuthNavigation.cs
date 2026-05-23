using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using VictusLounge.Data;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.Repositories;

namespace VictusLounge;

public partial class MainWindow
{
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
            ? "Р РµРіРёСЃС‚СЂР°С†РёСЏ РІ Elite Gaming Lounge"
            : "Р’С…РѕРґ РІ Elite Gaming Lounge";
        UpdateAuthRoleButtons();
    }

    private void LoginFromDatabase()
    {
        var login = LoginBox.Text.Trim();
        var password = LoginPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            ShowAuthError("Р’РІРµРґРёС‚Рµ Р»РѕРіРёРЅ Рё РїР°СЂРѕР»СЊ.");
            return;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var user = unitOfWork.Users.GetByLogin(login);
            if (user is null)
            {
                ShowAuthError("РќРµРІРµСЂРЅС‹Р№ Р»РѕРіРёРЅ РёР»Рё РїР°СЂРѕР»СЊ.");
                return;
            }

            if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                if (PasswordHasher.IsHashed(user.PasswordHash)
                    || !string.Equals(user.PasswordHash, password, StringComparison.Ordinal))
                {
                    ShowAuthError("РќРµРІРµСЂРЅС‹Р№ Р»РѕРіРёРЅ РёР»Рё РїР°СЂРѕР»СЊ.");
                    return;
                }

                user.PasswordHash = PasswordHasher.HashPassword(password);
                unitOfWork.SaveChanges();
            }

            SignInUser(user);
        }
        catch (Exception ex)
        {
            ShowAuthError($"РќРµ СѓРґР°Р»РѕСЃСЊ РїРѕРґРєР»СЋС‡РёС‚СЊСЃСЏ Рє SQL Server: {ex.Message}");
        }
    }

    private void RegisterAndLogin()
    {
        var fullName = RegisterFullNameBox.Text.Trim();
        var login = RegisterLoginBox.Text.Trim();
        var password = RegisterPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            ShowRegisterError("Р—Р°РїРѕР»РЅРёС‚Рµ РёРјСЏ, Р»РѕРіРёРЅ Рё РїР°СЂРѕР»СЊ.");
            return;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            if (unitOfWork.Users.Any(user => user.Login == login))
            {
                ShowRegisterError("РџРѕР»СЊР·РѕРІР°С‚РµР»СЊ СЃ С‚Р°РєРёРј Р»РѕРіРёРЅРѕРј СѓР¶Рµ РµСЃС‚СЊ.");
                return;
            }

            var user = new User
            {
                Id = unitOfWork.Users.GetNextId(item => item.Id),
                FullName = fullName,
                Login = login,
                PasswordHash = PasswordHasher.HashPassword(password),
                Role = "Client",
                Balance = 0m
            };

            unitOfWork.Users.Add(user);
            unitOfWork.SaveChanges();
            SignInUser(user);
        }
        catch (Exception ex)
        {
            ShowRegisterError($"РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕС…СЂР°РЅРёС‚СЊ РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ РІ SQL Server: {ex.Message}");
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
        ShowStatus("Р’С…РѕРґ РІС‹РїРѕР»РЅРµРЅ", $"{_currentUserFullName}: РѕС‚РєСЂС‹С‚ СЂРµР¶РёРј {GetRoleTitle(_currentRole)}.");
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
        ProfileRoleText.Text = $"{GetRoleTitle(_currentRole)} В· {_currentUserLogin}";
        ProfileInitialsText.Text = GetInitials(_currentUserFullName);
        WorkspaceText.Text = $"{GetRoleTitle(_currentRole)} workspace";
        UpdateCurrentBalanceText();
        SyncCurrentUserViewModel();
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

    private static string GetClientTier(User user)
    {
        return BetterTier(user.LoyaltyTier, GetClientTier(user.Balance));
    }

    private static string BetterTier(string current, string candidate)
    {
        return TierRank(candidate) > TierRank(current) ? candidate : current;
    }

    private static int TierRank(string tier)
    {
        return tier switch
        {
            "Elite" => 3,
            "Gold" => 2,
            "Silver" => 1,
            _ => 0
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
        BalanceNavButton.Visibility = _currentRole is "client" or "admin" ? Visibility.Visible : Visibility.Collapsed;
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
            "admin" => view is "dashboard" or "map" or "booking" or "balance" or "events" or "admin" or "shift" or "settings",
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
        if (view is "map" or "cabinet" or "balance")
        {
            LoadDatabaseState();
        }
        if (view == "map")
        {
            Dispatcher.InvokeAsync(ApplyMapPcButtonStatuses, DispatcherPriority.Loaded);
        }
        ShowStatus(title, $"РћС‚РєСЂС‹С‚ СЂР°Р·РґРµР»: {title}.");
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
        BalanceBonusText.Text = "РџРѕР»СѓС‡РµРЅРѕ Р±РѕРЅСѓСЃРѕРІ: 0";
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

}

