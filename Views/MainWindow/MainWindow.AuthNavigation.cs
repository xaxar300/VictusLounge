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
using VictusLounge.ViewModels;

namespace VictusLounge;

public partial class MainWindow
{
    private void SelectAuthRole(string role)
    {
        _currentRole = role;
        UpdateAuthRoleButtons();
        ShowStatus("Role selected", $"After login: {GetRoleTitle(_currentRole)} mode.");
    }

    private void ShowAuthView(bool isRegister)
    {
        _viewModel.Auth.IsRegisterMode = isRegister;
        ApplyAuthViewState();
    }

    private void ApplyAuthViewState()
    {
        var isRegister = _viewModel.Auth.IsRegisterMode;
        LoginAuthView.Visibility = isRegister ? Visibility.Collapsed : Visibility.Visible;
        RegisterAuthView.Visibility = isRegister ? Visibility.Visible : Visibility.Collapsed;
        AuthErrorText.Text = _viewModel.Auth.ErrorMessage;
        RegisterErrorText.Text = _viewModel.Auth.RegisterErrorMessage;
        AuthErrorText.Visibility = _viewModel.Auth.HasError ? Visibility.Visible : Visibility.Collapsed;
        RegisterErrorText.Visibility = _viewModel.Auth.HasRegisterError ? Visibility.Visible : Visibility.Collapsed;
        AuthWindowTitleText.Text = _viewModel.Auth.AuthTitle;
        UpdateAuthRoleButtons();
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
        _viewModel.Navigation.IsAuthOverlayVisible = false;

        LoadDatabaseState();
        UpdateCurrentUserUi();
        UpdateAuthRoleButtons();
        NavigateTo(GetDefaultViewForRole(_currentRole));
        ApplySidebarState(false);
        ShowStatus("Вход выполнен", $"{_currentUserFullName}: открыт режим {GetRoleTitle(_currentRole)}.");
    }

    private void ShowAuthError(string message)
    {
        _viewModel.Auth.ErrorMessage = message;
        ApplyAuthViewState();
    }

    private void ShowRegisterError(string message)
    {
        _viewModel.Auth.RegisterErrorMessage = message;
        ApplyAuthViewState();
    }

    private void UpdateCurrentUserUi()
    {
        if (!IsLoaded)
        {
            return;
        }

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
        if (navigateIfNeeded && !IsViewAllowedForRole(_currentView))
        {
            NavigateTo(GetDefaultViewForRole(_currentRole));
        }

        ApplySidebarState(false);
    }

    private bool IsViewAllowedForRole(string view)
    {
        return _currentRole switch
        {
            "client" => view is "dashboard" or "map" or "booking" or "cabinet" or "balance" or "events" or "settings",
            "admin" => view is "map" or "admin" or "shift" or "settings",
            "owner" => view is "owner" or "admin" or "shift" or "settings",
            _ => view is "settings"
        };
    }

    private static string GetDefaultViewForRole(string role)
    {
        return role switch
        {
            "admin" => "admin",
            "owner" => "owner",
            _ => "dashboard"
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

    private void ConfigureNavigationCommands()
    {
        _viewModel.Navigation.NavigateCommand = new RelayCommand(view => NavigateTo(view?.ToString() ?? "dashboard"));
        _viewModel.Navigation.LogoutCommand = new RelayCommand(_ => Logout());
        _viewModel.Navigation.ToggleSidebarCommand = new RelayCommand(_ => ToggleSidebar());
    }

    private void Logout()
    {
        _currentUserId = 0;
        _currentUserFullName = "Not signed in";
        _currentUserLogin = string.Empty;
        _currentRole = "client";
        _viewModel.Navigation.IsAuthOverlayVisible = true;
        UpdateCurrentUserUi();
        UpdateAuthRoleButtons();
        ApplyRoleAccess(false);
        ShowAuthView(isRegister: false);
    }

    private void ToggleSidebar()
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;
        _viewModel.Navigation.IsSidebarCollapsed = _isSidebarCollapsed;

        ApplySidebarState();

        ShowStatus(
            _isSidebarCollapsed ? "Меню свернуто" : "Меню раскрыто",
            _isSidebarCollapsed ? "Навигация оставлена в компактном режиме." : "Полная навигация снова доступна.");
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
        SidebarToggleIcon.Data = Geometry.Parse(_isSidebarCollapsed
            ? "M 5 5 L 9 9 L 5 13"
            : "M 9 5 L 5 9 L 9 13");

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
            ShowStatus("Раздел недоступен", $"{GetRoleTitle(_currentRole)} работает только со своими рабочими инструментами.");
            return;
        }

        _currentView = view;
        _viewModel.Navigation.CurrentView = view;
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
        _viewModel.Navigation.CurrentTitle = title;
        if (view is "map" or "cabinet" or "balance")
        {
            LoadDatabaseState();
        }
        if (view == "map")
        {
            Dispatcher.InvokeAsync(ApplyMapPcButtonStatuses, DispatcherPriority.Loaded);
        }
        ShowStatus(title, $"Открыт раздел: {title}.");
    }

    private void ApplyTheme(string theme, bool showToast = true)
    {
        _currentTheme = theme;
        ApplyThemeResources(theme);
        UpdateThemeButtons();
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
        GlobalSearchBox.ToolTip = T("Common.SearchPlaceholder");

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
        _viewModel.Navigation.CurrentTitle = _currentView switch
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
        BalancePromoLabelText.Text = T("Balance.Promo");
        BalanceApplyPromoButton.Content = T("Balance.Apply");
        if (_viewModel.Balance.IsRegularPackagesVisible)
        {
            _viewModel.Balance.ShowDefaultPackageOffer(T("Balance.Packages"), T("Balance.QuickGame"), T("Balance.Buy"));
        }
        EveningPackageText.Text = T("Balance.EveningPack");
        NightPackageText.Text = T("Balance.NightPack");
        BootcampPackageText.Text = T("Balance.BootcampTraining");
        WeekendPackageText.Text = T("Balance.WeekendPass");
        EveningBuyButton.Content = T("Balance.Buy");
        NightBuyButton.Content = T("Balance.Buy");
        BootcampBuyButton.Content = T("Balance.Buy");
        WeekendBuyButton.Content = T("Balance.Buy");
        BalancePersonalOfferLabelText.Text = T("Balance.PersonalOffer");
        _viewModel.Balance.PersonalOfferText = T("Balance.PersonalOfferText");
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
